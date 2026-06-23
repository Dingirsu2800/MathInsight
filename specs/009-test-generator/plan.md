# Implementation Plan: Test Generator Module

**Branch**: `009-test-generator` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/009-test-generator/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.TestGen` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `tst` - shared connection and DbContext configuration)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.TestGen/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces (e.g. GenerationEngine)
├── Persistence/         # DB Contexts, entity configurations
└── TestGenModuleExtensions.cs
```

## Proposed Changes

### Database Layer
- Table: `tst.blueprints`
- Table: `tst.blueprint_details`

### Service & API Gateway
- Controllers:
  - `GET /api/v1/blueprints` - Get all blueprints (with search/filter by status, grade)
  - `POST /api/v1/blueprints` - Create new blueprint (Draft status)
  - `GET /api/v1/blueprints/pending` - View pending review blueprints (creator != current user)
  - `POST /api/v1/blueprints/{id}/review` - Approve or Reject blueprint with notes
  - `POST /api/v1/blueprints/{id}/clone` - Deep-copy blueprint matrix
  - `PUT /api/v1/blueprints/{id}` - Update blueprint (only in Draft/Rejected status)
  - `DELETE /api/v1/blueprints/{id}` - Delete blueprint (hard delete if draft/unused, soft-deletes/archives otherwise)

### Integration & Domain Events
- **TestGen** subscribes to event messages or communicates with the `Recommender` service internally using MediatR to query a student's `WeakTags` when executing test generation logic.
- During question selection:
  - Standard select: match topic & difficulty.
  - Adaptive select: query `rcm` module for student's `WeakTags`. If found, apply selection bias weighting (70% priority to WeakTag questions) within the specified topic matrices.
