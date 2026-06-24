# AGENTS.md

## Project overview

MathInsight is an education platform built with:

- Backend: ASP.NET Core / .NET 10
- Frontend: React JavaScript with Vite
- Database: Azure SQL / SQL Server
- Architecture: Modular Monolith by feature modules

## Communication rules

- Respond in the same language as the user's message.
- Be professional, precise, and direct.
- Explain root causes and trade-offs when reviewing errors or architecture decisions.
- When the user is learning, prefer clear step-by-step explanations over unexplained code dumps.

## Repository rules

- Do not modify submitted reports unless explicitly requested.
- Do not change database schema fields without confirmation.
- Do not add new database columns only for implementation convenience.
- Do not commit secrets, connection strings, tokens, `.env`, `bin/`, `obj/`, or `node_modules/`.
- Use JavaScript for frontend, not TypeScript, unless the team changes this decision.
- Backend modules should follow feature boundaries, not technical-layer-only grouping.

## Important project decisions

- Student and Expert accounts require email confirmation before use.
- Teacher accounts require Admin approval and activation before use.
- Student reporting a question does not immediately hide the question and does not change it to `REPORTED`.
- Teacher cannot report questions.
- Expert-created questions are published/approved by default.
- Lecture-Material relationship remains many-to-many according to the current ERD.
- TargetScore is a 0-10 score and can be tracked per TagTopic according to the current ERD.

## Backend guidance

- Keep DTOs at API/application boundaries.
- Use interfaces for services where behavior may have multiple implementations or needs testing.
- Prefer module registration methods, such as `AddIdentityAccessModule`, instead of coupling `WebAPI` directly to internal handlers or consumers.
- Keep RabbitMQ and Redis optional unless the feature explicitly requires them.
- Keep local domain events and integration events separate when possible.

## Frontend guidance

- Use React JavaScript.
- Prefer feature-based folders.
- Component files should use PascalCase, for example `LoginPage.jsx`.
- API utilities should be centralized instead of duplicating Axios setup.
- Keep reusable UI components separate from feature-specific pages.

## Verification

Before finalizing backend changes, run the relevant .NET build or tests when the .NET 10 SDK is available.

Before finalizing frontend changes, run:

```bash
npm install
npm run build
```

If verification cannot be run, explain the reason clearly.
