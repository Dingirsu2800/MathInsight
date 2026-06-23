# Implementation Plan: Testing Module

**Branch**: `003-testing` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/003-testing/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Testing` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `tst`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Testing/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.TestingExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `tests`
  - Table: `test_questions`
  - Table: `test_sessions`
  - Table: `test_answers`
  - Table: `test_answer_options`
  - Table: `test_incidents`

### Service & API Gateway
- Controllers:
    - POST /api/v1/tests/generate
    - POST /api/v1/tests/sessions/start
    - POST /api/v1/tests/sessions/{id}/auto-save
    - POST /api/v1/tests/sessions/{id}/incident
    - POST /api/v1/tests/sessions/{id}/submit
    - GET /api/v1/tests/sessions/{id}/solution
    - POST /api/v1/tests/sessions/{id}/questions/{questionId}/report