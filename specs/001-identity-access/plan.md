# Implementation Plan: Identity & Access Module

**Branch**: `001-identity-access` | **Date**: 2026-06-23 | **Updated**: 2026-06-28
**Spec**: [spec.md](spec.md)

## Summary

Builds the `MathInsight.Modules.Identity_Access` component handling authentication (local + Google OAuth), account lifecycle management, role-permission RBAC, and teacher application verification. Registers with YARP gateway proxy routing and DI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client), BCrypt.Net-Next |
| Storage | SQL Server; map to current DB script tables |
| Cache | Redis (JWT blacklist, email confirmation tokens) |
| External | Google OAuth 2.0 via `AddGoogle()` |
| Testing | xUnit / Integration tests |
| Platform | Windows / Linux (Docker containerized) |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Identity_Access/
├── Commands/
│   ├── Login/                  # LoginCommand + Handler (JWT issuance)
│   ├── Logout/                 # LogoutCommand + Handler (Redis blacklist)
│   ├── Register/               # StudentRegisterCommand, TeacherRegisterCommand
│   ├── ChangePassword/
│   ├── ResetPassword/
│   ├── ConfirmEmail/           # EmailConfirmationCommand + Handler
│   ├── GoogleCallback/         # GoogleOAuthCallbackCommand + Handler
│   ├── ManualCreateAccount/    # Admin: CreateAccountManuallyCommand
│   ├── ImportAccounts/         # Admin: ImportFromExcelCommand (MassTransit queue)
│   ├── UpdateAccount/
│   ├── ToggleAccountStatus/    # Activate/Deactivate
│   ├── ResolveApplication/     # Approve/Reject TeacherApplication
│   ├── AdjustPermission/
│   └── UpdateRole/
├── Queries/
│   ├── GetProfile/             # GetProfileQuery + Handler
│   ├── GetAccountList/         # Admin: paged account list
│   └── GetTeacherApplications/ # Admin: paginated application list
├── Events/
│   └── TeacherApplicationSubmittedEvent.cs   # MediatR domain event
├── Persistence/
│   ├── IdentityDbContext.cs    # Shared connection, maps to current DB script table names
│   ├── Configurations/         # EF IEntityTypeConfiguration per entity
│   │   ├── AccountConfiguration.cs
│   │   ├── RoleConfiguration.cs
│   │   ├── PermissionConfiguration.cs
│   │   ├── RolePermissionConfiguration.cs
│   │   ├── StudentConfiguration.cs
│   │   ├── TeacherConfiguration.cs
│   │   ├── ExpertConfiguration.cs
│   │   └── TeacherApplicationConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── AuthController.cs
│   ├── AccountsController.cs
│   └── AdminController.cs
├── Services/
│   ├── ITokenService.cs        # JWT issuance, blacklist check
│   ├── TokenService.cs
│   ├── IEmailService.cs        # Email confirmation dispatch
│   ├── EmailService.cs
│   └── Auth/
│       ├── IAuthSessionService.cs        # Login lockout, active session, blacklist abstraction
│       ├── RedisAuthSessionService.cs    # Redis-backed implementation
│       └── InMemoryAuthSessionService.cs # Optional local fallback when Redis is disabled
└── IdentityModuleExtensions.cs   # AddIdentityModule() DI registration
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Notes |
|-------|-------|
| `Account` | Core credential + profile table |
| `Role` | 4 seeded roles: Admin, Expert, Teacher, Student |
| `Permission` | Permission keys (e.g. `lecture:publish`, `test:generate`) |
| `RolePermission` | Composite PK junction table |
| `Student` | 1:1 with Account |
| `Teacher` | 1:1 with Account |
| `Expert` | 1:1 with Account |
| `TeacherApplication` | Status lifecycle from the current DB script |

### Service & API Gateway — REST Endpoints

**Auth**
```
POST   /api/v1/auth/login                    # UC-01: Local login → returns JWT
POST   /api/v1/auth/logout                   # UC-02: JWT required; invalidate JWT (Redis blacklist)
POST   /api/v1/auth/register/student         # UC-39: Student self-register
POST   /api/v1/auth/register/teacher         # UC-08: Teacher self-register (→ PENDING)
POST   /api/v1/auth/google                   # UC-07: Google OAuth initiate
GET    /api/v1/auth/google/callback          # UC-07: Google OAuth callback → JWT
POST   /api/v1/auth/reset-password           # UC-06: Initiate reset (send email)
POST   /api/v1/auth/confirm-email            # Email confirmation token validation
```

### Auth API Contract

Auth endpoints return stable error codes for frontend localization. The `message` field is developer-facing and may remain English; Vietnamese user-facing text is handled by frontend clients.

Example error response:

```json
{
  "code": "AUTH_INVALID_CREDENTIALS",
  "message": "Invalid username/email or password."
}
```

Backend exposes one role-agnostic login endpoint:

```http
POST /api/v1/auth/login
```

Frontend clients may split login UI by role group, but backend does not require separate login endpoints per role. Successful login responses must include the authenticated user's role, allowing frontend routing such as Student, Teacher, Expert, or Admin dashboards.

**Account Self-Service (JWT required, any authenticated role)**
```
GET    /api/v1/accounts/profile              # UC-04: View own profile
PUT    /api/v1/accounts/profile              # UC-05: Update own profile
POST   /api/v1/accounts/change-password      # UC-03: Change password (requires old pass)
```

**Admin Management (AdminOnly policy)**
```
GET    /api/v1/admin/accounts                # UC-09: Paged account list (filter by role, status)
POST   /api/v1/admin/accounts/manual         # UC-11: Create account manually
POST   /api/v1/admin/accounts/import         # UC-12: Excel bulk import
PUT    /api/v1/admin/accounts/{id}           # UC-13: Update account info
PUT    /api/v1/admin/accounts/{id}/status    # UC-14: Activate / Deactivate
GET    /api/v1/admin/applications            # UC-10: View teacher applications (paged)
POST   /api/v1/admin/applications/{id}/resolve  # UC-15: Approve / Reject application
PUT    /api/v1/admin/roles/{roleId}/permissions # UC-16: Adjust permissions for role
PUT    /api/v1/admin/roles/{roleId}          # UC-17: Update role name/description
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `TeacherApplicationSubmittedEvent` | Identity module | Notification module | Notify Admin of new teacher application |
| `AccountCreatedEvent` | Identity module | Notification module | Send welcome email |
| `ApplicationResolvedEvent` | Identity module | Notification module | Notify Teacher of approval/rejection result |

### Cross-Module Dependencies

- **Notification module** listens to `AccountCreatedEvent`, `ApplicationResolvedEvent` via MassTransit.
- **Redis** used for: JWT blacklist (`jwt:blacklist:{tokenId}`), email confirmation token (`email:confirm:{token}`), login failure counter (`login:fail:{accountId}`).
- **Auth session service** abstracts login lockout, Student single-session enforcement, and token blacklist checks. Redis is the production/dev-cache implementation; an in-memory implementation may be used as a local fallback when Redis is disabled.
- **MassTransit queue**: `excel_import_queue` — async bulk import processing.

## Verification Plan

1. Run `dotnet build` — zero compile errors.
2. Verify EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:
   - Login with correct credentials → 200 + JWT.
   - Login with wrong password 5 times → 429/403 (locked).
   - Duplicate email register → 409.
   - Unconfirmed email login → 401.
   - Teacher register → status PENDING.
   - Admin approve teacher → status APPROVED, `is_active = true`.
