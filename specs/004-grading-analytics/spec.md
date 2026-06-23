# Feature Specification: Grading & Analytics Module

**Feature Branch**: `004-grading-analytics`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Manages grading rules execution, background queues for exam grading, LaTeX explanations rendering, and math problem solver assistant integrations."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-49: Submit Test/question (Auto-grading trigger)**
- **UC-50: View Detailed Solution**
- **UC-51: Ask Chatbot for assistance**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-03: Submitted or force-submitted test answers are read-only.**
- **DC-05: Auto-grading, updating tags mastery points, and logging activity must execute as a single database transaction.**
- **Real-time grading: for Practice mode, grading takes < 2.0 seconds.**
- **Deferred grading: for Exam mode, grading is processed asynchronously via RabbitMQ to support high concurrency under 60.0 seconds.**

### Key Entities *(include if feature involves data)*

- **Uses test_sessions, test_answers, test_answer_options tables from the Testing module for processing grading results.**: 

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `grd` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.