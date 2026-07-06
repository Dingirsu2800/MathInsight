# Feature Specification: Grading & Analytics Module

**Feature Branch**: `004-grading-analytics`

**Created**: 2026-06-23 | **Updated**: 2026-06-30

**Status**: Approved

**Source Documents**: PRD §4 (FT-05), UCS UC-49, UC-50, UC-51, TDS §2.4, §4.7

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-49 | Submit Test/Question (Auto-grading trigger) | System | Called by Testing submit flow |
| UC-50 | View Detailed Solution | Student | After session `status = Graded` |
| UC-51 | Ask Chatbot for Assistance | Student | Student requests AI explanation |

### Grading Modes

| Mode | When | Mechanism | SLA |
|------|------|-----------|-----|
| **Practice** | `test_format = Practice` | Synchronous, in-process | < 2.0 seconds |
| **Exam** | `test_format = Exam` | Synchronous for MVP | < 5.0 seconds |

### Edge Cases

- **Partial submission**: Questions with null `answer_id` are graded as incorrect (`is_correct = false`, `points_earned = 0.00`).
- **Multiple-select grading**: All correct options must be selected AND no incorrect options — otherwise `is_correct = false`.
- **Short answer grading**: Case-insensitive string match against `correct_answer` stored in `Answer.answer_content`.
- **Grading failure**: If grading fails mid-transaction → rollback entire submit transaction (DC-05). The session remains `InProgress` so the student can retry submit.
- **Double-grading prevention**: If `TestSession.status = Graded` already → skip/reject; log warning.

## Requirements *(mandatory)*

### Functional Requirements

- **DC-03**: Once a session leaves `InProgress`, test answers are read-only. Grading writes only grading result fields (`is_correct`, `points_earned`) inside the submit transaction.
- **DC-05**: Auto-grading writes (`TestAnswer` result fields and `TestSession` score/status) must execute as a **single database transaction**. Recommender updates are triggered after successful grading and must be idempotent through `StudentTopicSessionResult`.
- **BR-17 (Practice grading)**: For `Practice` mode, grading must complete within **2.0 seconds** end-to-end from submit.
- **BR-18 (Exam grading MVP)**: For `Exam` mode, grading is synchronous in MVP because `Submitted` is not persisted as a durable DB state. If the team later needs async grading, add a `PendingGrading` status or a separate grading job table first.
- **BR-19**: After grading completes, `TestSession.status` is updated to `Graded`; `submission_type`, `num_correct`, `num_incorrect`, `num_abandoned`, and `score` are calculated and persisted.
- **BR-20**: Score formula: `score = SUM(points_earned) / total_questions × 10.0` — normalized to a 0–10 scale.
- **BR-21**: AI Chatbot (UC-51) is called via the OpenAI/Claude REST API. Chatbot input: the stored question content + student's selected answer. Response includes a step-by-step explanation written in natural language with simple math notation suitable for students. Chatbot response is **not persisted** to the database.
- **BR-22**: After grading completes, `GradeCalculatedEvent` is published in-process (MediatR). It has **two consumers**:
  1. **Recommender module (005)** — updates `StudentTopicSessionResult` and `TagsMastery` per topic (idempotent).
  2. **Notification module (008)** — sends a "test graded" push notification to the student.

  Event payload (`MathInsight.Shared.Events.GradeCalculatedEvent`):

  | Field | Type | Description |
  |-------|------|-------------|
  | `SessionId` | `Guid` | Graded session |
  | `StudentId` | `Guid` | Owner student |
  | `TestId` | `Guid` | Parent test |
  | `Score` | `decimal` | 0.00–10.00 normalized score (BR-20) |
  | `NumCorrect` | `int` | Count of correct answers |
  | `NumIncorrect` | `int` | Count of incorrect answers |
  | `NumAbandoned` | `int` | Count of unanswered questions |
  | `PerTagResults` | `IReadOnlyList<TopicGradeResult>` | One entry per TagId: `(TagId, TopicScore, CorrectCount, TotalCount)` |
  | `GradedAt` | `DateTime` | UTC timestamp |

  Consumers must be **idempotent** — duplicate events for the same `SessionId` must be safe to ignore.
- **BR-23 (COMPOSITE True/False scoring)**: When a `COMPOSITE` question has **all `QuestionPart` rows with `part_type = TRUE_FALSE`**, the `points_earned` for the parent answer is determined by the **count of correct parts**, using the following non-linear table (relative to the question's `default_point`):

  | Correct parts | Points earned |
  |---------------|---------------|
  | 0             | 0.00          |
  | 1             | 0.10 × `default_point` |
  | 2             | 0.25 × `default_point` |
  | 3             | 0.50 × `default_point` |
  | N (all)       | 1.00 × `default_point` |

  This rule applies regardless of **which** specific parts are correct. `is_correct` on the parent `TestAnswer` is `true` only when all parts are correct. Each child `TestAnswerPart.is_correct` is still recorded individually for solution display.

### Grading Algorithm per Question Type

| Type | Grading Logic |
|------|---------------|
| `SINGLE_CHOICE` | `is_correct = (student_answer_id == correct_answer_id)` |
| `MULTIPLE_SELECT` | `is_correct = (all correct options selected AND no incorrect options selected)` |
| `TRUE_FALSE` | Same as `SINGLE_CHOICE` — standalone single-answer True/False question |
| `COMPOSITE (general)` | Grade each `QuestionPart` individually; `points_earned` = sum of part points earned |
| `COMPOSITE (all-TRUE_FALSE parts)` | Apply BR-23 non-linear scoring table based on count of correct parts |
| `SHORT_ANSWER` | Case-insensitive string match: `LOWER(short_answer_text) == LOWER(correct_answer_content)` |

### Key Entities *(read from Testing module)*

This module does **not own** additional tables. It reads and writes to:
- `TestSession` — updates `Status`, `NumCorrect`, `NumIncorrect`, `NumAbandoned`, `Score`
- `TestAnswer` — updates `IsCorrect`, `PointsEarned` per answer
- `Question` — reads `DefaultPoint` for scoring
- `Answer` — reads `IsCorrect` flag for reference key

Delegates competency updates to **Recommender module (005)** via `GradeCalculatedEvent`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Practice grading completes in < **2.0 seconds** (BR-17, NFR-P02).
- Exam grading completes synchronously for MVP, target < **5.0 seconds** (BR-18).
- Grading transaction is atomic — no partial state on failure (DC-05).
- Chatbot response returns within **10 seconds** (NFR for UC-51).
- No separate `grd` schema is created for MVP; this module maps to current DB script tables owned by Testing and QuestionBank.

## Assumptions

- Target database is SQL Server.
- Testing module (003) calls Grading in-process during submit; `TestSubmittedEvent` is optional/transient and must not imply a persisted `Submitted` status.
- No RabbitMQ grading queue is required for MVP under the current `TestSession.Status` design.
- Polly retry policy: 3 retries with exponential backoff for grading transaction failures.
- Chatbot integration uses OpenAI or Claude API; credentials injected via environment variables.
