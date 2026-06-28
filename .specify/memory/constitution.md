# MathInsight Constitution

## Core Principles

### I. Specification-First Delivery

Every feature must start from the Spec Kit flow: `spec.md` defines business behavior, `plan.md` defines implementation approach, and `tasks.md` defines executable work. Code changes must trace back to an accepted spec item. If implementation discovers a conflict, update or clarify the spec before broadening the code scope.

### II. Current Database Contract Is Protected

The SQL creation scripts under `../Database` are the current implementation contract for MVP development. Code must map to the existing table and column names unless the team explicitly approves a migration. Do not add, remove, or rename database fields just to make code easier. Report, ERD, and schema changes require explicit approval because they affect submitted project documentation.

### III. Modular Monolith Boundaries

Backend code uses ASP.NET Core on .NET 10 and a modular-monolith structure. Each module owns its controllers, DTOs, use cases, and persistence mapping for its feature area. Cross-module sharing belongs in `MathInsight.Shared` only when it is genuinely reusable infrastructure or a stable integration contract, such as domain events. Avoid hidden dependencies between feature modules.

### IV. MVP Scope Over Infrastructure Complexity

For local development, infrastructure that is not required for the active feature must have a safe fallback. RabbitMQ may be enabled for asynchronous flows, but the application must run locally with an in-memory bus by default. Redis or other external services must be introduced only when a spec-backed feature needs them, such as test-session state or caching with a clear invalidation rule.

### V. Quality Gates Are Mandatory

Before a change is considered ready, the affected application must build successfully. Backend changes require `dotnet build`. Frontend changes require `npm run build`. Warnings about vulnerable packages, broken contracts, or schema drift must be reported instead of ignored. Generated outputs such as `bin`, `obj`, `node_modules`, and `dist` must not be committed.

### VI. Stable API Contracts and Client Localization

Backend APIs must return stable machine-readable error codes for client handling. Developer-facing messages may be written in English, but user-facing localization belongs to frontend clients. Frontend UI structure, such as role-specific pages or workflow-specific screens, must not force duplicated backend endpoints unless a spec explicitly requires different business rules, authorization, or validation.

## Product Decisions

- Frontend stack: React with JavaScript and Vite. Do not migrate to TypeScript unless the team explicitly changes the stack.
- Backend stack: ASP.NET Core on .NET 10, deployed to Azure App Service or an equivalent Azure-hosted backend.
- Database: Azure SQL / SQL Server. Connection strings and secrets must come from environment variables, user secrets, or Azure configuration, never committed files.
- Backend endpoints should remain role-agnostic when business behavior is shared. Frontend clients may split UI by role or workflow, but backend APIs should expose separate endpoints only when behavior, authorization, or validation materially differs.
- Student and Expert self-registration accounts start inactive and become usable only after email confirmation.
- Teacher registration requires an admin approval flow before activation.
- Question Bank: Expert-created questions are published by default. Student reports do not hide a question or change its status. Teachers cannot report questions. Admin rejection requires the original Expert to handle it and Admin to re-review it.
- TargetScore is per Student and TagTopic, representing a score target on the 0-10 scale for that topic.
- Lecture and Material are many-to-many in the current ERD/design and should remain that way unless the team approves a redesign.

## Development Workflow

1. Confirm the relevant spec before coding.
2. Keep each pull request focused on one feature slice or one foundation change.
3. Update documentation only when it is part of the approved scope.
4. Keep local configuration in ignored files such as `.env` or `appsettings.Development.json`.
5. Do not commit generated build outputs, dependency folders, local database files, or secrets.

## Governance

This constitution has higher priority than placeholder implementation details and lower priority than explicit team decisions recorded in accepted specs or approved issue comments. Amendments require a short rationale, impact note, and approval from the team lead or project owner. Any change that affects submitted reports, ERD, or database schema must be called out before implementation.

**Version**: 1.1.0 | **Ratified**: 2026-06-26 | **Last Amended**: 2026-06-28
