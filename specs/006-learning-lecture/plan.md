# Implementation Plan: Learning & Lecture Module

**Branch**: `006-learning-lecture` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/006-learning-lecture/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Learning_Lecture` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `lrn`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Learning_Lecture/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.Learning_LectureExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `lectures`
  - Table: `materials`
  - Table: `discussion_questions`
  - Table: `discussion_answers`
  - Table: `discussion_reports`

### Service & API Gateway
- Controllers:
    - GET /api/v1/lectures
    - POST /api/v1/lectures
    - PUT /api/v1/lectures/{id}
    - PUT /api/v1/lectures/{id}/publish
    - PUT /api/v1/lectures/{id}/archive
    - GET /api/v1/materials
    - POST /api/v1/materials
    - PUT /api/v1/materials/{id}
    - POST /api/v1/lectures/{id}/materials/{materialId}
    - GET /api/v1/discussions/{lectureId}
    - POST /api/v1/discussions/{lectureId}/questions
    - POST /api/v1/discussions/questions/{qId}/answers
    - PUT /api/v1/discussions/comments/{id}
    - DELETE /api/v1/discussions/comments/{id}
    - POST /api/v1/discussions/reports
    - GET /api/v1/teacher/discussions/moderation
    - POST /api/v1/teacher/discussions/moderation/{reportId}/resolve