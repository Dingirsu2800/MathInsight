# Implementation Plan: Identity & Access Module

**Branch**: `001-identity-access` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/001-identity-access/spec.md)

## Summary

Builds the `MathInsight.Modules.Identity_Access` component handling authentication (local + Google OAuth), account lifecycle management, role-permission RBAC, and teacher application verification. Registers with YARP gateway proxy routing and DI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client), BCrypt.Net-Next |
| Storage | SQL Server (Schema: `usr`) |
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
│   ├── IdentityDbContext.cs    # Shared connection, `usr` schema
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
│   └── EmailService.cs
└── IdentityAccessModuleExtensions.cs   # AddIdentityAccessModule() DI registration
```

## Proposed Changes

### Database Layer (Schema: `usr`)

| Table | Notes |
|-------|-------|
| `usr.accounts` | Core credential + profile table |
| `usr.roles` | 4 seeded roles: Admin, Expert, Teacher, Student |
| `usr.permissions` | Permission keys (e.g. `lecture:publish`, `test:generate`) |
| `usr.role_permissions` | Composite PK junction table |
| `usr.students` | 1:1 with accounts |
| `usr.teachers` | 1:1 with accounts |
| `usr.experts` | 1:1 with accounts |
| `usr.teacher_applications` | Status lifecycle: PENDING → APPROVED/REJECTED |

### Service & API Gateway — REST Endpoints

**Auth (Public, no JWT required)**
```
POST   /api/v1/auth/login                    # UC-01: Local login → returns JWT
POST   /api/v1/auth/logout                   # UC-02: Invalidate JWT (Redis blacklist)
POST   /api/v1/auth/register/student         # UC-39: Student self-register
POST   /api/v1/auth/register/teacher         # UC-08: Teacher self-register (→ PENDING)
POST   /api/v1/auth/google                   # UC-07: Google OAuth initiate
GET    /api/v1/auth/google/callback          # UC-07: Google OAuth callback → JWT
POST   /api/v1/auth/reset-password           # UC-06: Initiate reset (send email)
POST   /api/v1/auth/confirm-email            # Email confirmation token validation
```

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
- **MassTransit queue**: `excel_import_queue` — async bulk import processing.

## Verification Plan

1. Run `dotnet build` — zero compile errors.
2. Apply EF Core migration: `dotnet ef migrations add Init_Identity --project MathInsight.WebAPI`.
3. Integration tests:
   - Login with correct credentials → 200 + JWT.
   - Login with wrong password 5 times → 429/403 (locked).
   - Duplicate email register → 409.
   - Unconfirmed email login → 401.
   - Teacher register → status PENDING.
   - Admin approve teacher → status APPROVED, `is_active = true`.