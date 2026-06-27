# Feature Specification: Grading & Analytics Module

**Feature Branch**: `004-grading-analytics`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-05), UCS UC-49, UC-50, UC-51, TDS §2.4, §4.7

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-49 | Submit Test/Question (Auto-grading trigger) | System | `TestSubmittedEvent` consumed from Testing module |
| UC-50 | View Detailed Solution | Student | After session `status = GRADED` |
| UC-51 | Ask Chatbot for Assistance | Student | Student requests AI explanation |

### Grading Modes

| Mode | When | Mechanism | SLA |
|------|------|-----------|-----|
| **Practice** | `test_format = PRACTICE` | Real-time (synchronous, in-process) | < 2.0 seconds |
| **Exam** | `test_format = EXAM` | Deferred (RabbitMQ `background_grading_queue`) | < 60.0 seconds |

### Edge Cases

- **Partial submission**: Questions with null `answer_id` are graded as incorrect (`is_correct = false`, `points_earned = 0.00`).
- **Multiple-select grading**: All correct options must be selected AND no incorrect options — otherwise `is_correct = false`.
- **Short answer grading**: Case-insensitive string match against `correct_answer` stored in `Answer.answer_content`.
- **Grading failure**: If grading fails mid-transaction → rollback entire state (DC-05). Retry via Polly.
- **Double-grading prevention**: If `TestSession.status = GRADED` already → skip; log warning.

## Requirements *(mandatory)*

### Functional Requirements

- **DC-03**: Submitted/force-submitted test answers are read-only. Grading only **reads** `TestAnswer` records and **writes** grading result fields (`is_correct`, `points_earned`).
- **DC-05**: Auto-grading, updating `TagsMastery` competency points, and logging activity must execute as a **single database transaction**. Any failure triggers full rollback.
- **BR-17 (Practice grading)**: For PRACTICE mode, grading must complete within **2.0 seconds** end-to-end from receiving `TestSubmittedEvent`.
- **BR-18 (Exam grading)**: For EXAM mode, grading is processed asynchronously via RabbitMQ `background_grading_queue`. Must complete within **60 seconds**.
- **BR-19**: After grading completes, the `TestSession.status` is updated to `GRADED`; `num_correct`, `num_incorrect`, `num_abandoned`, and `score` are calculated and persisted.
- **BR-20**: Score formula: `score = SUM(points_earned) / total_questions × 10.0` — normalized to a 0–10 scale.
- **BR-21**: AI Chatbot (UC-51) is called via the OpenAI/Claude REST API. Chatbot input: the question content (LaTeX) + student's selected answer. Response includes step-by-step explanation. Chatbot response is **not persisted** to the database.
- **BR-22**: Competency recalculation is delegated to the Recommender module (005) via `GradeCalculatedEvent` published after grading completes.

### Grading Algorithm per Question Type

| Type | Grading Logic |
|------|---------------|
| `SINGLE_CHOICE` | `is_correct = (student_answer_id == correct_answer_id)` |
| `MULTIPLE_SELECT` | `is_correct = (all correct options selected AND no incorrect options selected)` |
| `TRUE_FALSE` | Same as SINGLE_CHOICE |
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
- Exam grading completes in < **60 seconds** via RabbitMQ (BR-18, NFR-P03).
- Grading transaction is atomic — no partial state on failure (DC-05).
- Chatbot response returns within **10 seconds** (NFR for UC-51).
- No separate `grd` schema is created for MVP; this module maps to current DB script tables owned by Testing and QuestionBank.

## Assumptions

- Target database is SQL Server.
- MediatR publishes `TestSubmittedEvent` from Testing module (003) — this module is the consumer.
- `background_grading_queue` is provisioned in RabbitMQ via MassTransit.
- Polly retry policy: 3 retries with exponential backoff for grading transaction failures.
- Chatbot integration uses OpenAI or Claude API; credentials injected via environment variables.
