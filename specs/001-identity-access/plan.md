# Implementation Plan: Identity & Access Management

**Branch**: `[specs/001-identity-access]` | **Date**: 2026-06-22 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/001-identity-access/spec.md)
**Input**: Feature specification from `/specs/001-identity-access/spec.md`

## Summary
The Identity & Access Management module handles secure local and third-party authentication (Google OAuth 2.0), user self-registration, user profile settings, account lifecycle administration, and role-based access control (RBAC). 

From a technical perspective, we will implement this inside `MathInsight.Modules.Identity_Access` namespace as a modular monolith component. It separates from other modules using decoupled namespaces and communicates through MediatR domain events (e.g. `UserRegisteredEvent`, `AccountDeactivatedEvent`).

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: ASP.NET Core Identity Core, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.AspNetCore.Authentication.Google, MediatR  
**Storage**: SQL Server (relational data), Redis (JWT blacklist & caching permissions)  
**Testing**: xUnit, FluentAssertions, Moq  
**Target Platform**: Docker Containers / Linux Server  
**Project Type**: Web Service (Modular Monolith Component) + React SPA Frontend  
**Performance Goals**: Login and validation responses < 1.0 second under 200 concurrent users.  
**Constraints**: Hashed passwords (BCrypt strength 12), Account Lockout (5 failed attempts, 15 min lock), session uniqueness per Student.  
**Scale/Scope**: Mapped to 18 Use Cases (UC-01 to UC-17, UC-39) and 1 Background Job (JOB-01).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle I: Library-First**: Mapped successfully. The identity module has its core logic inside `MathInsight.Modules.Identity_Access` as a separate class library project.
- **Principle II: Secure Secrets**: Mapped successfully. No passwords, tokens, or client secrets are committed; they are read from environment variables or a local `.env` file.
- **Principle III: Test-First**: Test files will be generated and run before deploying code changes.
- **Principle IV: Isolation**: The database schema uses EF Core migrations isolated to the `accounts` and security tables.

## Project Structure

### Documentation (this feature)

```text
specs/001-identity-access/
├── spec.md              # Feature specification
├── plan.md              # This file (Technical Implementation Plan)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (Schemas and migrations)
├── quickstart.md        # Phase 1 output (Quickstart guide)
├── contracts/           # Phase 1 output (API DTO schemas)
│   ├── auth.json
│   └── admin.json
└── tasks.md             # Phase 2 output (Actionable implementation tasks checklist)
```

### Source Code (repository root)

```text
src/
├── MathInsight.Modules.Identity_Access/
│   ├── Extensions/
│   │   └── IdentityModuleExtensions.cs
│   ├── Models/
│   │   ├── Account.cs
│   │   ├── Student.cs
│   │   ├── Teacher.cs
│   │   ├── TeacherApplication.cs
│   │   ├── Role.cs
│   │   └── Permission.cs
│   ├── Services/
│   │   ├── IdentityService.cs
│   │   ├── ProfileService.cs
│   │   └── TokenService.cs
│   ├── Db/
│   │   ├── IdentityDbContext.cs
│   │   └── Seed/
│   │       └── data.sql
│   └── MathInsight.Modules.Identity_Access.csproj
├── MathInsight.WebAPI/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   └── AdminController.cs
│   └── Program.cs
frontend/
├── src/
│   ├── pages/
│   │   ├── Login.jsx
│   │   ├── RegisterStudent.jsx
│   │   ├── RegisterTeacher.jsx
│   │   └── UserManagement.jsx
│   └── services/
│       └── authService.js
```

**Structure Decision**: Using Option 2 (Web application with separate `frontend` React SPA and `src/` modular monolith backend structure).

## Complexity Tracking

*No violations to track. Standard JWT + BCrypt implementation satisfies all principles.*
