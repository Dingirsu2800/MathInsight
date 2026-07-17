# Tasks Checklist: Testing Module

**Branch**: `003-testing` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] Create EF `IEntityTypeConfiguration` for all 7 entities mapped to current DB script tables:
  - [x] `TestConfiguration` — nullable `test_code` with filtered UNIQUE index when not null; FK to `blueprints`
  - [x] `TestQuestionConfiguration` — composite PK `(test_id, question_id)`, `question_order`
  - [x] `TestSessionConfiguration` — composite index `(student_id, status)`; status enum constraint
  - [x] `TestAnswerConfiguration` — composite UNIQUE `(session_id, question_id)`; `points_earned` default 0.00
  - [x] `TestAnswerOptionConfiguration` — composite PK `(test_answer_id, answer_id)` (DC-07)
  - [x] `TestAnswerPartConfiguration` — FK to `test_answers`, FK to `question_parts`, `student_answer` (max length 1000)
  - [x] `TestIncidentConfiguration` — FK to `test_sessions`, `type` enum constraint
- [x] Create `TestingDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Coordinate with TestGen (009) on shared current DB script tables — same DbContext or separate registrations.
- [x] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [x] Seed: 2 tests (1 ACTIVE, 1 ARCHIVED), 3 test sessions per TDS §3.6

---

## Phase 2: Core Domain Logic

- [x] **StartSession Command**:
  - [x] Validate test `TestStatus = ACTIVE`
  - [x] Check no existing `InProgress` session for same `(StudentID, TestID)` (BR-15)
  - [x] Create `TestSession` with `Status = InProgress`, `SubmissionType = NULL`, `StartTime = NOW`
  - [x] Create `TestAnswer` stub records for each `TestQuestion` in the test
  - [x] Return session with question list (randomized per blueprint)

- [x] **AutoSave Command**:
  - [x] Validate session `Status = InProgress` (reject if `Graded` or `Abandoned`)
  - [x] Batch update `TestAnswer` (`AnswerID`, `ShortAnswerText`, `TimeSpent`) and `TestAnswerPart` (`BooleanAnswer`, `TextAnswer`, `NumericAnswer` based on part type)
  - [x] Update `UpdateChoiceTime`; set `FirstChoiceTime` if null
  - [x] Return `{ savedAt, remainingSeconds }` — remaining time from `StartTime + DurationMinutes`

- [x] **RecordIncident Command**:
  - [x] Insert `TestIncident` record with `Type = TAB_SWITCH | FOCUS_LOSS`
  - [x] Count incidents for session → if `>= 5`, call `ForceSubmitSessionCommand` (BR-10)

- [x] **SubmitSession Command** (UC-49):
  - [x] Validate `Status = InProgress`
  - [x] Lock answer writes inside the submit transaction
  - [x] Set `end_time = NOW`, `submission_type = StudentSubmit`
  - [x] Calculate `duration = (end_time - start_time).TotalSeconds`
  - [x] Count `num_abandoned` (unanswered/abandoned questions per BR-16b)
  - [x] **Practice mode (BR-16)**: Invoke Grading via MediatR in-process; commit only after grading updates `status = Graded`; return `200 OK`
  - [x] **Exam mode (BR-17)**: Publish `TestSubmittedEvent` to MassTransit queue; return `202 Accepted` immediately
  - [x] Publish `GradeCalculatedEvent` after successful grading (handled by Grading module)

- [x] **ForceSubmitSession Command**:
  - [x] Validate `Status = InProgress`
  - [x] Set `EndTime = NOW`, `SubmissionType = TimeoutSubmit` for timer expiry or `SystemSubmit` for system/proctor submit
  - [x] Save current auto-save state
  - [x] **Practice mode**: Invoke Grading via MediatR in-process; commit only after grading updates `status = Graded`
  - [x] **Exam mode**: Publish `TestSubmittedEvent` to MassTransit queue; grading proceeds asynchronously

- [x] **ReportSessionQuestion Command** (UC-48):
  - [x] Delegates to QuestionBank module's `ReportQuestionCommand`
  - [x] Pass `SessionID` context for audit

- [x] **GetDetailedSolution Query** (UC-50):
  - [x] Validate session `Status = Graded` (reject with 403 if not)
  - [x] Return: questions with `QuestionContent`, `answers`, `IsCorrect` per answer, `PointsEarned`, and rich-text/plain-text explanation
  - [x] Include `TestAnswer.ShortAnswerText` for SHORT_ANSWER type

---

## Phase 3: Controller and Routing

- [ ] `TestSessionsController` — StudentOnly: start, auto-save, incident, submit
- [ ] `SolutionController` — StudentOnly: GET solution (validates `Graded` status)
- [ ] Register all services inside `TestingModuleExtensions.cs`

---

## Phase 4: Verification

- [x] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-47: Start session → `InProgress`, correct question count
  - [ ] UC-47: Start duplicate `InProgress` session → 409 (BR-15)
  - [ ] UC-47: Auto-save 5 answers → persisted, `update_choice_time` set
  - [ ] UC-47: 4 incidents → no force-submit; 5th incident → `Graded` with `SubmissionType = SystemSubmit`
  - [ ] UC-49: Normal submit → `Graded`, `SubmissionType = StudentSubmit`, grading fields populated
  - [ ] UC-49: Submit `Graded` session → 409 (DC-03)
  - [ ] UC-49: Submit with unanswered questions → `NumAbandoned` = unanswered count
  - [ ] UC-50: View solution before `Graded` → 403
  - [ ] UC-50: View solution after `Graded` → full question/answer data returned
