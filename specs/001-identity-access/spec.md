# Feature Specification: Identity & Access Module

**Feature Branch**: `001-identity-access`

**Created**: 2026-06-23 | **Updated**: 2026-07-14

**Status**: Approved

**Source Documents**: PRD §3, §4 (FT-01), UCS UC-01–UC-17, UC-39, TDS §3 (accounts, roles, permissions), §4

---

## Design Decisions (2026-07-14)

Two decisions taken by the team. Both supersede earlier drafts of this spec.

### DD-01: No unverified accounts in the database

An `Account` row is **only created after the email is verified**. Registration data is held
in Redis (`pending:register:{token}`) until the user clicks the confirmation link; only then
is the row inserted, with `is_active = true`.

**Rationale.** The `Account` table stores a single boolean `is_active`. If unverified
accounts were persisted with `is_active = false`, that flag would carry two incompatible
meanings — *"has not confirmed email"* and *"deactivated by Admin"* — with no way to tell
them apart. Every mechanism for rescuing a user whose confirmation token expired
(auto-resend, or allowing re-registration over the stale row) requires distinguishing those
two states, and getting it wrong lets an Admin-deactivated user re-activate themselves.
Rather than add an `email_confirmed` column, the team chose to remove the ambiguous state
entirely.

**Consequence.** `is_active = false` now has exactly **one** meaning: deactivated by Admin
(UC-14). No new column is required. No resend endpoint is required — a user whose token
expired simply registers again, because nothing exists in the database to collide with.

This supersedes UC-39 step 4 (*"System creates a new Student account in an unverified
state"*) and the equivalent step in UC-08. See `/docs` for the derivation note.

### DD-02: Refresh tokens, stored in Redis

Login issues a short-lived **access token** plus a longer-lived **refresh token**. Both are
tracked in Redis. Revoking a session means deleting the refresh token and blacklisting the
access token's `jti`.

---

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor |
|-------|------|---------------|
| UC-01 | Login | User (all roles) |
| UC-02 | Logout | User (all roles) |
| UC-03 | Change Password | User (all roles) |
| UC-04 | View Profile | User (all roles) |
| UC-05 | Update Profile | User (all roles) |
| UC-06 | Reset Password | User (all roles) |
| UC-07 | Login by Google | User (all roles) |
| UC-08 | Register Teacher Account | Guest |
| UC-09 | View Account List | Admin |
| UC-10 | View Teacher Application | Admin |
| UC-11 | Create Account Manually | Admin |
| UC-12 | Import from Excel | Admin |
| UC-13 | Update Account | Admin |
| UC-14 | Activate / Deactivate Account | Admin |
| UC-15 | Approve / Reject Application | Admin |
| UC-16 | Adjust Permission | Admin |
| UC-17 | Update Role | Admin |
| UC-39 | Student Register Account | Guest |
| UC-93 | Verify Email Address | Guest |
| UC-95 | Refresh Access Token | User (all roles) |

> **UC-93** and **UC-95** are derived use cases not present in the original UCS document.
> UC-93 completes the self-registration flow (UC-39, UC-08), whose postconditions state that
> a verification email is sent but which never specify the confirmation step. UC-95 follows
> from DD-02. Both are recorded here for traceability; see `/docs`.

### Edge Cases

- **Duplicate registration**: email or username already belongs to a **confirmed** account
  → HTTP 409 Conflict at registration time.
- **Two pending registrations for the same email**: both are held in Redis; neither is
  blocked at registration time. Whoever confirms first wins; the second confirmation attempt
  fails with 409 `AUTH_EMAIL_ALREADY_CONFIRMED`. This is an accepted trade-off of DD-01.
- **Expired registration token**: `pending:register:{token}` expired (24h) → the user simply
  registers again. No Admin intervention, no resend endpoint. Any uploaded teacher
  certificate becomes an orphaned file that is not cleaned up (see Known Limitations).
- **Empty fields**: DTO validation via `[Required]`, `[MaxLength]` → HTTP 400.
- **Google OAuth conflict**: Google email matches an existing confirmed account → link
  accounts, do not create a duplicate.
- **Admin unlocks locked account**: resets the failed-login counter immediately.
- **Refresh token reuse after logout**: the refresh token was deleted from Redis → HTTP 401
  `AUTH_TOKEN_INVALID`. The client must log in again.

---

## Requirements *(mandatory)*

### Functional Requirements

- **DC-01**: Username and Email must be unique across all **confirmed** accounts. A
  registration whose email or username collides with a confirmed account returns HTTP 409.

- **BR-01**: Both Username and Password fields are required before login submission.

- **BR-02**: A Student account may not be logged in on multiple devices simultaneously. A
  new login deletes the previous refresh token and blacklists the previous access token's
  `jti`.

- **BR-03**: An account is locked for **15 minutes** after **5 consecutive failed login
  attempts** within a **10-minute** window. The failure message is generic — it does not
  reveal which field was wrong.

- **BR-04** *(supersedes the earlier version — see DD-01)*: **Self-registration does not
  create an account row.** For Student (UC-39) and Teacher (UC-08):

  1. The system validates the input and checks the email/username against **confirmed
     accounts only**.
  2. The system hashes the password and stores the full registration payload in Redis under
     `pending:register:{token}` (TTL 24 hours).
  3. The system sends a confirmation email containing the token.
  4. **No row is written to `Account`, `Student`, `Teacher`, or `TeacherApplication` at this
     point.**
  5. On confirmation (UC-93), the system reads the payload from Redis, inserts the `Account`
     row with **`is_active = true`**, inserts the role-specific row (and, for Teacher, the
     `TeacherApplication` row with status `Pending`), then deletes the Redis key.

  Expert does **not** self-register. Accounts created by Admin (UC-11, UC-12) and via Google
  OAuth (UC-07) are inserted directly with `is_active = true` and receive no confirmation
  email.

- **BR-05**: For Teacher self-registration (UC-08), the certificate file is uploaded to blob
  storage **at registration time** and its URL is held in the Redis payload. The certificate
  must be a **JPEG or PNG image no larger than 10 MB**; any other content type or an oversized
  file is rejected at registration time (HTTP 400). PDF is **not** supported. The
  `TeacherApplication` row is created only at confirmation. If the token expires unconfirmed,
  the uploaded file simply remains in blob storage as an orphaned file. No cleanup job runs.

- **BR-06**: A confirmed Teacher account has `is_active = true` but
  `TeacherApplication.status = Pending`, and **cannot log in** until Admin approves (UC-15).
  Login therefore checks application status independently of `is_active`.

- **BR-07**: Google OAuth callback must complete and redirect within **3 seconds**
  (NFR-AC-FT01-02). Accounts created via Google OAuth are inserted with `is_active = true` —
  the email is already verified by Google, so no confirmation email is sent and no
  pending-registration record is created.

- **BR-08**: Password must be minimum **8 characters**, maximum **128 characters**, and
  include at least one uppercase letter, one lowercase letter, one number, and one special
  character. Stored as a BCrypt hash (strength 12). Plaintext storage is strictly prohibited
  — including inside the Redis pending-registration payload, which stores the **hash**, never
  the raw password.

- **BR-09** *(supersedes the earlier version — see DD-02)*: Login issues **two** tokens:

  | Token | Lifetime | Contents | Storage |
  |-------|----------|----------|---------|
  | Access token (JWT) | **15 minutes** | `account_id`, `role`, `email`, `jti` | Stateless; revoked via blacklist |
  | Refresh token | **7 days** | Opaque GUID | Redis `refresh:{token}` → `accountId` |

  The access token is validated by the YARP Gateway on every request. The refresh token is
  presented only to `POST /api/v1/auth/refresh` (UC-95), which returns a new access token and
  **rotates** the refresh token — the old one is deleted immediately, so a refresh token can
  never be used twice.

- **BR-10**: On logout (UC-02), the client **must supply the refresh token** in the request
  body; a logout without it returns **HTTP 400**. The system then deletes that refresh token
  from Redis **and** blacklists the access token's `jti` (`jwt:blacklist:{jti}`, TTL = the
  token's remaining lifetime). Reusing either token afterwards returns 401. The refresh token is
  required because it is the only value that identifies which session to end; without it the
  refresh token would survive its full 7-day lifetime and the session would not actually end.

  Logout **does not require a valid access token**. The endpoint is not `[Authorize]`-protected:
  the refresh token in the body is what identifies the session, so it is looked up in Redis to
  resolve the `accountId` and delete that refresh token. This is deliberate — an expired access
  token is exactly when a user needs to end their session cleanly; requiring a valid one would
  leave the refresh token alive in Redis for its full 7-day lifetime. The access token's `jti` is
  still blacklisted **when it can be read** from the (possibly expired) bearer token, which is
  decoded without signature/lifetime validation for this purpose only. An unknown or already-
  revoked refresh token is a **no-op that still returns success** (idempotent logout — the
  response never reveals whether the session existed).

- **BR-11**: Authentication APIs must return stable machine-readable `code` values in
  addition to developer-facing `message` text. Frontend clients localize these codes into
  Vietnamese.

- **BR-12**: Login is a single role-agnostic endpoint (`POST /api/v1/auth/login`). Frontend
  clients may provide role-specific login pages, but the backend returns the authenticated
  user's role for client-side routing.

- **BR-13** *(rewritten — see DD-01)*: Because unverified accounts never reach the database,
  a failed login has exactly these causes:

  | Condition | Error Code | HTTP |
  |-----------|-----------|------|
  | Username/email not found, or password wrong | `AUTH_INVALID_CREDENTIALS` | 401 |
  | Too many failed attempts (BR-03) | `AUTH_ACCOUNT_LOCKED` | 429 |
  | `is_active = false` | `AUTH_ACCOUNT_DEACTIVATED` | 403 |
  | Role = Teacher, `TeacherApplication.status = Pending` | `AUTH_APPLICATION_PENDING` | 403 |
  | Role = Teacher, `TeacherApplication.status = Rejected` | `AUTH_APPLICATION_REJECTED` | 403 (includes `review_comments`) |

  `is_active = false` is **unambiguous**: it means the account was deactivated by an Admin
  (UC-14). No new column is required, and no inference from cache state is performed.

- **BR-14** *(rewritten — see DD-01)*: Token lifetimes:

  | Redis key | TTL |
  |-----------|-----|
  | `pending:register:{token}` | 24 hours |
  | `password:reset:{token}` | 15 minutes (per UCS UC-06) |
  | `refresh:{token}` | 7 days |
  | `jwt:blacklist:{jti}` | remaining access-token lifetime |
  | `login:fail:{accountId}` | 10 minutes |

  **There is no resend endpoint and no auto-resend.** A user whose registration token expired
  registers again from scratch.

- **BR-15**: Changing a password (UC-03), completing a password reset (UC-06), deactivating
  an account (UC-14), and adjusting a role's permissions (UC-16) must all **revoke every
  active session** for the affected account(s): delete the refresh token(s) from Redis and
  blacklist the outstanding access-token `jti`(s).

---

### Key Entities *(include if feature involves data)*

**No schema changes.** The module maps to the existing SQL script tables.

- **Account**: `account_id` (VARCHAR 36, PK), `username` (VARCHAR 50, UNIQUE),
  `password_hash` (VARCHAR 255), `email` (VARCHAR 100, UNIQUE), `first_name`, `last_name`,
  `phone_number`, `date_of_birth`, `avatar_url`, `role_id` (FK → roles), `is_active`
  (BOOLEAN), `created_time`
- **Expert**: `expert_id` (PK, FK → accounts), `specialty`
- **Student**: `student_id` (PK, FK → accounts), `gender`, `school`, `current_grade`
- **Teacher**: `teacher_id` (PK, FK → accounts), `biography`, `is_verified` (BOOLEAN)
- **TeacherApplication**: `application_id`, `teacher_id` (FK), `documents_url`, `status`
  (`Pending` → `Approved` | `Rejected`), `review_comments`, `applied_time`, `reviewed_time`,
  `reviewed_by` (FK → accounts)
- **Role**: `role_id`, `role_name` (UNIQUE), `description`
- **Permission**: `permission_id`, `permission_key` (UNIQUE), `description`
- **RolePermission**: `role_id` (FK), `permission_id` (FK) — composite PK

> Every row that reaches `Account` is already email-verified, so application logic always
> inserts `is_active = true`. The column default in the SQL script is left untouched; the
> insert sets the value explicitly.

### Transient State (Redis — not persisted)

| Key | Value | TTL | Purpose |
|-----|-------|-----|---------|
| `pending:register:{token}` | JSON registration payload (incl. `password_hash`; for Teacher also `documents_url`) | 24h | Registration awaiting email confirmation (BR-04) |
| `password:reset:{token}` | `accountId` | 15m | Password reset (UC-06) |
| `refresh:{token}` | `accountId` | 7d | Refresh token (BR-09) |
| `session:refresh:{accountId}` | set of that account's active refresh tokens | 7d | Enables BR-02 (Student single session) and BR-15 (revoke all sessions) |
| `jwt:blacklist:{jti}` | `1` | remaining access-token life | Revoked access token (BR-10) |
| `login:fail:{accountId}` | counter | 10m | Failed-attempt counter (BR-03) |

### Enums & Lookup Values

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| TeacherApplication | status | `Pending`, `Approved`, `Rejected` |
| Account | role_name | `Admin`, `Expert`, `Teacher`, `Student` |

### Account Creation Paths

| Path | Row written at | Initial `is_active` | Confirmation email? | Can log in when |
|------|---------------|--------------------|--------------------|-----------------|
| Student self-register (UC-39) | **email confirmation** (UC-93) | `true` | Yes | Immediately after confirming |
| Teacher self-register (UC-08) | **email confirmation** (UC-93) | `true` | Yes | After confirming **and** Admin approves (UC-15) |
| Google OAuth, new user (UC-07) | OAuth callback | `true` | No | Immediately |
| Admin creates manually (UC-11) | on create | `true` | No | Immediately |
| Admin bulk import (UC-12) | on import | `true` | No | Immediately |

### Auth Error Codes

| Code | HTTP | Meaning |
|------|------|---------|
| `AUTH_INVALID_CREDENTIALS` | 401 | Username/email or password wrong (generic, per BR-03) |
| `AUTH_ACCOUNT_LOCKED` | 429 | Too many failed attempts (BR-03) |
| `AUTH_ACCOUNT_DEACTIVATED` | 403 | Account deactivated by Admin (UC-14) |
| `AUTH_APPLICATION_PENDING` | 403 | Teacher application awaiting Admin review |
| `AUTH_APPLICATION_REJECTED` | 403 | Teacher application rejected; returns `review_comments` |
| `AUTH_TOKEN_EXPIRED` | 410 | Registration or password-reset token expired |
| `AUTH_TOKEN_INVALID` | 401 | Token malformed, already used, or revoked |
| `AUTH_EMAIL_ALREADY_CONFIRMED` | 409 | The email or username already belongs to a confirmed account. Returned in **two** places: at **registration time** (DC-01, the identity is already taken) and at **confirmation time** when another registration confirmed the same email first (the pending-registration race — see Edge Cases) |
| `AUTH_CERTIFICATE_INVALID` | 400 | Teacher certificate rejected: not a JPG/PNG, or exceeds 10 MB (BR-05) |

> There is **no** `AUTH_EMAIL_NOT_CONFIRMED`. That state cannot occur: an unverified account
> does not exist in the database, so it cannot attempt to log in.

### Permission Matrix (from PRD §3.2)

| Action | Admin | Expert | Teacher | Student | Guest |
|--------|-------|--------|---------|---------|-------|
| Login / Logout | Full | Full | Full | Full | No |
| Register Account | No | No | Full | Full | Full |
| Verify Teacher Credentials | Full | No | No | No | No |
| Deactivate Account | Full | No | No | No | No |
| Import Batch Accounts | Full | No | No | No | No |
| Adjust Permissions | Full | No | No | No | No |

### Known Limitations

- **Orphaned teacher certificates accumulate.** When a Teacher self-registration token expires
  unconfirmed (BR-05), the certificate uploaded at registration time remains in blob storage with
  no corresponding `TeacherApplication` row. The MVP does **not** clean these up — there is no
  scheduled job to remove them, so orphaned certificate files grow over time.

- **In-memory pending registrations do not survive a restart.** When `Redis:Enabled = false`
  (local development only), pending registrations are held in an in-memory store instead of Redis.
  Restarting the process loses every in-flight registration, so any unconfirmed user must register
  again. Production runs with Redis enabled and is unaffected.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All auth API endpoints return successful responses within **2 seconds** (NFR-P01).
- Google OAuth callback completes within **3 seconds** (NFR-AC-FT01-02).
- Access-token validation at the YARP layer adds < 10ms overhead.
- **No `Account` row ever exists with an unconfirmed email.** By construction, a query for
  such accounts returns zero rows.
- **`is_active = false` implies exactly one thing**: deactivated by Admin. Verified by
  integration test.
- A user whose registration token expired can recover **without Admin intervention**, simply
  by registering again.
- Logout, password change, password reset, account deactivation, and permission adjustment
  each invalidate all outstanding access **and** refresh tokens for the affected account.
- A rotated refresh token cannot be reused.
- The backend maps Identity entities to the current SQL script tables. **This module
  introduces no schema change.**
- All seed accounts (`admin`, `expert_01`, `teacher_01`, `student_01`, `student_02`) are
  testable end-to-end.

---

## Assumptions

- Target database is SQL Server. The backend maps to the current DB script tables
  (`Account`, `Role`, `Permission`, `RolePermission`, `Student`, `Teacher`, `Expert`,
  `TeacherApplication`). No schema-prefixed tables, no new columns, no new tables.
- Redis is available and is the **sole** store for all transient auth state (pending
  registrations, refresh tokens, reset tokens, blacklist, counters). Losing Redis loses
  in-flight registrations and forces every user to log in again — an accepted trade-off. It
  does **not** corrupt any persisted account state.
- Blob storage is available for teacher certificate uploads.
- MediatR provides decoupled in-process event handling; MassTransit provides cross-module
  integration.
- Google OAuth 2.0 credentials are injected via environment variables
  (`GoogleOAuth:ClientId`, `GoogleOAuth:ClientSecret`).
- BCrypt strength 12 is used for password hashing.
- Confirmation and password-reset emails are sent over SMTP (MailKit). SMTP credentials
  (`Smtp:Username`, `Smtp:Password`) are **never committed** — `appsettings.json` holds empty
  placeholders, and real values are supplied via **user secrets or environment variables**.
  When SMTP is disabled or unconfigured (`Smtp:Enabled = false` or an empty `Smtp:Host`), the
  app falls back to logging the confirmation link (local development), logging a warning at
  startup. Emails contain a clickable link (`{FrontendBaseUrl}/confirm-email?token=…`), not a
  bare token.