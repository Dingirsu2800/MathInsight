# Tasks Checklist: Grading & Analytics Module

## Scoring Contract V2

- [x] Grade immutable QuestionVersion data with TestQuestion scoring snapshots.
- [x] Preserve machine points and calculate effective invalidated points.
- [x] Recalculate version-wide affected sessions idempotently and increment GradeRevision.
- [x] Publish revision-aware weighted topic results.

**Branch**: `004-grading-analytics` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] No owned tables — this module cross-reads current DB script tables owned by Testing and QuestionBank.
- [x] Configure read access to `TestSession`, `TestAnswer`, `TestAnswerOption`.
- [x] Configure legacy fallback access to `Question.DefaultWeight`; normal grading reads immutable `QuestionVersion` and `TestQuestion` scoring snapshots.
- [x] Configure read/write access to `TestAnswerPart` (`is_correct`, `points_earned`).
- [x] Configure read access to `QuestionPart` (`part_type`, `answer_key`, `default_point`).
- [x] Confirm shared `DbContext` strategy with Testing (003) and QuestionBank (002) modules

---

## Phase 2: Core Domain Logic

- [x] **GradingEngine** (`IGradingEngine`):
  - [x] `SINGLE_CHOICE` grading: compare `TestAnswer.answer_id` to the correct `Answer.answer_id`
  - [x] `MULTIPLE_SELECT` grading: compare selected `TestAnswerOption` set to the full correct answer set — all correct + no incorrect = true
  - [x] `TRUE_FALSE` grading (standalone): same as `SINGLE_CHOICE`
  - [x] `COMPOSITE` grading — general: grade each `QuestionPart`; `points_earned` = sum of correct part points
  - [x] `COMPOSITE` grading — all-TRUE_FALSE parts (BR-23): count correct parts → look up non-linear table (0→0, 1→0.10×dp, 2→0.25×dp, 3→0.50×dp, N→1.00×dp); `is_correct` on parent = true only when all parts correct; each `TestAnswerPart.is_correct` still recorded individually for solution display; `TestAnswerPart.points_earned = 0` (parent `TestAnswer.points_earned` is the source of truth — do NOT distribute non-linear parent score to child parts)
  - [x] `SHORT_ANSWER` grading: `LOWER(short_answer_text) == LOWER(Answer.answer_content)` where `Answer.is_correct = true`
  - [x] Calculate `score = SUM(points_earned) / SUM(max_points) × 10.0` (BR-20)
  - [x] Count `num_correct`, `num_incorrect`, `num_abandoned` (abandoned per BR-16b)

- [x] **GradeSubmittedSessionHandler** (MVP synchronous):
  - [x] Called in-process by Testing submit/force-submit flow
  - [x] Validate `TestSession.status = InProgress`
  - [x] Run `GradingEngine.Grade()` synchronously for `Practice` and `Exam`
  - [x] Write results in the same transaction as submission (DC-05): update `test_answers` + `test_sessions`
  - [x] Set `TestSession.status = Graded`; preserve `submission_type` from Testing (`StudentSubmit`, `TimeoutSubmit`, `SystemSubmit`)
  - [x] Publish `GradeCalculatedEvent` after commit
  - [x] SLA: `Practice` < 2.0 seconds, `Exam` target < 5.0 seconds

- [x] **Transactional Atomicity** (DC-05):
  - [x] Wrap grading writes + session status update in single `using var tx = db.BeginTransaction()`
  - [x] On failure: rollback → session stays `InProgress`, not `Graded`
  - [x] Log failure with structured logging (session_id, error)

- [x] **ChatbotService** (UC-51):
  - [x] Implement `IChatbotService.AskAsync(questionContent, studentAnswer)`
  - [x] POST to Gemini API with system prompt: "math tutor, explain step-by-step in clear natural language; use simple Unicode/plain-text math notation where needed"
  - [x] Apply 10-second timeout, Polly circuit breaker (3 fails = open 30s)
  - [x] Enforce 1 request per student per session using **in-memory rate limiter** (A2 — MVP only).
    - Keyed by `(studentId, sessionId)`; TTL-based or flag per request scope.
    - **Do NOT use Redis** for this in MVP — Constitution §IV prohibits Redis unless spec-backed. Redis becomes relevant only under multi-instance deployment (post-MVP).
  - [x] Return explanation string — do NOT persist to database (BR-21)

- [x] **Polly Retry Policy** (U2 — per Assumptions:L96):
  - [x] Configure Polly retry on grading DB transaction: 3 retries with exponential backoff (1s, 2s, 4s)
  - [x] On all retries exhausted: rollback transaction, log structured error, return failure to caller (session stays `InProgress`)

- [x] **GradeCalculatedEvent Contract** (G3):
  - [x] Use `MathInsight.Shared.Events.GradeCalculatedEvent` — do NOT define a separate local copy
  - [x] Populate `PerTagResults` from grading output: one `TopicGradeResult(TagId, TopicScore, CorrectCount, TotalCount)` per distinct **primary** tag in session. Use `QuestionTopic.TagID WHERE IsPrimary = true` for each question. Multi-tag analytics deferred post-MVP.
  - [x] Populate `GradedAnswerDto.TagId` with the question's **primary** topic tag (`QuestionTopic.TagID WHERE IsPrimary = true`).
  - [x] Populate `NumAbandoned` from count of unanswered/abandoned answers per BR-16b
  - [x] Publish via MediatR `IPublisher.Publish(event)` after transaction commit (not before)

---

## Phase 3: Controller and Routing

- [x] `GradingController`:
  - [x] `POST /api/v1/chatbot/assist` — StudentOnly, UC-51
  - [x] Accepts `{ sessionId, questionId, studentAnswer }` in request body
  - [x] Returns `{ explanation: "..." }` as JSON

- [x] Register inside `GradingModuleExtensions.cs`:
  - GradingEngine, ChatbotService, MediatR handlers

---

## Phase 4: Verification

- [x] `dotnet build` — zero compile errors
- [x] Integration tests (xUnit):
  - [x] Practice: 40-question session graded in < 2.0s
  - [x] Exam: session graded synchronously and persisted as `Graded`
  - [x] SINGLE_CHOICE correct → `is_correct = true`, `points_earned = default_point`
  - [x] MULTIPLE_SELECT partial → `is_correct = false`, `points_earned = 0`
  - [x] SHORT_ANSWER case-insensitive match → `is_correct = true`
  - [x] Abandoned answer (per BR-16b) → `is_correct = false`, counted in `num_abandoned`
  - [x] COMPOSITE all-TRUE_FALSE — 0 correct → `points_earned = 0` (BR-23)
  - [x] COMPOSITE all-TRUE_FALSE — 1/N correct → `points_earned = 0.10 × default_point` (BR-23)
  - [x] COMPOSITE all-TRUE_FALSE — 2/N correct → `points_earned = 0.25 × default_point` (BR-23)
  - [x] COMPOSITE all-TRUE_FALSE — 3/N correct → `points_earned = 0.50 × default_point` (BR-23)
  - [x] COMPOSITE all-TRUE_FALSE — N/N correct → `points_earned = default_point`; `is_correct = true` (BR-23)
  - [x] COMPOSITE general (mixed parts) — parent score = sum of part points earned
  - [x] DC-05: Simulated DB failure mid-grade → rollback, session stays `InProgress`
  - [x] UC-51: Chatbot returns explanation JSON within 10s (happy path)
  - [x] UC-51: Chatbot API times out after 10s → endpoint returns structured error (e.g. 503); student session is NOT affected (U1)
  - [x] UC-51: Second chatbot call same session → rate limited (429)

---

## Phase 5: Query Endpoints (UC-55, UC-56) — 2026-07-14

### 5.1 DTOs

- [x] `SessionResultDto.cs` — `SessionResultDto`, `GradedAnswerDetailDto`, `AnswerPartDetailDto`
- [x] `SessionHistoryDto.cs` — `SessionHistoryDto`, `StudentHistoryStatsDto`, `PagedResult<T>`

### 5.2 Queries — UC-55 (View Session Result)

- [x] `GetSessionResultQuery.cs` — MediatR query record `GetSessionResultQuery(Guid SessionId, Guid AuthenticatedStudentId)`
- [x] `GetSessionResultQueryHandler.cs`:
  - [x] Load `TestSession` + `TestAnswers` + nav props via EF
  - [x] Guard: `StudentId != authenticatedStudentId` → throw `UnauthorizedAccessException` → controller maps to 403
  - [x] Guard: session not found → return `null` → controller maps to 404
  - [x] Map to `SessionResultDto` (answers ordered by `QuestionNo ASC`)

### 5.3 Queries — UC-56 (View Test History + Stats)

- [x] `GetSessionHistoryQuery.cs` — `GetSessionHistoryQuery(Guid StudentId, int Page, int PageSize, string? TestFormat, DateTime? FromDate, DateTime? ToDate)`
- [x] `GetSessionHistoryQueryHandler.cs`:
  - [x] Filter `Status == "Graded"`, `StudentId`, optional format/date filters
  - [x] Order by `EndTime DESC`
  - [x] Return `PagedResult<SessionHistoryDto>`
- [x] `GetStudentHistoryStatsQuery.cs` — `GetStudentHistoryStatsQuery(Guid StudentId)`
- [x] `GetStudentHistoryStatsQueryHandler.cs`:
  - [x] Compute `totalSessions`, `sessionsLast30Days`, `averageScore`, `accuracyPercent`
  - [x] Return `StudentHistoryStatsDto`

### 5.4 Controller — `StudentGradingController`

- [x] New controller `StudentGradingController` at route `api/v1/grading`
- [x] `GET /api/v1/grading/sessions/{sessionId}` — UC-55
- [x] `GET /api/v1/grading/student/history` — UC-56
- [x] `GET /api/v1/grading/student/stats` — UC-56

### 5.5 Verification

- [x] `dotnet build` — zero compile errors
