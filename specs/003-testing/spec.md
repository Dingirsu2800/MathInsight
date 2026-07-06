# Feature Specification: Testing Module

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
→ Student submits → session locked → GradingModule runs in the same submit flow
→ session becomes `Graded` → Student views solution
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
- **BR-16**: `TestAnswer.points_earned` is populated during grading (module 004). Submit returns only after grading succeeds and `status = Graded`.
- **BR-16a**: `TestSession.submission_type` stores how the session was submitted: `StudentSubmit`, `TimeoutSubmit`, or `SystemSubmit`. It is required when `status = Graded` and must be null while `status = InProgress` or `Abandoned`.

### Key Entities *(include if feature involves data)*

- **Test**: `test_id`, `blueprint_id` (FK → blueprints, nullable), `test_format` (**Practice** | **Exam**), `generated_for_student_id` (FK → students, nullable), `generated_by` (default 'System'), `test_status` (**ACTIVE** | **ARCHIVED**), `test_name`, `test_code` (nullable; unique when not null), `duration_minutes`, `total_questions`, `created_time`
- **TestQuestion**: `test_id` (FK), `question_id` (FK), `question_order` — composite PK
- **TestSession**: `session_id`, `test_id` (FK), `student_id` (FK), `test_format` (**Practice** | **Exam**), `status` (**InProgress** | **Graded** | **Abandoned**), `submission_type` (**StudentSubmit** | **TimeoutSubmit** | **SystemSubmit**, nullable), `duration`, `start_time`, `end_time`, `total_question`, `num_correct`, `num_incorrect`, `num_abandoned`, `score`
- **TestAnswer**: `test_answer_id`, `session_id` (FK), `question_id` (FK), `answer_id` (FK, nullable for MultipleSelect/ShortAnswer), `question_no`, `time_spent`, `first_choice_time`, `update_choice_time`, `short_answer_text`, `is_correct` (nullable until graded), `points_earned` (0.00 until graded)
- **TestAnswerOption**: `test_answer_id` (FK, PK), `answer_id` (FK, PK) — for MultipleSelect
- **TestAnswerPart**: `test_answer_part_id` (PK), `test_answer_id` (FK), `question_part_id` (FK), `answer_id` (FK, nullable for ShortAnswer), `short_answer_text` (nullable), `is_correct` (nullable until graded), `points_earned` (0.00 until graded) — for Composite parts
- **TestIncident**: `incident_id`, `session_id` (FK), `type` (TAB_SWITCH | FOCUS_LOSS), `time`


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
- MediatR event `TestSubmittedEvent` can be used inside the submit flow, but it is a transient domain event, not a persisted `Submitted` status.
- Real-time timer sync may use SignalR or server-side session expiry check on every auto-save request.
- `TestGen` module (009) is responsible for creating the `Test` and `TestQuestion` records before session start.
