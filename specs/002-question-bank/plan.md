# Implementation Plan: Question Bank Module

**Branch**: `002-question-bank` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/002-question-bank/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.QuestionBank` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `qnb`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.QuestionBank/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.QuestionBankExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `questions`
  - Table: `answers`
  - Table: `questionversions`
  - Table: `questionreports`
  - Table: `tagtopics`
  - Table: `tagdifficultys`
  - Table: `questiontopics`

### Service & API Gateway
- Controllers:
    - GET /api/v1/questions
    - POST /api/v1/questions/single
    - POST /api/v1/questions/import
    - PUT /api/v1/questions/{id}
    - DELETE /api/v1/questions/{id}
    - GET /api/v1/questions/{id}/versions
    - POST /api/v1/questions/{id}/report
    - GET /api/v1/expert/reports
    - POST /api/v1/expert/reports/{id}/resolve
    - GET /api/v1/tags/topics
    - POST /api/v1/tags/topics
    - PUT /api/v1/tags/topics/{id}
    - DELETE /api/v1/tags/topics/{id}