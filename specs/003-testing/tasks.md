# Tasks Checklist: Testing Module

**Branch**: `003-testing` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for all 6 entities mapped to current DB script tables:
  - [ ] `TestConfiguration` — UNIQUE `test_code`; FK to `blueprints`
  - [ ] `TestQuestionConfiguration` — composite PK `(test_id, question_id)`, `question_order`
  - [ ] `TestSessionConfiguration` — composite index `(student_id, status)`; status enum constraint
  - [ ] `TestAnswerConfiguration` — composite UNIQUE `(session_id, question_id)`; `points_earned` default 0.00
  - [ ] `TestAnswerOptionConfiguration` — composite PK `(test_answer_id, answer_id)` (DC-07)
  - [ ] `TestIncidentConfiguration` — FK to `test_sessions`, `type` enum constraint
- [ ] Create `TestingDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [ ] Coordinate with TestGen (009) on shared current DB script tables — same DbContext or separate registrations.
- [ ] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 2 tests (1 ACTIVE, 1 ARCHIVED), 3 test sessions per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **StartSession Command**:
  - [ ] Validate test `test_status = ACTIVE`
  - [ ] Check no existing `IN_PROGRESS` session for same `(student_id, test_id)` (BR-15)
  - [ ] Create `TestSession` with `status = IN_PROGRESS`, `start_time = NOW`
  - [ ] Create `TestAnswer` stub records for each `TestQuestion` in the test
  - [ ] Return session with question list (randomized per blueprint)

- [ ] **AutoSave Command**:
  - [ ] Validate session `status = IN_PROGRESS` (reject if SUBMITTED/FORCE_SUBMITTED)
  - [ ] Batch update `TestAnswer`: `answer_id`, `selected_options`, `short_answer_text`
  - [ ] Update `update_choice_time`; set `first_choice_time` if null
  - [ ] Return `{ savedAt, remainingSeconds }` — remaining time from `start_time + duration_minutes`

- [ ] **RecordIncident Command**:
  - [ ] Insert `TestIncident` record with `type = TAB_SWITCH | FOCUS_LOSS`
  - [ ] Count incidents for session → if `>= 5`, call `ForceSubmitSessionCommand` (BR-10)

- [ ] **SubmitSession Command** (UC-49):
  - [ ] Validate `status = IN_PROGRESS`
  - [ ] Set `status = SUBMITTED`, `end_time = NOW`
  - [ ] Calculate `duration = (end_time - start_time).TotalSeconds`
  - [ ] Count `num_abandoned` (questions with null answer)
  - [ ] Publish `TestSubmittedEvent` via MediatR (→ Grading module, Gamification, Notification)

- [ ] **ForceSubmitSession Command**:
  - [ ] Set `status = FORCE_SUBMITTED`, `end_time = NOW`
  - [ ] Save current auto-save state
  - [ ] Publish `TestSubmittedEvent`

- [ ] **ReportSessionQuestion Command** (UC-48):
  - [ ] Delegates to QuestionBank module's `ReportQuestionCommand`
  - [ ] Pass `session_id` context for audit

- [ ] **GetDetailedSolution Query** (UC-50):
  - [ ] Validate session `status = GRADED` (reject with 403 if not)
  - [ ] Return: questions with `question_content`, `answers`, `is_correct` per answer, `points_earned`, and rich-text/plain-text explanation
  - [ ] Include `TestAnswer.short_answer_text` for SHORT_ANSWER type

---

## Phase 3: Controller and Routing

- [ ] `TestSessionsController` — StudentOnly: start, auto-save, incident, submit
- [ ] `SolutionController` — StudentOnly: GET solution (validates GRADED status)
- [ ] Register all services inside `TestingModuleExtensions.cs`

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-47: Start session → `IN_PROGRESS`, correct question count
  - [ ] UC-47: Start duplicate IN_PROGRESS session → 409 (BR-15)
  - [ ] UC-47: Auto-save 5 answers → persisted, `update_choice_time` set
  - [ ] UC-47: 4 incidents → no force-submit; 5th incident → `FORCE_SUBMITTED`
  - [ ] UC-49: Normal submit → `SUBMITTED`, `TestSubmittedEvent` published
  - [ ] UC-49: Submit SUBMITTED session → 409 (DC-03)
  - [ ] UC-49: Submit with unanswered questions → `num_abandoned` = correct count
  - [ ] UC-50: View solution before GRADED → 403
  - [ ] UC-50: View solution after GRADED → full question/answer data returned
