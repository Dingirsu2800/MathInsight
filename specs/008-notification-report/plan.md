# Implementation Plan: Notification & Report Module

**Branch**: `008-notification-report` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/008-notification-report/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Notification_Report` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `ntf`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Notification_Report/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.Notification_ReportExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `notifications`

### Service & API Gateway
- Controllers:
    - GET /api/v1/notifications
    - PUT /api/v1/notifications/{id}/read
    - GET /api/v1/reports/competency
    - GET /api/v1/reports/heatmap
    - GET /api/v1/reports/performance
    - GET /api/v1/reports/leaderboard
    - GET /api/v1/reports/exam-history