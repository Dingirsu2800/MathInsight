# Tasks Checklist: Identity & Access Module

**Branch**: `001-identity-access` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Updated**: 2026-07-14

> **Two design decisions changed this checklist.** Read them in spec.md before starting.
>
> - **DD-01** — Self-registration writes **nothing** to SQL. The payload lives in Redis until
>   the user confirms; the `Account` row is created **at confirmation**, with
>   `is_active = true`. Consequence: `is_active = false` means *only* "deactivated by Admin".
> - **DD-02** — Login issues an access token **and** a refresh token, both tracked in Redis.
>   Refreshing rotates the refresh token.
>
> Several tasks previously marked `[x]` have been **reopened** because they were written
> against the old model. They are flagged below.

---

## Phase 1: Persistence Setup

**Blocks everything else. Do this first.**

- [ ] EF `IEntityTypeConfiguration` for all 8 entities, mapped to the current DB script tables:
  - [x] `AccountConfiguration` — unique indexes on `username`, `email`; FK to `roles`
  - [ ] `RoleConfiguration` — seed 4 roles (Admin, Expert, Teacher, Student)
  - [ ] `PermissionConfiguration` — seed permission keys from the Permission Matrix
  - [ ] `RolePermissionConfiguration` — composite PK; seed default role-permissions
  - [x] `StudentConfiguration` — 1:1 with Account
  - [x] `TeacherConfiguration` — 1:1 with Account
  - [x] `ExpertConfiguration` — 1:1 with Account
  - [ ] `TeacherApplicationConfiguration` — status constrained to `Pending`/`Approved`/`Rejected`
- [x] `IdentityDbContext.cs` — shared connection, explicit `ToTable(...)` mappings
- [x] Do **not** add an EF migration unless the team explicitly switches from SQL-script
      source-of-truth to EF-migration source-of-truth
- [ ] Seed data per TDS §3.6 (5 accounts + roles), all with `is_active = true`
- [ ] Add Permission and RolePermission seed rows to
      `database/002_Seed_MathInsight_Demo.sql`, mirroring the HasData seed in
      `PermissionConfiguration.cs` and `RolePermissionConfiguration.cs`. Without this,
      both tables are empty at runtime — HasData is inert because the database is
      built from SQL scripts, not EF migrations — and permission-based
      authorization will not work.

---

## Phase 2: Contracts & Infrastructure

- [ ] `Contracts/AuthErrorCodes.cs` — replace the inline string literals currently in
      `AuthController` with constants:
      `AUTH_INVALID_CREDENTIALS`, `AUTH_ACCOUNT_LOCKED`, `AUTH_ACCOUNT_DEACTIVATED`,
      `AUTH_APPLICATION_PENDING`, `AUTH_APPLICATION_REJECTED`, `AUTH_TOKEN_EXPIRED`,
      `AUTH_TOKEN_INVALID`, `AUTH_EMAIL_ALREADY_CONFIRMED`
      **There is no `AUTH_EMAIL_NOT_CONFIRMED`** — per DD-01 that state cannot occur.
- [ ] `IPendingRegistrationStore` / `RedisPendingRegistrationStore` — **NEW**
      `Save(payload) → token` (SET `pending:register:{token}`, TTL 24h),
      `Get(token)`, `Delete(token)`.
      The payload holds the **BCrypt hash**, never the raw password (BR-08).
- [ ] `ICertificateStorage` / `BlobCertificateStorage` — **NEW**
      Uploads the teacher certificate at registration time and returns a URL held in the
      Redis payload (BR-05).
- [ ] `ITokenService` / `TokenService` — **REOPENED for DD-02**
  - [ ] Issue access token (JWT, **15 min**, claims: `account_id`, `role`, `email`, `jti`)
  - [ ] Issue refresh token (opaque GUID, **7 days**) → SET `refresh:{token}` = accountId,
        SADD to `session:refresh:{accountId}`
  - [ ] Rotate refresh token — delete the old value on every refresh so it is single-use
  - [ ] Blacklist `jti` (`jwt:blacklist:{jti}`, TTL = the token's remaining life)
- [ ] `IAuthSessionService` — **REOPENED for DD-02**
  - [x] Interface exists under `Services/Auth`
  - [x] `RedisAuthSessionService` — login lockout, blacklist, Student active-session tracking
  - [x] `InMemoryAuthSessionService` — local fallback when Redis is disabled
  - [x] Registered in `IdentityModuleExtensions.cs`
  - [ ] Add `RevokeAllSessions(accountId)` — deletes every refresh token in
        `session:refresh:{accountId}` and blacklists outstanding `jti`s (BR-15)
  - [ ] Rework Student single-session enforcement (BR-02) to delete the previous **refresh**
        token, not merely blacklist an access token

---

## Phase 3: Core Domain Logic

### Registration — writes to Redis only, never to SQL (BR-04, BR-05)

- [ ] `StudentRegisterCommand` — **REWRITTEN for DD-01**
      Validate DTO → check email/username against **confirmed accounts only** → BCrypt-hash
      the password → `IPendingRegistrationStore.Save(...)` → send confirmation email →
      return **202 Accepted**.
      **Assert in code review: this handler performs zero SQL inserts.**
- [ ] `TeacherRegisterCommand` — **REWRITTEN for DD-01**
      As above, plus: upload the certificate to blob storage first (JPG/PNG only, ≤10MB) and
      carry `documents_url` in the Redis payload. **No `TeacherApplication` row is created
      here.**
  - The handler MUST populate `CertificateUploadRequest.SizeInBytes` from
    `IFormFile.Length`. If it is left at 0, the 10MB size gate in `BlobCertificateStorage`
    is silently skipped.
- [ ] `ConfirmEmailCommand` — **UC-93. The only place a self-registered account is born.**
  - [ ] `GET pending:register:{token}` → missing ⇒ 410 `AUTH_TOKEN_EXPIRED`
  - [ ] Re-check uniqueness against `Account` ⇒ conflict ⇒ 409 `AUTH_EMAIL_ALREADY_CONFIRMED`
        (the pending-registration race — two people registered the same email, one confirmed
        first)
  - [ ] In **one transaction**: INSERT `Account` (`is_active = true`) → INSERT `Student`, or
        INSERT `Teacher` + `TeacherApplication` (status `Pending`, `documents_url` from the
        payload)
  - [ ] Publish `AccountCreatedEvent`; for Teacher also `TeacherApplicationSubmittedEvent`
  - [ ] `DEL pending:register:{token}`
  - [ ] Student can log in immediately. Teacher still needs Admin approval (BR-06).

> There is **no** `ResendConfirmationCommand` and no `/auth/resend-confirmation` endpoint.
> A user whose token expired simply registers again — nothing in the database blocks them.

### Login & tokens

- [ ] `LoginCommand` / `LoginCommandHandler` — **REOPENED**. The current handler returns a
      bare `null` for four different failure causes, which the controller flattens into a
      single 401 `AUTH_INVALID_CREDENTIALS`. Rework it to:
  - [ ] Return `AUTH_ACCOUNT_LOCKED` (429) when the failure counter is tripped
  - [ ] Return `AUTH_ACCOUNT_DEACTIVATED` (403) when `is_active = false`
        — **unambiguous under DD-01; no join, no cache lookup, no inference**
  - [ ] For Teacher: load `TeacherApplication` and return `AUTH_APPLICATION_PENDING` (403) or
        `AUTH_APPLICATION_REJECTED` (403, with `review_comments`)
  - [ ] Return `AUTH_INVALID_CREDENTIALS` (401) only for "not found" or "wrong password"
  - [ ] Issue **both** an access token and a refresh token (DD-02)
  - [ ] Enforce Student single-session by deleting the previous refresh token (BR-02)
- [ ] `RefreshTokenCommand` / Handler — **NEW (UC-95, DD-02)**
      Look up `refresh:{token}` → missing ⇒ 401 `AUTH_TOKEN_INVALID`. Re-check `is_active` ⇒
      403 if deactivated. **Rotate**: delete the old refresh token, issue a new one, issue a
      new access token. The old refresh token must fail on reuse.
- [ ] `LogoutCommandHandler` — **REOPENED for DD-02**
  - [x] Blacklists the access-token `jti`
  - [ ] Must **also** delete the refresh token from Redis and remove it from
        `session:refresh:{accountId}` (BR-10)

### Password

- [ ] `ResetPasswordCommand` — UC-06 part 1. Send an email with a reset token
      (`password:reset:{token}`, TTL 15 min). Return a generic response regardless of whether
      the email exists (prevents enumeration).
- [ ] `ConfirmResetPasswordCommand` — **NEW.** UC-06 part 2. Validate the token, hash and
      store the new password, delete the token, and **`RevokeAllSessions(accountId)`** (BR-15).
      Expired ⇒ 410. Invalid ⇒ 401.
- [ ] `ChangePasswordCommand` — verify the current password, hash and update, then
      **`RevokeAllSessions(accountId)`** (BR-15).

### Google OAuth

- [ ] `GoogleOAuthCallbackCommand` — validate the OAuth token, link to an existing confirmed
      account by email or create a new one with `is_active = true` (Google already verified the
      email, so **no** pending-registration record and **no** confirmation email). Issue access
      + refresh tokens. Target < 3s (BR-07).

### Profile

- [ ] `GetProfileQuery` — return the caller's own profile with role
- [ ] `UpdateProfileCommand` — validate email uniqueness if it changed

### Admin

- [ ] `CreateAccountManuallyCommand` — any role. **`is_active = true` immediately, no
      confirmation email** (BR-04). Publish `AccountCreatedEvent`.
- [ ] `ImportFromExcelCommand` — push to `excel_import_queue`. Imported accounts are
      `is_active = true`, no confirmation email. Partial import: valid rows created, invalid
      rows skipped with a per-row error summary.
- [ ] `UpdateAccountCommand` — validate email uniqueness and role validity
- [ ] `ToggleAccountStatusCommand` — Activate / Deactivate. On deactivate, call
      **`RevokeAllSessions(accountId)`** (BR-15). Admin cannot deactivate their own account.
- [ ] `ResolveApplicationCommand` — Approve (`is_verified = true`, status `Approved`) or
      Reject (requires `review_comments`, status `Rejected`). Only `Pending` applications may
      be resolved. Publish `ApplicationResolvedEvent`.
      Note: the account is **already** `is_active = true` at this point — approval unblocks the
      *application status* check in login (BR-06), not the `is_active` flag.
- [ ] `AdjustPermissionCommand` — add / remove permissions for a role, then
      **`RevokeAllSessions`** for every account holding that role, so the change takes effect
      immediately (BR-15).
- [ ] `UpdateRoleCommand` — update a role's name or description. System roles (Admin, Expert,
      Teacher, Student) cannot be renamed.

### Queries

- [ ] `GetAccountListQuery` — paged; filter by role / status / name
- [ ] `GetTeacherApplicationsQuery` — paged; filter by status

### Domain Events

- [ ] `AccountCreatedEvent` — published at email confirmation, manual create, import, and
      OAuth account creation
- [ ] `TeacherApplicationSubmittedEvent` — published at **email confirmation** of a Teacher,
      **not** at registration (the row does not exist until then)
- [ ] `ApplicationResolvedEvent` — published by UC-15

---

## Phase 4: Controllers & Routing

- [ ] `AuthController` — public: login, refresh, register/student, register/teacher,
      confirm-email, google, google/callback, reset-password, confirm-reset-password.
      Access-token required: logout.
      Replace all inline error-code literals with `AuthErrorCodes` constants.
- [ ] `AccountsController` — any authenticated role: profile GET/PUT, change-password
- [ ] `AdminController` — `AdminOnly`: accounts CRUD, import, applications, roles/permissions
- [ ] Apply `[Authorize]` / `[Authorize(Policy = "AdminOnly")]`
- [ ] Register everything in `IdentityModuleExtensions.cs`:
      ```csharp
      services.AddIdentityModule(configuration);
      ```
      DbContext, MediatR handlers, `TokenService`, `EmailService`,
      `RedisPendingRegistrationStore`, `BlobCertificateStorage`, `AuthSessionService`, Redis,
      MassTransit consumers, the cleanup job.

---

## Phase 5: Verification

- [x] `dotnet build` — zero compile errors
- [ ] EF mappings match the current SQL script tables; `dotnet build` succeeds

### DD-01 invariants — test these first, they are the whole point

- [ ] Register a Student → **`SELECT COUNT(*) FROM Account` is unchanged.** Assert directly.
- [ ] Confirm the email → exactly one new `Account` row, `is_active = true`
- [ ] Let the registration token expire (24h) → register again with the same email → **succeeds**
- [ ] No code path anywhere produces an `Account` row for an unverified email

### Login (UC-01, BR-13)

- [ ] Valid credentials → 200 + access token + refresh token + role
- [ ] Access-token claims include `account_id`, `role`, `email`, `jti`
- [ ] Wrong password × 5 → 429 `AUTH_ACCOUNT_LOCKED`
- [ ] `is_active = false` → 403 `AUTH_ACCOUNT_DEACTIVATED`
- [ ] Teacher, application Pending → 403 `AUTH_APPLICATION_PENDING`
- [ ] Teacher, application Rejected → 403 `AUTH_APPLICATION_REJECTED` + `review_comments`
- [ ] Nonexistent user and wrong password both → 401 `AUTH_INVALID_CREDENTIALS` (indistinguishable)
- [ ] Student logs in on a second device → the first device's refresh token is gone (BR-02)

### Tokens (DD-02)

- [ ] Refresh with a valid token → new access token, **and the old refresh token now returns 401**
- [ ] Refresh with a deactivated account → 403 `AUTH_ACCOUNT_DEACTIVATED`
- [ ] Logout → both the access token and the refresh token return 401
- [ ] Change password → every outstanding token for that account returns 401 (BR-15)
- [ ] Access token expires after 15 min; refresh still works until day 7

### Registration & confirmation (UC-39, UC-08, UC-93)

- [ ] Teacher register → certificate is in blob storage; **no** `TeacherApplication` row yet
- [ ] Teacher register with an 11MB JPEG → 400, and no file reaches Cloudinary
- [ ] Teacher register with a GIF or PDF → 400 `UnsupportedCertificateTypeException`
- [ ] Teacher confirm → `TeacherApplication` row exists, status `Pending`
- [ ] Two pending registrations for the same email → the second confirmation returns 409
      `AUTH_EMAIL_ALREADY_CONFIRMED`
- [ ] Confirm with an expired token → 410 `AUTH_TOKEN_EXPIRED`
- [ ] Confirm twice with the same token → the second attempt returns 410 (the key was deleted)
- [ ] Register with an email that belongs to a **confirmed** account → 409 at registration time

### Admin

- [ ] Manual create (UC-11) → `is_active = true`, **no** confirmation email dispatched
- [ ] Excel import (UC-12) → 150 accounts, all `is_active = true`
- [ ] Approve teacher (UC-15) → `is_verified = true`, status `Approved`, Teacher can now log in
- [ ] Deactivate (UC-14) → all sessions revoked; refresh returns 403
- [ ] Adjust permission (UC-16) → sessions of every account holding that role are revoked

---

## Suggested Implementation Order

1. **Phase 1 in full.** Nothing is testable without seeded roles and permissions.
2. **Phase 2 in full.** `AuthErrorCodes`, `IPendingRegistrationStore`, `TokenService`
   (refresh tokens), `RevokeAllSessions`. Everything downstream depends on these.
3. **`LoginCommandHandler` + `RefreshTokenCommand` + `LogoutCommandHandler`.** This is the
   spine; the rest is validated against it.
4. **Registration flow** — `StudentRegisterCommand`, `TeacherRegisterCommand`,
   `ConfirmEmailCommand`, `ICertificateStorage`, the two events.
5. **Password flow** — reset, confirm-reset, change.
6. **Google OAuth.**
7. **Profile.**
8. **Admin** — list, manual create, import, update, toggle status, resolve application.
9. **RBAC** — adjust permission, update role.
10. **Phase 4 controllers**, then **Phase 5 tests**.

Steps 4–9 are independent of one another and can be parallelised across the team once step 3
is merged.