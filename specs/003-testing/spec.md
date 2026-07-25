# Feature Specification: Testing Module

> **Approved scoring amendment**: [Scoring Contract V2](../scoring-contract-v2.md) requires immutable rendering, snapshot validation, and effective-point history.

**Feature Branch**: `003-testing`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-04), UCS UC-47–UC-50, TDS §3 (tests, test_sessions, test_answers, test_incidents)

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Notes |
|-------|------|---------------|-------|
| UC-47 | Doing Test/Question | Student | Start session, answer questions, auto-save |
| UC-48 | Report Error | Student | Report bad question during session |
| UC-49 | Submit Test/Question | Student | Normal or force-submit |
| UC-50 | View Detailed Solution | Student | After session is `Graded` |

### User Flow (BF-01: Student Takes a Test)

```
Student selects test config → TestGen generates Test → Student starts TestSession
→ answers questions (auto-save every 5 min or on selection)
→ tab switch incidents logged → 5 switches force-submits
→ timer expires → force-submit
→ Student submits → session locked
   ├─ Practice: GradingModule runs in-process (MediatR) → session becomes `Graded` → Student views solution
   └─ Exam: TestSubmittedEvent published to MassTransit queue → 202 Accepted
           → GradingModule consumer grades asynchronously → session becomes `Graded`
           → Student notified via SignalR/polling → Student views solution
```

### Edge Cases

- **Network disconnection**: System pauses countdown, resumes from last auto-save.
- **Timer expiration**: System force-submits, redirects to solution page.
- **Tab switch ≥ 5**: Session force-submitted immediately.
- **Partial answers**: Student submits with unanswered questions → confirm dialog → accepted as-is.
- **Re-submission attempt**: `status = Graded` or `Abandoned` → all answers immutable (DC-03).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-03**: Once a `TestSession` leaves `InProgress`, all associated `TestAnswer` records become strictly **read-only** to guarantee audit integrity. In MVP, submit/force-submit immediately grades the session, so `Submitted` and `ForceSubmitted` are not persisted as durable statuses.
- **DC-07**: `TestAnswerOption` composite PK constraint enforces unique option selection per question answer.
- **BR-10**: Exam security: browser tab focus loss is logged as a `TestIncident`. After **5 incidents**, the system immediately suspends the session and force-submits.
- **BR-11**: Student progress is auto-saved in the background every **5 minutes** or upon any answer selection change.
- **BR-12**: Timer countdown is synchronized with the server — client-side timer is display only; server enforces the actual deadline.
- **BR-13**: When timer reaches 00:00, system automatically locks the interface, saves all selected answers at that point, and triggers force-submit workflow.
- **BR-14**: `TestSession.test_format` must be set at session start: `Practice` or `Exam`. This cannot be changed after session creation.
- **BR-15**: A Student may have at most one `InProgress` session for the same `test_id` at any given time.
- **BR-16**: `TestAnswer.points_earned` is populated during grading (module 004). For Practice mode, submit returns only after grading succeeds and `status = Graded`. For Exam mode, submit returns `202 Accepted` immediately while grading proceeds asynchronously.
- **BR-16a**: `TestSession.submission_type` stores how the session was submitted: `StudentSubmit`, `TimeoutSubmit`, or `SystemSubmit`. It is required when `status = Graded` and must be null while `status = InProgress` or `Abandoned`.
- **BR-17**: Exam mode submissions use MassTransit async grading via `TestSubmittedEvent` published to queue. Practice mode continues using MediatR in-process synchronous grading. Frontend uses SignalR push (primary) or polling `GET /sessions/{id}` (fallback) to detect when Exam grading completes.
- **BR-16b**: An answer is considered "unanswered/abandoned" (which determines `TestSession.num_abandoned` and `GradeCalculatedEvent.Answers.IsAbandoned`) based on its `QuestionType`:
  - `SINGLE_CHOICE`: `answer_id IS NULL`
  - `TRUE_FALSE`: `answer_id IS NULL`
  - `MULTIPLE_SELECT`: No options selected (i.e., no entries in `TestAnswerOption`)
  - `SHORT_ANSWER`: `short_answer_text` is null or consists only of whitespace
  - `COMPOSITE`: All child parts are unanswered/abandoned (i.e., all child parts have null or whitespace-only `student_answer` values in `TestAnswerPart`)


### Key Entities *(include if feature involves data)*

- **Test**: `TestID` (PK), `BlueprintID` (FK → blueprints, nullable), `TestFormat` (**Practice** | **Exam**), `GeneratedForStudentID` (FK → students, nullable), `GeneratedBy` (default 'System'), `TestStatus` (**ACTIVE** | **ARCHIVED**), `TestName`, `TestCode` (nullable; unique when not null), `DurationMinutes`, `TotalQuestions`, `CreatedTime`
- **TestQuestion**: `TestID` (FK, PK), `QuestionID` (FK, PK), `QuestionOrder` — composite PK
- **TestSession**: `SessionID` (PK), `TestID` (FK), `StudentID` (FK), `TestFormat` (**Practice** | **Exam**), `Status` (**InProgress** | **Graded** | **Abandoned**), `SubmissionType` (**StudentSubmit** | **TimeoutSubmit** | **SystemSubmit**, nullable), `Duration`, `StartTime`, `EndTime`, `TotalQuestion`, `NumCorrect`, `NumIncorrect`, `NumAbandoned`, `Score`
- **TestAnswer**: `TestAnswerID` (PK), `SessionID` (FK), `QuestionID` (FK), `AnswerID` (FK, nullable for MultipleSelect/ShortAnswer), `QuestionNo`, `TimeSpent`, `FirstChoiceTime`, `UpdateChoiceTime`, `ShortAnswerText`, `IsCorrect` (nullable until graded), `PointsEarned` (0.00 until graded)
- **TestAnswerOption**: `TestAnswerID` (FK, PK), `AnswerID` (FK, PK) — for MultipleSelect
- **TestAnswerPart**: `TestAnswerID` (FK, PK), `PartID` (FK, PK), `BooleanAnswer` (nullable bool), `TextAnswer` (nullable string), `NumericAnswer` (nullable decimal), `IsCorrect` (nullable until graded), `PointsEarned` (0.00 until graded) — for Composite parts
- **TestIncident**: `IncidentID` (PK), `SessionID` (FK), `Type` (TAB_SWITCH | FOCUS_LOSS), `Time`


### Session State Machine

```
[Start]
  │
  ▼
InProgress ──(student submit + grading succeeds)──▶ Graded
  │
  ├──(timer expires + grading succeeds)───────────▶ Graded
  │
  ├──(system/proctor submit + grading succeeds)───▶ Graded
  │
  └──(student abandons / session expires)─────────▶ Abandoned
```

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| TestSession | test_format | `Practice`, `Exam` |
| TestSession | status | `InProgress`, `Graded`, `Abandoned` |
| TestSession | submission_type | `StudentSubmit`, `TimeoutSubmit`, `SystemSubmit` |
| Test | test_format | `Practice`, `Exam` |
| Test | test_status | `ACTIVE`, `ARCHIVED` |
| TestIncident | type | `TAB_SWITCH`, `FOCUS_LOSS` |


## Success Criteria *(mandatory)*

### Measurable Outcomes

- All test session API endpoints respond within **2 seconds** (NFR-P01).
- Auto-save completes within **1 second** of trigger.
- Force-submit on tab switch #5 triggers within **500ms**.
- Backend maps Testing entities to the current SQL script tables; no separate `tst` schema is created for MVP.
- Session recovery after network disconnection resumes within **3 seconds** of reconnect.

## Assumptions

- Target database is SQL Server. Backend maps to current DB script tables (`Test`, `TestQuestion`, `TestSession`, `TestAnswer`, `TestAnswerOption`, `TestIncidents`) instead of schema-prefixed tables.
- **Dual-path grading**: Practice mode uses MediatR in-process (synchronous); Exam mode uses MassTransit async (TestSubmittedEvent published to RabbitMQ or InMemory queue). The Grading module's `TestSubmittedConsumer` handles Exam messages.
- `TestSubmittedEvent` serves dual purpose: MediatR notification (Practice) and MassTransit message contract (Exam). It is not a persisted `Submitted` status.
- Real-time timer sync may use SignalR or server-side session expiry check on every auto-save request.
- `TestGen` module (009) is responsible for creating the `Test` and `TestQuestion` records before session start.
