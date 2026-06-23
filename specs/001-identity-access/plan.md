# Implementation Plan: Identity & Access Module

**Branch**: `001-identity-access` | **Date**: 2026-06-23 | **Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/001-identity-access/spec.md)

## Summary

This plan outlines the architecture and tasks required to build the `MathInsight.Modules.Identity_Access` project component. It registers the module with YARP gateway proxy routing and registers dependency lifecycles with the application program composition root.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: MediatR, EF Core, MassTransit (RabbitMQ client)

**Storage**: SQL Server (Schema: `usr`)

**Testing**: xUnit / Integration tests

**Target Platform**: Windows / Linux (Docker containerized)

**Project Type**: Modular Monolith Web API

## Project Structure

```text
src/MathInsight.Modules.Identity_Access/
├── Controllers/         # API Endpoint controllers
├── Services/            # Business services and interfaces
├── Persistence/         # DB Contexts, entity configurations
└── MathInsight.Modules.Identity_AccessExtensions.cs
```

## Proposed Changes

### Database Layer
  - Table: `accounts`
  - Table: `experts`
  - Table: `students`
  - Table: `teachers`
  - Table: `teacherapplications`
  - Table: `roles`
  - Table: `permissions`
  - Table: `rolepermissions`

### Service & API Gateway
- Controllers:
    - POST /api/v1/auth/login
    - POST /api/v1/auth/logout
    - POST /api/v1/auth/register
    - POST /api/v1/auth/google
    - POST /api/v1/auth/reset-password
    - POST /api/v1/auth/change-password
    - GET /api/v1/accounts/profile
    - PUT /api/v1/accounts/profile
    - GET /api/v1/admin/accounts
    - POST /api/v1/admin/accounts/manual
    - POST /api/v1/admin/accounts/import
    - PUT /api/v1/admin/accounts/{id}/status
    - GET /api/v1/admin/applications
    - POST /api/v1/admin/applications/{id}/resolve
    - PUT /api/v1/admin/roles/{id}/permissions