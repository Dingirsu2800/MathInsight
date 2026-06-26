# Implementation Plan: Testing Module

**Branch**: `003-testing` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/003-testing/spec.md)

## Summary

Builds the `MathInsight.Modules.Testing` component managing student test sessions: starting sessions, answering questions, auto-save, incident tracking, and submission flow. Publishes `TestSubmittedEvent` consumed by the Grading module.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server (Schema: `tst` — shared with TestGen module) |
| Real-time | SignalR (optional — for timer sync and incident alert) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Testing/
├── Commands/
│   ├── StartSession/           # UC-47: create TestSession (IN_PROGRESS), create TestAnswer stubs
│   ├── AutoSave/               # UC-47: batch update TestAnswer selections
│   ├── RecordIncident/         # UC-47: create TestIncident; force-submit if count >= 5
│   ├── SubmitSession/          # UC-49: normal submit → SUBMITTED → publish TestSubmittedEvent
│   ├── ForceSubmitSession/     # UC-49: timer/incident force → FORCE_SUBMITTED → publish event
│   └── ReportSessionQuestion/  # UC-48: create QuestionReport during session
├── Queries/
│   ├── GetSessionStatus/       # Current session state + remaining time
│   ├── GetSessionAnswers/      # Load saved answers for session resume
│   └── GetDetailedSolution/    # UC-50: post-grading solution view (only if GRADED)
├── Events/
│   └── TestSubmittedEvent.cs   # MediatR notification → Grading module consumes
├── Persistence/
│   ├── TestingDbContext.cs     # Shared connection, `tst` schema
│   ├── Configurations/
│   │   ├── TestConfiguration.cs
│   │   ├── TestQuestionConfiguration.cs
│   │   ├── TestSessionConfiguration.cs
│   │   ├── TestAnswerConfiguration.cs
│   │   ├── TestAnswerOptionConfiguration.cs
│   │   └── TestIncidentConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── TestSessionsController.cs
│   └── SolutionController.cs
└── TestingModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Schema: `tst`)

| Table | Key Indexes |
|-------|-------------|
| `tst.tests` | `test_code` UNIQUE; `blueprint_id` FK |
| `tst.test_questions` | Composite PK `(test_id, question_id)` |
| `tst.test_sessions` | `student_id` BTREE; `(student_id, status)` composite |
| `tst.test_answers` | Composite UNIQUE `(session_id, question_id)` |
| `tst.test_answer_options` | Composite PK `(test_answer_id, answer_id)` |
| `tst.test_incidents` | `session_id` BTREE |

### Service & API Gateway — REST Endpoints

**Student (StudentOnly policy)**
```
POST   /api/v1/tests/sessions/start              # UC-47: create TestSession
GET    /api/v1/tests/sessions/{id}               # Get current session state + time remaining
GET    /api/v1/tests/sessions/{id}/answers       # Load saved answers (resume support)
POST   /api/v1/tests/sessions/{id}/auto-save     # UC-47: save answer selections
POST   /api/v1/tests/sessions/{id}/incident      # UC-47: log tab switch incident
POST   /api/v1/tests/sessions/{id}/submit        # UC-49: normal submit
POST   /api/v1/tests/sessions/{id}/questions/{qId}/report  # UC-48: report question in session
GET    /api/v1/tests/sessions/{id}/solution      # UC-50: view solution (only if GRADED)
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `TestSubmittedEvent` | Testing module | Grading module (004) | Trigger grading pipeline |
| `TestSubmittedEvent` | Testing module | Gamification module (007) | Log activity, update streak |
| `TestSubmittedEvent` | Testing module | Notification module (008) | Send "test submitted" push |

### Cross-Module Dependencies

- **TestGen module (009)**: Creates `Test` + `TestQuestion` records before session start.
- **Grading module (004)**: Consumes `TestSubmittedEvent` via MediatR; populates `TestAnswer.is_correct`, `points_earned`, session score.
- **QuestionBank module (002)**: `question_id` references in `TestAnswer`; question content queried for solution display.
- **Recommender module (005)**: Reads session results after grading for competency updates.

### Auto-Save Mechanics

- Client sends `POST /api/v1/tests/sessions/{id}/auto-save` every 5 minutes OR on each answer change.
- Payload: `{ answers: [{ questionId, answerId, selectedOptions, shortAnswerText }] }`.
- Handler validates `session_id` is `IN_PROGRESS`, updates `TestAnswer` records in batch.
- Returns `{ savedAt: "ISO8601", remainingSeconds: N }`.

### Incident Force-Submit Logic

```csharp
// In RecordIncidentHandler:
var incidentCount = await _db.TestIncidents.CountAsync(i => i.SessionId == sessionId);
if (incidentCount >= 5)
{
    await _mediator.Send(new ForceSubmitSessionCommand(sessionId));
}
```

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF migration applies cleanly against dev SQL Server.
3. Integration tests (xUnit):
   - UC-47: Start session → TestSession `IN_PROGRESS`, TestAnswer stubs created.
   - UC-47: Auto-save → answers persisted, `update_choice_time` updated.
   - UC-47: Log 4 incidents → no force-submit.
   - UC-47: Log 5th incident → session force-submitted, `status = FORCE_SUBMITTED`.
   - UC-49: Normal submit → `status = SUBMITTED`, `TestSubmittedEvent` published.
   - UC-49: Submit already-submitted session → 409 (DC-03).
   - UC-50: View solution before GRADED → 403.
   - UC-50: View solution after GRADED → returns questions + correct answers + explanations.