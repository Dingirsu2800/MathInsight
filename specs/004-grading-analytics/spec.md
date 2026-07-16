# Feature Specification: Grading & Analytics Module

**Feature Branch**: `004-grading-analytics`

**Created**: 2026-06-23 | **Updated**: 2026-07-14

**Status**: Approved

**Source Documents**: PRD §4 (FT-05), UCS UC-49, UC-50, UC-51, UC-55, UC-56, TDS §2.4, §4.7

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-49 | Submit Test/Question (Auto-grading trigger) | System | Called by Testing submit flow |
| UC-50 | View Detailed Solution | Student | After session `status = Graded` |
| UC-51 | Ask Chatbot for Assistance | Student | Student requests AI explanation |
| UC-55 | View Session Result | Student | Student navigates to result page after grading |
| UC-56 | View Test History | Student | Student navigates to history page |

---

## UC-55: View Session Result

**Endpoint**: `GET /api/v1/grading/sessions/{sessionId}`
**Auth**: `[Authorize(Roles = "Student")]`  
**Actor**: Student — only the session owner may access their own session result.

### Request
```
GET /api/v1/grading/sessions/{sessionId:guid}
Authorization: Bearer <jwt>
```

### Response — `SessionResultDto`

| Field | Type | Source |
|-------|------|--------|
| `sessionId` | `Guid` | `TestSession.SessionId` |
| `testId` | `Guid` | `TestSession.TestId` |
| `testFormat` | `string` | `Practice \| Exam` |
| `status` | `string` | `InProgress \| Graded \| Abandoned` |
| `score` | `decimal` | 0.00–10.00 |
| `numCorrect` | `int` | |
| `numIncorrect` | `int` | |
| `numAbandoned` | `int` | |
| `totalQuestion` | `int` | |
| `durationMinutes` | `int?` | `TestSession.Duration` |
| `submittedAt` | `DateTime?` | `TestSession.EndTime` |
| `answers` | `GradedAnswerDetailDto[]` | From `TestAnswer` join |

**`GradedAnswerDetailDto`**:

| Field | Type | Source |
|-------|------|--------|
| `questionId` | `Guid` | |
| `questionNo` | `int` | |
| `questionType` | `string` | |
| `questionContent` | `string` | `Question.QuestionContent` |
| `difficultyLevel` | `byte` | |
| `isCorrect` | `bool?` | Null when not yet graded |
| `pointsEarned` | `decimal` | |
| `maxPoints` | `decimal` | `Question.DefaultPoint` |
| `timeSpent` | `int?` | seconds |
| `selectedOptionId` | `Guid?` | For SINGLE_CHOICE / TRUE_FALSE |
| `shortAnswerText` | `string?` | For SHORT_ANSWER |
| `selectedOptionIds` | `Guid[]` | For MULTIPLE_SELECT |
| `answerParts` | `AnswerPartDetailDto[]` | For COMPOSITE |

**`AnswerPartDetailDto`**:

| Field | Type | Source |
|-------|------|--------|
| `questionPartId` | `Guid` | |
| `partType` | `string` | |
| `studentAnswer` | `string?` | |
| `isCorrect` | `bool?` | |
| `pointsEarned` | `decimal` | |

### Business Rules
- **BR-UC55-01**: Only the student who owns the session (`TestSession.StudentId == authenticatedStudentId`) may access it → `403 Forbidden` otherwise.
- **BR-UC55-02**: Session not found → `404 Not Found`.
- **BR-UC55-03**: Session status `InProgress` → return partial data (answers with `isCorrect = null`). Do not block the read.

---

## UC-56: View Test History

**Endpoint**: `GET /api/v1/grading/student/history`
**Auth**: `[Authorize(Roles = "Student")]`  
**Actor**: Student — returns only the authenticated student's sessions.

### Request — Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `page` | `int` | No | Default `1` |
| `pageSize` | `int` | No | Default `20`, max `100` |
| `testFormat` | `string?` | No | Filter by `Practice` or `Exam` |
| `fromDate` | `DateTime?` | No | Filter from date (inclusive, UTC) |
| `toDate` | `DateTime?` | No | Filter to date (inclusive, UTC) |

### Response — `PagedResult<SessionHistoryDto>`

```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 128,
  "totalPages": 7,
  "items": [ ... ]
}
```

**`SessionHistoryDto`**:

| Field | Type | Source |
|-------|------|--------|
| `sessionId` | `Guid` | |
| `testId` | `Guid` | |
| `testFormat` | `string` | `Practice \| Exam` |
| `status` | `string` | `Graded \| Abandoned` |
| `score` | `decimal` | 0.00–10.00 |
| `numCorrect` | `int` | |
| `numIncorrect` | `int` | |
| `numAbandoned` | `int` | |
| `totalQuestion` | `int` | |
| `durationMinutes` | `int?` | |
| `submittedAt` | `DateTime?` | `TestSession.EndTime` |
| `submissionType` | `string?` | `StudentSubmit \| TimeoutSubmit \| SystemSubmit` |

### Aggregate Stats Endpoint

**Endpoint**: `GET /api/v1/grading/student/stats`  
Returns aggregate statistics computed from the student's graded sessions.

**Response — `StudentHistoryStatsDto`**:

| Field | Type | Description |
|-------|------|-------------|
| `totalSessions` | `int` | All graded sessions |
| `sessionsLast30Days` | `int` | Sessions in the last 30 days |
| `averageScore` | `decimal` | Average score across all graded sessions |
| `accuracyPercent` | `decimal` | `SUM(numCorrect) / SUM(totalQuestion) × 100` |

### Business Rules
- **BR-UC56-01**: Only sessions with `Status = Graded` are returned (exclude `InProgress`, `Abandoned`).
- **BR-UC56-02**: Results ordered by `EndTime DESC` (newest first).
- **BR-UC56-03**: `pageSize` capped at `100` — larger values are clamped silently.

### Grading Modes

| Mode | When | Mechanism | SLA |
|------|------|-----------|-----|
| **Practice** | `test_format = Practice` | Synchronous, in-process | < 2.0 seconds |
| **Exam** | `test_format = Exam` | Synchronous for MVP | < 5.0 seconds |

### Edge Cases

- **Partial submission**: Abandoned questions (where no answer is provided, per BR-16b) are graded as incorrect (`is_correct = false`, `points_earned = 0.00`).
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
- **BR-20**: Score formula: `score = SUM(points_earned) / SUM(max_points) × 10.0` — normalized to a 0–10 scale.
  `max_points` for each parent question is defined as `Question.default_point`.
- **BR-21**: AI Chatbot (UC-51) is called via the OpenAI/Claude REST API. Chatbot input: the stored question content + student's selected answer. Response includes a step-by-step explanation written in natural language with simple math notation suitable for students. Chatbot response is **not persisted** to the database.
- **BR-22**: After grading completes, `GradeCalculatedEvent` is published in-process (MediatR). It has **two consumers**:
  1. **Recommender module (005)** — updates `StudentTopicSessionResult` and `TagsMastery` per topic (idempotent).
  2. **Notification module (008)** — sends a "test graded" push notification to the student.

  Event payload (`MathInsight.Shared.Events.GradeCalculatedEvent`):

  | Field | Type | Description |
  |-------|------|-------------|
  | `SessionId` | `string` | Graded session; canonical SQL `VARCHAR(36)` identifier |
  | `StudentId` | `string` | Owner student; semantic IDs are supported |
  | `TestId` | `string` | Parent test |
  | `TestFormat` | `string` | Test format (`Practice` or `Exam`) |
  | `Score` | `decimal` | 0.00–10.00 normalized score (BR-20) |
  | `NumCorrect` | `int` | Count of correct answers |
  | `NumIncorrect` | `int` | Count of incorrect answers |
  | `NumAbandoned` | `int` | Count of unanswered/abandoned questions (per BR-16b) |
  | `PerTagResults` | `IReadOnlyList<TopicGradeResult>` | One entry per primary TagId: `(TagId, TotalItems, CorrectItems, EarnedPoints, MaxPoints, TopicScore)`, where `TopicScore = EarnedPoints / MaxPoints * 10`. **MVP rule**: group by `QuestionTopic.TagID WHERE IsPrimary = true` only. Multi-tag analytics deferred post-MVP. |
  | `Answers` | `IReadOnlyList<GradedAnswerDto>` | Detailed list of graded answers for Elo calculation (F1 resolution) |
  | `GradedAt` | `DateTime` | UTC timestamp |

  `GradedAnswerDto` contains:
  - `QuestionId` (`string`)
  - `TagId` (`string`) — **primary topic tag** of the question (`QuestionTopic.TagID WHERE IsPrimary = true`). MVP uses one tag per question for grading/recommender; multi-tag support is post-MVP.
  - `IsCorrect` (`bool`)
  - `PointsEarned` (`decimal`)
  - `MaxPoints` (`decimal`) — required to preserve partial credit in per-topic aggregation.
  - `TimeSpent` (`int`)
  - `DifficultyLevel` (`byte` - value 1..4)
  - `QuestionNo` (`int`)
  - `IsAbandoned` (`bool`) — true if the question is abandoned/unanswered (per BR-16b)

  `TopicGradeResult` contains:
  - `TagId` (`string`)
  - `TotalItems` (`decimal`)
  - `CorrectItems` (`decimal`)
  - `EarnedPoints` (`decimal`)
  - `MaxPoints` (`decimal`)
  - `TopicScore` (`decimal`) — `EarnedPoints / MaxPoints * 10`, rounded to two decimals.

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

  **Child part scoring rule (all-TRUE_FALSE)**: `TestAnswer.points_earned` is the **source of truth** for score calculation (per the non-linear table above). `TestAnswerPart.is_correct` is recorded individually for solution display purposes. `TestAnswerPart.points_earned` is set to **0** for all parts — it is **not** used for score calculation in this mode. This avoids the ambiguity of trying to distribute the non-linear parent score back to individual parts.

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
- Chatbot rate limiter (UC-51): **in-memory only for MVP** — keyed by `(studentId, sessionId)`. Redis is explicitly excluded per Constitution §IV; it may be introduced only when multi-instance deployment becomes a spec-backed requirement.
