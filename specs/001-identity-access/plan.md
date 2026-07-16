# Implementation Plan: Identity & Access Module

**Branch**: `001-identity-access` | **Date**: 2026-06-23 | **Updated**: 2026-07-14
**Spec**: [spec.md](spec.md)

## Summary

Builds the `MathInsight.Modules.Identity_Access` component handling authentication (local +
Google OAuth), account lifecycle management, role-permission RBAC, and teacher application
verification. Registers with the YARP gateway proxy routing and the DI composition root.

Two decisions from spec.md shape this plan:

- **DD-01 — No unverified accounts in the database.** Self-registration writes **nothing** to
  SQL. The registration payload lives in Redis (`pending:register:{token}`, 24h) until the
  user confirms; only then is the `Account` row inserted, with `is_active = true`. This means
  `is_active = false` has exactly one meaning — deactivated by Admin — and no
  `email_confirmed` column is needed.
- **DD-02 — Refresh tokens in Redis.** Login issues a 15-minute access token (JWT) plus a
  7-day opaque refresh token stored in Redis. Refreshing rotates the token. Revocation means
  deleting the refresh token and blacklisting the access token's `jti`.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client), BCrypt.Net-Next |
| Storage | SQL Server; maps to current DB script tables. **No schema change.** |
| Cache | Redis — pending registrations, refresh tokens, reset tokens, JWT blacklist, login-failure counter |
| Blob storage | Teacher certificate uploads |
| External | Google OAuth 2.0 via `AddGoogle()` |
| Testing | xUnit / integration tests |
| Platform | Windows / Linux (Docker containerized) |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Identity_Access/
├── Commands/
│   ├── Login/                  # LoginCommand + Handler (issues access + refresh token)
│   ├── Logout/                 # LogoutCommand + Handler (delete refresh, blacklist jti)
│   ├── RefreshToken/           # RefreshTokenCommand + Handler (UC-95, rotates the token)
│   ├── Register/               # StudentRegisterCommand, TeacherRegisterCommand
│   │                           #   → write to Redis ONLY, never to SQL
│   ├── ConfirmEmail/           # ConfirmEmailCommand + Handler (UC-93)
│   │                           #   → the ONLY place a self-registered Account row is created
│   ├── ChangePassword/
│   ├── ResetPassword/          # ResetPasswordCommand (send email)
│   ├── ConfirmResetPassword/   # ConfirmResetPasswordCommand (set new password by token)
│   ├── GoogleCallback/         # GoogleOAuthCallbackCommand + Handler
│   ├── ManualCreateAccount/    # Admin: CreateAccountManuallyCommand
│   ├── ImportAccounts/         # Admin: ImportFromExcelCommand (MassTransit queue)
│   ├── UpdateAccount/
│   ├── ToggleAccountStatus/    # Activate / Deactivate
│   ├── ResolveApplication/     # Approve / Reject TeacherApplication
│   ├── AdjustPermission/
│   └── UpdateRole/
├── Queries/
│   ├── GetProfile/
│   ├── GetAccountList/         # Admin: paged account list
│   └── GetTeacherApplications/ # Admin: paged application list
├── Events/
│   ├── TeacherApplicationSubmittedEvent.cs
│   ├── AccountCreatedEvent.cs
│   └── ApplicationResolvedEvent.cs
├── Persistence/
│   ├── IdentityDbContext.cs    # shared connection; explicit ToTable(...) mappings
│   ├── Configurations/
│   │   ├── AccountConfiguration.cs
│   │   ├── RoleConfiguration.cs
│   │   ├── PermissionConfiguration.cs
│   │   ├── RolePermissionConfiguration.cs
│   │   ├── StudentConfiguration.cs
│   │   ├── TeacherConfiguration.cs
│   │   ├── ExpertConfiguration.cs
│   │   └── TeacherApplicationConfiguration.cs
├── Controllers/
│   ├── AuthController.cs
│   ├── AccountsController.cs
│   └── AdminController.cs
├── Services/
│   ├── ITokenService.cs / TokenService.cs
│   │     # issue access token (JWT, 15m), issue + rotate refresh token (7d),
│   │     # blacklist jti, validate refresh token
│   ├── IEmailService.cs / EmailService.cs
│   ├── ICertificateStorage.cs / BlobCertificateStorage.cs
│   ├── IPendingRegistrationStore.cs / RedisPendingRegistrationStore.cs
│   │     # Save(payload) -> token; Get(token); Delete(token)
│   └── Auth/
│       ├── IAuthSessionService.cs
│       │     # login lockout; refresh-token set per account; blacklist;
│       │     # RevokeAllSessions(accountId)
│       ├── RedisAuthSessionService.cs
│       └── InMemoryAuthSessionService.cs   # local fallback when Redis is disabled
├── Contracts/
│   └── AuthErrorCodes.cs       # const strings — no more inline literals
└── IdentityModuleExtensions.cs # AddIdentityModule() DI registration
```

## Proposed Changes

### Database Layer — no schema change

| Table | Notes |
|-------|-------|
| `Account` | Core credential + profile table. Every row is email-verified by construction (DD-01). |
| `Role` | 4 seeded roles: Admin, Expert, Teacher, Student |
| `Permission` | Permission keys (e.g. `lecture:publish`, `test:generate`) |
| `RolePermission` | Composite PK junction table |
| `Student` | 1:1 with Account |
| `Teacher` | 1:1 with Account |
| `Expert` | 1:1 with Account |
| `TeacherApplication` | Row created at email confirmation, not at registration (BR-05) |

No columns are added. No tables are added. `email_confirmed` is **not** needed: an
unverified account has no row to describe.

### Redis Key Layout

| Key | Value | TTL | Purpose |
|-----|-------|-----|---------|
| `pending:register:{token}` | JSON payload: username, email, `password_hash`, role, grade/school (Student) or biography/`documents_url` (Teacher) | 24h | BR-04 — registration awaiting confirmation |
| `password:reset:{token}` | `accountId` | 15m | UC-06 |
| `refresh:{token}` | `accountId` | 7d | BR-09 — refresh token |
| `session:refresh:{accountId}` | Redis SET of that account's active refresh tokens | 7d | BR-02 (Student single session), BR-15 (revoke all) |
| `jwt:blacklist:{jti}` | `1` | remaining access-token life | BR-10 |
| `login:fail:{accountId}` | counter | 10m | BR-03 |

The pending-registration payload stores the **BCrypt hash**, never the raw password (BR-08).

### Service & API Gateway — REST Endpoints

**Auth (public unless noted)**
```
POST   /api/v1/auth/login                    # UC-01 → access token + refresh token + role
POST   /api/v1/auth/refresh                  # UC-95 → new access token, rotates refresh token
POST   /api/v1/auth/logout                   # UC-02; access token required.
                                             #   Deletes refresh token, blacklists jti
POST   /api/v1/auth/register/student         # UC-39 → writes to Redis ONLY. No DB row.
POST   /api/v1/auth/register/teacher         # UC-08 → uploads certificate to blob,
                                             #   writes to Redis ONLY. No DB row.
POST   /api/v1/auth/confirm-email            # UC-93 → THE point where the Account row
                                             #   is created, is_active = true
POST   /api/v1/auth/google                   # UC-07 initiate
GET    /api/v1/auth/google/callback          # UC-07 callback → tokens
POST   /api/v1/auth/reset-password           # UC-06 part 1: send email with reset token
POST   /api/v1/auth/confirm-reset-password   # UC-06 part 2: set new password by token
```

There is **no** `/auth/resend-confirmation`. Per DD-01, a user whose registration token
expired just registers again — nothing exists in the database to collide with.

### Registration Flow (UC-39 / UC-08, implementing BR-04 and BR-05)

```
POST /auth/register/student
  1. Validate DTO (BR-08 password strength)
  2. SELECT Account WHERE email = ? OR username = ?
       → found  → 409 AUTH_... (a CONFIRMED account already owns it)
       → absent → continue
  3. Hash the password (BCrypt, strength 12)
  4. token = GUID
     SET pending:register:{token} = { username, email, password_hash, role: Student,
                                      grade, school }  EX 86400
  5. Send confirmation email containing {token}
  6. 202 Accepted — "Check your email"
     >>> NOTHING has been written to SQL <<<

POST /auth/register/teacher
  Same as above, plus between steps 3 and 4:
    3b. Upload the certificate to blob storage → documents_url
        (JPEG/PNG only, ≤10MB; held in the Redis payload; the TeacherApplication row
         does not exist yet)
```

### Email Confirmation Flow (UC-93, implementing BR-04 step 5)

```
POST /auth/confirm-email  { token }
  1. GET pending:register:{token}
       → missing  → 410 AUTH_TOKEN_EXPIRED   (24h elapsed, or already used)
  2. Re-check uniqueness:
     SELECT Account WHERE email = ? OR username = ?
       → found → 409 AUTH_EMAIL_ALREADY_CONFIRMED
                 (the pending-registration race: someone else confirmed first)
  3. BEGIN TRANSACTION
       INSERT Account (..., is_active = TRUE)      ← the ONLY insert point
       IF role = Student  → INSERT Student
       IF role = Teacher  → INSERT Teacher (is_verified = false)
                            INSERT TeacherApplication (status = Pending,
                                                       documents_url from payload)
                            publish TeacherApplicationSubmittedEvent
       publish AccountCreatedEvent
     COMMIT
  4. DEL pending:register:{token}
  5. 200 OK — Student may now log in.
             Teacher must still wait for Admin approval (BR-06).
```

### Login Handler Flow (UC-01, implementing BR-13, BR-02, BR-09)

```
POST /auth/login
  1. Validate DTO (BR-01)
  2. Check login:fail:{accountId} → locked? → 429 AUTH_ACCOUNT_LOCKED
  3. SELECT Account WHERE username = ? OR email = ?
       → not found → increment login:fail → 401 AUTH_INVALID_CREDENTIALS
  4. BCrypt.Verify(password, account.password_hash)
       → false → increment login:fail → 401 AUTH_INVALID_CREDENTIALS
  5. IF is_active = false → 403 AUTH_ACCOUNT_DEACTIVATED
       (unambiguous: this can ONLY mean Admin deactivation — DD-01)
  6. IF role = Teacher:
       load TeacherApplication
         status = Pending  → 403 AUTH_APPLICATION_PENDING
         status = Rejected → 403 AUTH_APPLICATION_REJECTED + review_comments
         status = Approved → continue
  7. IF role = Student:                                    (BR-02)
       for each token in session:refresh:{accountId}:
         DEL refresh:{token}
       clear session:refresh:{accountId}
       blacklist the previous access-token jti if one is tracked
  8. Issue access token  (JWT, 15m: account_id, role, email, jti)
     Issue refresh token (GUID, 7d)
       SET  refresh:{refreshToken} = accountId  EX 604800
       SADD session:refresh:{accountId} {refreshToken}
  9. DEL login:fail:{accountId}
 10. 200 OK { accessToken, refreshToken, role, expiresIn }
```

Note that step 5 needs no join, no cache lookup, and no inference. That is the whole point
of DD-01.

### Refresh Flow (UC-95, implementing BR-09)

```
POST /auth/refresh  { refreshToken }
  1. GET refresh:{refreshToken} → accountId
       → missing → 401 AUTH_TOKEN_INVALID  (expired, revoked, or already rotated)
  2. Re-load the account. If is_active = false → 403 AUTH_ACCOUNT_DEACTIVATED
       (a deactivated user must not be able to refresh their way back in)
  3. ROTATE:
       DEL  refresh:{refreshToken}
       SREM session:refresh:{accountId} {refreshToken}
       issue a new refresh token; SET + SADD it
  4. Issue a new access token
  5. 200 OK { accessToken, refreshToken, expiresIn }
```

Rotation means a stolen refresh token is single-use — reuse of the old value returns 401.

### Session Revocation (BR-15)

`IAuthSessionService.RevokeAllSessions(accountId)`:

```
for each token in session:refresh:{accountId}:
    DEL refresh:{token}
DEL session:refresh:{accountId}
blacklist any outstanding access-token jti for this account
```

Called by: logout (that session only), change password, confirm password reset, deactivate
account (UC-14), adjust role permissions (UC-16 — for every account holding that role).

### Auth API Contract

Auth endpoints return stable error codes for frontend localization. The `message` field is
developer-facing and may remain English; Vietnamese user-facing text is handled by the
frontend.

```json
{
  "code": "AUTH_APPLICATION_PENDING",
  "message": "Teacher application is awaiting Admin review."
}
```

Error codes are declared as constants in `Contracts/AuthErrorCodes.cs` — the current inline
string literals in `AuthController` must be replaced.

Backend exposes one role-agnostic login endpoint. Successful login responses include the
authenticated user's role so the frontend can route to the Student, Teacher, Expert, or Admin
dashboard.

The client stores **both** tokens returned by login/refresh (the access token and the refresh
token) and must send the refresh token in the **logout request body** — logout requires it and
returns 400 without it (BR-10).

**Account Self-Service (access token required, any role)**
```
GET    /api/v1/accounts/profile              # UC-04
PUT    /api/v1/accounts/profile              # UC-05
POST   /api/v1/accounts/change-password      # UC-03 — revokes all sessions (BR-15)
```

**Admin Management (AdminOnly policy)**
```
GET    /api/v1/admin/accounts                    # UC-09: paged, filter by role/status
POST   /api/v1/admin/accounts/manual             # UC-11: is_active = true, no email
POST   /api/v1/admin/accounts/import             # UC-12: is_active = true, no email
PUT    /api/v1/admin/accounts/{id}               # UC-13
PUT    /api/v1/admin/accounts/{id}/status        # UC-14 — revokes all sessions (BR-15)
GET    /api/v1/admin/applications                # UC-10: paged
POST   /api/v1/admin/applications/{id}/resolve   # UC-15: approve / reject
PUT    /api/v1/admin/roles/{roleId}/permissions  # UC-16 — revokes sessions of that role
PUT    /api/v1/admin/roles/{roleId}              # UC-17
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `AccountCreatedEvent` | Identity (at email confirmation, manual create, import, OAuth) | Notification | Welcome email |
| `TeacherApplicationSubmittedEvent` | Identity (at email confirmation of a Teacher) | Notification | Notify Admin of a new application |
| `ApplicationResolvedEvent` | Identity (UC-15) | Notification | Notify the Teacher of approval / rejection |

Note the timing shift from earlier drafts: `TeacherApplicationSubmittedEvent` fires at
**confirmation**, not at registration, because that is when the `TeacherApplication` row comes
into existence.

### Cross-Module Dependencies

- **Notification module** consumes `AccountCreatedEvent`, `TeacherApplicationSubmittedEvent`,
  `ApplicationResolvedEvent` via MassTransit.
- **Redis** is the sole store for all six transient key patterns above. Losing Redis loses
  in-flight registrations and forces re-login; it does **not** corrupt persisted account state.
- **Blob storage** holds teacher certificates.
- **MassTransit queue** `excel_import_queue` handles async bulk import.

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point at the current SQL script tables. Do **not** add an EF migration unless
   the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:

   **DD-01 invariants**
   - Register a Student → **zero rows** in `Account`. Assert the count directly.
   - Confirm the email → exactly one `Account` row, `is_active = true`.
   - Let the registration token expire → register again with the same email → succeeds.
   - No code path produces an `Account` row with an unverified email.

   **Login**
   - Valid credentials → 200 + access token + refresh token + role.
   - Wrong password × 5 → 429 `AUTH_ACCOUNT_LOCKED`.
   - `is_active = false` → 403 `AUTH_ACCOUNT_DEACTIVATED`.
   - Teacher, application Pending → 403 `AUTH_APPLICATION_PENDING`.
   - Teacher, application Rejected → 403 `AUTH_APPLICATION_REJECTED` + `review_comments`.
   - Student logs in on a second device → the first device's refresh token is gone (BR-02).

   **Tokens (DD-02)**
   - Refresh with a valid token → new access token; the **old refresh token now returns 401**.
   - Refresh with a deactivated account → 403.
   - Logout → both the access token and the refresh token return 401.
   - Change password → every outstanding token for that account returns 401 (BR-15).

   **Registration & confirmation**
   - Teacher register → certificate is in blob storage, **no** `TeacherApplication` row yet.
   - Teacher confirm → `TeacherApplication` row exists with status Pending.
   - Two pending registrations for the same email → the second confirmation returns 409
     `AUTH_EMAIL_ALREADY_CONFIRMED`.
   - Confirm with an expired token → 410 `AUTH_TOKEN_EXPIRED`.

   **Admin**
   - Manual create → `is_active = true`, no confirmation email dispatched.
   - Approve teacher → `is_active = true`, `is_verified = true`, status Approved.
   - Deactivate → all sessions revoked.
   - Excel import → 150 accounts, all `is_active = true`.
   - Adjust permission → sessions of accounts holding that role are revoked.