# Tasks Checklist: Identity & Access Module

**Branch**: `001-identity-access` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] Create EF `IEntityTypeConfiguration` for all 8 entities mapped to current DB script tables:
  - [x] `AccountConfiguration` — unique indexes on `username`, `email`; FK to `roles`
  - [x] `RoleConfiguration` — seed 4 roles (Admin, Expert, Teacher, Student) — seeded via `database/002_Seed_MathInsight_Demo.sql`
  - [x] `PermissionConfiguration` — seed permission keys from Permission Matrix — seeded via `database/003_Seed_Permissions.sql`
  - [x] `RolePermissionConfiguration` — composite PK, seed default role-permissions — seeded via `database/003_Seed_Permissions.sql`
  - [x] `StudentConfiguration` — 1:1 with Account
  - [x] `TeacherConfiguration` — 1:1 with Account
  - [x] `ExpertConfiguration` — 1:1 with Account
  - [x] `TeacherApplicationConfiguration` — status enum constraint
- [x] Create `IdentityDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [x] Add seed data per TDS §3.6 (5 accounts + roles)

---

## Phase 2: Core Domain Logic

- [ ] **Auth Commands**:
  - [x] `LoginCommand` / `LoginCommandHandler` — validate credentials, check `is_active`, BCrypt verify, issue JWT, enforce single-session (Redis)
  - [x] `LogoutCommandHandler` — add token to Redis blacklist (`jwt:blacklist:{jti}`)
  - [ ] `StudentRegisterCommand` — create Account + Student, send confirmation email, `is_active = false`
  - [ ] `TeacherRegisterCommand` — create Account + Teacher + TeacherApplication (PENDING), `is_active = false`
  - [ ] `ConfirmEmailCommand` — validate Redis token, set `is_active = true`
  - [ ] `GoogleOAuthCallbackCommand` — create/link Account, issue JWT (< 3s target)
  - [ ] `ResetPasswordCommand` — send email with reset token (Redis TTL 15 min)
  - [ ] `ChangePasswordCommand` — verify current password, hash and update
- [ ] **Auth API Contract**:
  - [x] Standardize auth error responses with stable `code` and developer-facing `message`
  - [x] Ensure login response includes role information for frontend role-based routing
- [ ] **Profile Commands**:
  - [ ] `UpdateProfileCommand` — validate unique email if changed
- [x] **Admin Commands**:
  - [x] `CreateAccountManuallyCommand` — any role, publishes `AccountCreatedEvent`
  - [x] `ImportFromExcelCommand` — implemented synchronously (ClosedXML parse + validate + bulk insert in-request) instead of `excel_import_queue`; per-row success/skip report returned directly
  - [x] `ToggleAccountStatusCommand` — Activate/Deactivate, self-deactivation guard; termination of active sessions enforced via `IsActive` re-check in JWT `OnTokenValidated` (Program.cs) for all roles
  - [x] `ResolveApplicationCommand` — Approve (set `is_verified = true`, `is_active = true`) / Reject (comments required), publishes `ApplicationResolvedEvent`
  - [x] `AdjustPermissionCommand` — replaces a role's permission set; self-admin-permission guard (`identity:admin_access` key)
  - [x] `UpdateRoleCommand` — update role name or description; system-role rename guard
- [ ] **Queries**:
  - [ ] `GetProfileQuery` — return own profile with role
  - [x] `GetAccountListQuery` — paged, filter by role/status/search
  - [x] `GetTeacherApplicationsQuery` — paged, filter by status (+ `GetTeacherApplicationDetailQuery`, `GetRolesQuery` added for UC-10 detail view and UC-16/17 read-side)
- [ ] **Domain Events**:
  - [ ] `TeacherApplicationSubmittedEvent` (MediatR notification) — belongs to UC-08 (teacher self-registration), not yet implemented
  - [x] `AccountCreatedEvent` (MediatR notification) — published, no consumer wired yet (Notification module is out of scope)
  - [x] `ApplicationResolvedEvent` (MediatR notification) — published, no consumer wired yet (Notification module is out of scope)
- [x] **Lock mechanism**: Redis `login:fail:{accountId}` counter with 10-min TTL; lock 15 min on 5 fails
- [ ] **Auth session service**:
  - [x] Create `IAuthSessionService` under `Services/Auth`
  - [x] Create `RedisAuthSessionService` for login lockout, token blacklist, and Student active-session tracking
  - [x] Optionally create `InMemoryAuthSessionService` for local development when Redis is disabled
  - [x] Register the selected implementation in `IdentityModuleExtensions.cs`

---

## Phase 3: Controller and Routing

- [ ] `AuthController` — public routes: login, register, Google, reset, confirm-email; JWT-required route: logout (login/logout only so far)
- [ ] `AccountsController` — secured (any role): profile GET/PUT, change-password
- [x] `AdminController` — AdminOnly: accounts CRUD, import, applications, roles/permissions
- [x] Apply `[Authorize]` / `[Authorize(Policy = "AdminOnly")]` attributes — `AdminOnly` policy registered in `Program.cs`
- [ ] Register all services inside `IdentityModuleExtensions.cs`:
  ```csharp
  services.AddIdentityModule(configuration);
  ```
  Including: DbContext, MediatR handlers, TokenService, EmailService, Redis, MassTransit consumers

---

## Phase 4: Verification

- [x] `dotnet build` — zero compile errors
- [x] EF mappings match the current SQL script tables and `dotnet build` succeeds
- [ ] Integration tests (xUnit) — no test project exists yet for this module; deferred to a follow-up pass:
  - [ ] UC-01: Valid login → 200 + JWT with correct claims
  - [ ] UC-01: Wrong password × 5 → account locked (BR-03)
  - [ ] UC-01: Unconfirmed email login → 401 (BR-04)
  - [ ] UC-02: Logout → subsequent request with same token → 401
  - [ ] UC-07: Google OAuth callback → JWT issued in < 3s
  - [ ] UC-39: Student register → `is_active = false`, email sent
  - [ ] UC-08: Teacher register → TeacherApplication status = PENDING
  - [ ] UC-15: Admin approve → Teacher `is_active = true`, `is_verified = true`
  - [ ] DC-01: Duplicate email register → 409
  - [ ] UC-12: Excel import → 150 accounts created successfully
  - [ ] UC-14: Admin deactivate → active sessions terminated
  - [ ] UC-16: Adjust permission → role permissions updated
