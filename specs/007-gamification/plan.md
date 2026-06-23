# Implementation Plan: Gamification Module

**Branch**: `007-gamification` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/007-gamification/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Gamification` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `gam`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Gamification/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.GamificationExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `badges`
  - Table: `student_badges`
  - Table: `study_streaks`
  - Table: `target_scores`
  - Table: `activity_logs`

### Service & API Gateway
- Controllers:
    - GET /api/v1/gamification/streak
    - GET /api/v1/gamification/badges
    - GET /api/v1/gamification/badges/progress
    - GET /api/v1/gamification/targets
    - POST /api/v1/gamification/targets
    - PUT /api/v1/gamification/targets/{id}