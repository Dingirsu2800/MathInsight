# Tasks Checklist: Identity & Access Module

**Branch**: `001-identity-access` | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for all 8 entities under `usr` schema:
  - [ ] `AccountConfiguration` — unique indexes on `username`, `email`; FK to `roles`
  - [ ] `RoleConfiguration` — seed 4 roles (Admin, Expert, Teacher, Student)
  - [ ] `PermissionConfiguration` — seed permission keys from Permission Matrix
  - [ ] `RolePermissionConfiguration` — composite PK, seed default role-permissions
  - [ ] `StudentConfiguration` — 1:1 with Account
  - [ ] `TeacherConfiguration` — 1:1 with Account
  - [ ] `ExpertConfiguration` — 1:1 with Account
  - [ ] `TeacherApplicationConfiguration` — status enum constraint
- [ ] Create `IdentityDbContext.cs` with shared connection, `usr` schema default
- [ ] Add EF migration: `dotnet ef migrations add Init_Identity --project MathInsight.WebAPI`
- [ ] Add seed data per TDS §3.6 (5 accounts + roles)

---

## Phase 2: Core Domain Logic

- [ ] **Auth Commands**:
  - [ ] `LoginCommand` / `LoginCommandHandler` — validate credentials, check `is_active`, BCrypt verify, issue JWT, enforce single-session (Redis)
  - [ ] `LogoutCommandHandler` — add token to Redis blacklist (`jwt:blacklist:{jti}`)
  - [ ] `StudentRegisterCommand` — create Account + Student, send confirmation email, `is_active = false`
  - [ ] `TeacherRegisterCommand` — create Account + Teacher + TeacherApplication (PENDING), `is_active = false`
  - [ ] `ConfirmEmailCommand` — validate Redis token, set `is_active = true`
  - [ ] `GoogleOAuthCallbackCommand` — create/link Account, issue JWT (< 3s target)
  - [ ] `ResetPasswordCommand` — send email with reset token (Redis TTL 15 min)
  - [ ] `ChangePasswordCommand` — verify current password, hash and update
- [ ] **Profile Commands**:
  - [ ] `UpdateProfileCommand` — validate unique email if changed
- [ ] **Admin Commands**:
  - [ ] `CreateAccountManuallyCommand` — any role, send welcome notification
  - [ ] `ImportFromExcelCommand` — push to `excel_import_queue`, validate format
  - [ ] `ToggleAccountStatusCommand` — Activate/Deactivate, terminate active sessions
  - [ ] `ResolveApplicationCommand` — Approve (set `is_verified = true`, `is_active = true`) / Reject
  - [ ] `AdjustPermissionCommand` — add/remove permissions for a role
  - [ ] `UpdateRoleCommand` — update role name or description
- [ ] **Queries**:
  - [ ] `GetProfileQuery` — return own profile with role
  - [ ] `GetAccountListQuery` — paged, filter by role/status/name
  - [ ] `GetTeacherApplicationsQuery` — paged, filter by status
- [ ] **Domain Events**:
  - [ ] `TeacherApplicationSubmittedEvent` (MediatR notification)
  - [ ] `AccountCreatedEvent` (MediatR notification)
  - [ ] `ApplicationResolvedEvent` (MediatR notification)
- [ ] **Lock mechanism**: Redis `login:fail:{accountId}` counter with 10-min TTL; lock 15 min on 5 fails

---

## Phase 3: Controller and Routing

- [ ] `AuthController` — public routes (no JWT): login, logout, register, Google, reset, confirm-email
- [ ] `AccountsController` — secured (any role): profile GET/PUT, change-password
- [ ] `AdminController` — AdminOnly: accounts CRUD, import, applications, roles/permissions
- [ ] Apply `[Authorize]` / `[Authorize(Policy = "AdminOnly")]` attributes
- [ ] Register all services inside `IdentityAccessModuleExtensions.cs`:
  ```csharp
  services.AddIdentityAccessModule(configuration);
  ```
  Including: DbContext, MediatR handlers, TokenService, EmailService, Redis, MassTransit consumers

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] EF migration applies cleanly against dev SQL Server
- [ ] Integration tests (xUnit):
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