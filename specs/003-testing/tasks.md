# Tasks Checklist: Testing Module

## Scoring Contract V2

- [x] Map TestQuestion version/scoring fields and TestSession GradeRevision.
- [x] Render and validate submissions against QuestionVersion V2.
- [x] Implement trusted session-linked Student report creation.
- [x] Return immutable content, machine points, effective points, and invalidation audit.

**Branch**: `003-testing` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for all 7 entities mapped to current DB script tables:
  - [ ] `TestConfiguration` — nullable `TestCode` with filtered UNIQUE index when not null; FK to `blueprints`
  - [ ] `TestQuestionConfiguration` — composite PK `(TestID, QuestionID)`, `QuestionOrder`
  - [ ] `TestSessionConfiguration` — composite index `(StudentID, Status)`; Status enum constraint
  - [ ] `TestAnswerConfiguration` — composite UNIQUE `(SessionID, QuestionID)`; `PointsEarned` default 0.00
  - [ ] `TestAnswerOptionConfiguration` — composite PK `(TestAnswerID, AnswerID)` (DC-07)
  - [ ] `TestAnswerPartConfiguration` — composite PK `(TestAnswerID, PartID)`; FKs to `TestAnswer` and `QuestionPart`; type-specific fields: `BooleanAnswer` (BIT), `TextAnswer` (NVARCHAR), `NumericAnswer` (DECIMAL)
  - [ ] `TestIncidentConfiguration` — FK to `TestSession`, `Type` enum constraint
- [ ] Create `TestingDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Coordinate with TestGen (009) on shared current DB script tables — same DbContext or separate registrations.
- [x] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 2 tests (1 ACTIVE, 1 ARCHIVED), 3 test sessions per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **StartSession Command**:
  - [ ] Validate test `TestStatus = ACTIVE`
  - [ ] Check no existing `InProgress` session for same `(StudentID, TestID)` (BR-15)
  - [ ] Create `TestSession` with `Status = InProgress`, `SubmissionType = NULL`, `StartTime = NOW`
  - [ ] Create `TestAnswer` stub records for each `TestQuestion` in the test
  - [ ] Return session with question list (randomized per blueprint)

- [ ] **AutoSave Command**:
  - [ ] Validate session `Status = InProgress` (reject if `Graded` or `Abandoned`)
  - [ ] Batch update `TestAnswer` (`AnswerID`, `ShortAnswerText`, `TimeSpent`) and `TestAnswerPart` (`BooleanAnswer`, `TextAnswer`, `NumericAnswer` based on part type)
  - [ ] Update `UpdateChoiceTime`; set `FirstChoiceTime` if null
  - [ ] Return `{ savedAt, remainingSeconds }` — remaining time from `StartTime + DurationMinutes`

- [ ] **RecordIncident Command**:
  - [ ] Insert `TestIncident` record with `Type = TAB_SWITCH | FOCUS_LOSS`
  - [ ] Count incidents for session → if `>= 5`, call `ForceSubmitSessionCommand` (BR-10)

- [ ] **SubmitSession Command** (UC-49):
  - [ ] Validate `Status = InProgress`
  - [ ] Lock answer writes inside the submit transaction
  - [ ] Set `EndTime = NOW`, `SubmissionType = StudentSubmit`
  - [ ] Calculate `Duration = (EndTime - StartTime).TotalSeconds`
  - [ ] Count `NumAbandoned` (unanswered/abandoned questions per BR-16b)
  - [ ] Invoke Grading module in-process; commit only after grading updates `Status = Graded`
  - [ ] Publish `GradeCalculatedEvent` after successful grading

- [ ] **ForceSubmitSession Command**:
  - [ ] Validate `Status = InProgress`
  - [ ] Set `EndTime = NOW`, `SubmissionType = TimeoutSubmit` for timer expiry or `SystemSubmit` for system/proctor submit
  - [ ] Save current auto-save state
  - [ ] Invoke Grading module in-process; commit only after grading updates `Status = Graded`

- [ ] **ReportSessionQuestion Command** (UC-48):
  - [ ] Delegates to QuestionBank module's `ReportQuestionCommand`
  - [ ] Pass `SessionID` context for audit

- [ ] **GetDetailedSolution Query** (UC-50):
  - [ ] Validate session `Status = Graded` (reject with 403 if not)
  - [ ] Return: questions with `QuestionContent`, `answers`, `IsCorrect` per answer, `PointsEarned`, and rich-text/plain-text explanation
  - [ ] Include `TestAnswer.ShortAnswerText` for SHORT_ANSWER type

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
