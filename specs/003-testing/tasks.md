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

- [ ] **StartSession Command**:
  - [ ] Validate test `test_status = ACTIVE`
  - [ ] Check no existing `InProgress` session for same `(student_id, test_id)` (BR-15)
  - [ ] Create `TestSession` with `status = InProgress`, `submission_type = NULL`, `start_time = NOW`
  - [ ] Create `TestAnswer` stub records for each `TestQuestion` in the test
  - [ ] Return session with question list (randomized per blueprint)

- [ ] **AutoSave Command**:
  - [ ] Validate session `status = InProgress` (reject if `Graded` or `Abandoned`)
  - [ ] Batch update `TestAnswer`: `answer_id`, `selected_options`, `short_answer_text`, `time_spent` (received from Client payload for guessing penalty)
  - [ ] Update `update_choice_time`; set `first_choice_time` if null
  - [ ] Return `{ savedAt, remainingSeconds }` — remaining time from `start_time + duration_minutes`

- [ ] **RecordIncident Command**:
  - [ ] Insert `TestIncident` record with `type = TAB_SWITCH | FOCUS_LOSS`
  - [ ] Count incidents for session → if `>= 5`, call `ForceSubmitSessionCommand` (BR-10)

- [ ] **SubmitSession Command** (UC-49):
  - [ ] Validate `status = InProgress`
  - [ ] Lock answer writes inside the submit transaction
  - [ ] Set `end_time = NOW`, `submission_type = StudentSubmit`
  - [ ] Calculate `duration = (end_time - start_time).TotalSeconds`
  - [ ] Count `num_abandoned` (unanswered/abandoned questions per BR-16b)
  - [ ] **Practice mode (BR-16)**: Invoke Grading via MediatR in-process; commit only after grading updates `status = Graded`; return `200 OK`
  - [ ] **Exam mode (BR-17)**: Publish `TestSubmittedEvent` to MassTransit queue; return `202 Accepted` immediately
  - [ ] Publish `GradeCalculatedEvent` after successful grading (handled by Grading module)

- [ ] **ForceSubmitSession Command**:
  - [ ] Validate `status = InProgress`
  - [ ] Set `end_time = NOW`, `submission_type = TimeoutSubmit` for timer expiry or `SystemSubmit` for system/proctor submit
  - [ ] Save current auto-save state
  - [ ] **Practice mode**: Invoke Grading via MediatR in-process; commit only after grading updates `status = Graded`
  - [ ] **Exam mode**: Publish `TestSubmittedEvent` to MassTransit queue; grading proceeds asynchronously

- [ ] **ReportSessionQuestion Command** (UC-48):
  - [ ] Delegates to QuestionBank module's `ReportQuestionCommand`
  - [ ] Pass `session_id` context for audit

- [ ] **GetDetailedSolution Query** (UC-50):
  - [ ] Validate session `status = Graded` (reject with 403 if not)
  - [ ] Return: questions with `question_content`, `answers`, `is_correct` per answer, `points_earned`, and rich-text/plain-text explanation
  - [ ] Include `TestAnswer.short_answer_text` for SHORT_ANSWER type

---

## Phase 3: Controller and Routing

- [ ] `TestSessionsController` — StudentOnly: start, auto-save, incident, submit
- [ ] `SolutionController` — StudentOnly: GET solution (validates `Graded` status)
- [ ] Register all services inside `TestingModuleExtensions.cs`

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-47: Start session → `InProgress`, correct question count
  - [ ] UC-47: Start duplicate `InProgress` session → 409 (BR-15)
  - [ ] UC-47: Auto-save 5 answers → persisted, `update_choice_time` set
  - [ ] UC-47: 4 incidents → no force-submit; 5th incident → `Graded` with `submission_type = SystemSubmit`
  - [ ] UC-49: Normal submit → `Graded`, `submission_type = StudentSubmit`, grading fields populated
  - [ ] UC-49: Submit `Graded` session → 409 (DC-03)
  - [ ] UC-49: Submit with unanswered questions → `num_abandoned` = unanswered count
  - [ ] UC-50: View solution before `Graded` → 403
  - [ ] UC-50: View solution after `Graded` → full question/answer data returned
