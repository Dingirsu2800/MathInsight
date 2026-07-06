# Implementation Plan: Testing Module

**Branch**: `003-testing` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](spec.md)

## Summary

Builds the `MathInsight.Modules.Testing` component managing student test sessions: starting sessions, answering questions, auto-save, incident tracking, and submission flow. Submit invokes the Grading module in-process for MVP and persists only durable session states.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core |
| Storage | SQL Server; map to current DB script tables shared with TestGen |
| Real-time | SignalR (optional — for timer sync and incident alert) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Testing/
├── Commands/
│   ├── StartSession/           # UC-47: create TestSession (InProgress), create TestAnswer stubs
│   ├── AutoSave/               # UC-47: batch update TestAnswer selections
│   ├── RecordIncident/         # UC-47: create TestIncident; force-submit if count >= 5
│   ├── SubmitSession/          # UC-49: normal submit → StudentSubmit → Graded
│   ├── ForceSubmitSession/     # UC-49: timer/system submit → TimeoutSubmit/SystemSubmit → Graded
│   └── ReportSessionQuestion/  # UC-48: create QuestionReport during session
├── Queries/
│   ├── GetSessionStatus/       # Current session state + remaining time
│   ├── GetSessionAnswers/      # Load saved answers for session resume
│   └── GetDetailedSolution/    # UC-50: post-grading solution view (only if Graded)
├── Events/
│   └── TestSubmittedEvent.cs   # Optional transient notification inside submit flow
├── Persistence/
│   ├── TestingDbContext.cs     # Shared connection, maps to current DB script table names
│   ├── Configurations/
│   │   ├── TestConfiguration.cs
│   │   ├── TestQuestionConfiguration.cs
│   │   ├── TestSessionConfiguration.cs
│   │   ├── TestAnswerConfiguration.cs
│   │   ├── TestAnswerOptionConfiguration.cs
│   │   ├── TestAnswerPartConfiguration.cs
│   │   └── TestIncidentConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── TestSessionsController.cs
│   └── SolutionController.cs
└── TestingModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Key Indexes |
|-------|-------------|
| `Test` | `TestCode` nullable with filtered unique index when not null; `BlueprintID` nullable FK; `TestFormat` (Practice | Exam); `GeneratedForStudentID` nullable FK; `GeneratedBy` |
| `TestQuestion` | Composite PK `(TestID, QuestionID)` |
| `TestSession` | `(StudentID, Status)` index; durable status values `InProgress`, `Graded`, `Abandoned`; `SubmissionType` captures submit reason |
| `TestAnswer` | `SessionID`, `QuestionID`, grading fields |
| `TestAnswerOption` | Composite PK `(TestAnswerID, AnswerID)` |
| `TestAnswerPart` | `TestAnswerPartID` PK; `TestAnswerID` FK; `QuestionPartID` FK; `AnswerID` FK (nullable); `ShortAnswerText` (nullable); `IsCorrect` (nullable); `PointsEarned` |
| `TestIncidents` | `SessionID` FK |

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
GET    /api/v1/tests/sessions/{id}/solution      # UC-50: view solution (only if Graded)
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `GradeCalculatedEvent` | Grading module (004) | Recommender/Gamification/Notification | Trigger competency update, activity, and "test graded" notification |

### Cross-Module Dependencies

- **TestGen module (009)**: Creates `Test` + `TestQuestion` records before session start.
- **Grading module (004)**: Called in-process during submit; populates `TestAnswer.is_correct`, `points_earned`, session score, and sets `TestSession.status = Graded`.
- **QuestionBank module (002)**: `question_id` references in `TestAnswer`; question content queried for solution display.
- **Recommender module (005)**: Reads session results after grading for competency updates.

### Auto-Save Mechanics

- Client sends `POST /api/v1/tests/sessions/{id}/auto-save` every 5 minutes OR on each answer change.
- Payload: `{ answers: [{ questionId, answerId, selectedOptions, shortAnswerText, parts: [{ questionPartId, answerId, selectedOptions, shortAnswerText }] }] }`.
- Handler validates `session_id` is `InProgress`, updates `TestAnswer` and `TestAnswerPart` records in batch.
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
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests (xUnit):
   - UC-47: Start session → TestSession `InProgress`, TestAnswer stubs created.
   - UC-47: Auto-save → answers persisted, `update_choice_time` updated.
   - UC-47: Log 4 incidents → no force-submit.
   - UC-47: Log 5th incident → session force-submitted, `status = Graded`, `submission_type = SystemSubmit`.
   - UC-49: Normal submit → `status = Graded`, `submission_type = StudentSubmit`, grading fields populated.
   - UC-49: Submit already-graded session → 409 (DC-03).
   - UC-50: View solution before `Graded` → 403.
   - UC-50: View solution after `Graded` → returns questions + correct answers + explanations.
