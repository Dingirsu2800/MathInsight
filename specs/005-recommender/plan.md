# Implementation Plan: Recommender Module

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/005-recommender/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Recommender` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `rcm`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Recommender/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.RecommenderExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `rcm.competency_points`
  - Table: `rcm.tags_mastery`

### Services & SAR Integration
- **SAR Recommendation Pipeline**: 
  - Execute a Python script/runner utilizing the `recommenders` library's `SAR` algorithm.
  - Interaction data (student, topic_tag, correctness_rating) is exported from database logs or read directly from SQL Server.
  - The trained model calculates co-occurrence similarities and writes output recommendation mappings to Redis (`WeakTags` cache keys).
- **Internal Service (MediatR / In-Process DI)**:
  - `IRecommenderService.GetStudentWeakTags(Guid studentId)` returns the top weak tags for biasing test generation.
- **Event Consumer**:
  - `GradeCalculatedConsumer` catches `GradeCalculatedEvent`, updates `competency_points`, recalculates mastery status, updates `tags_mastery` in database, and invalidates the Redis cache for student weaknesses.

### Service & API Gateway
- Controllers:
    - GET /api/v1/recommender/weak-tags (Retrieve current weak tags for dashboard)
    - GET /api/v1/recommender/lectures (Get recommended lectures matching weak tags)
    - GET /api/v1/recommender/materials (Get recommended documents/PDFs matching weak tags)