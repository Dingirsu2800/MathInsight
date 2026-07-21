# Tasks Checklist: Grading & Analytics Module

**Branch**: `004-grading-analytics` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] No owned tables — this module cross-reads current DB script tables owned by Testing and QuestionBank.
- [x] Configure read access to `TestSession`, `TestAnswer`, `TestAnswerOption`.
- [x] Configure read access to `Question` (`DefaultPoint`) and `Answer` (`IsCorrect`).
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

- [x] **GradingOrchestrator** (`IGradingOrchestrator`):
  - [x] Core grading flow shared by both MediatR handler (Practice) and MassTransit consumer (Exam)
  - [x] Load `TestSession` with all navigation properties: `TestAnswers` → `Question` → `Answers`, `Parts`, `QuestionTopics`; `SelectedOptions`; `AnswerParts` → `QuestionPart`
  - [x] Validate `TestSession.status = InProgress`
  - [x] Run `GradingEngine.Grade()` synchronously
  - [x] Write results in single transaction (DC-05): update `TestAnswer` + `TestAnswerPart` + `TestSession`
  - [x] Set `TestSession.status = Graded`; preserve `submission_type` from Testing
  - [x] Build and return `GradeCalculatedEvent` (G3) for downstream publishing

- [x] **GradeSubmittedSessionHandler** (MediatR, Practice mode):
  - [x] `INotificationHandler<TestSubmittedEvent>` — called in-process by Testing submit flow
  - [x] Delegates to `IGradingOrchestrator.GradeSessionAsync()`
  - [x] Publishes `GradeCalculatedEvent` via MediatR after grading completes
  - [x] SLA: `Practice` < 2.0 seconds

- [x] **TestSubmittedConsumer** (MassTransit, Exam mode):
  - [x] `IConsumer<TestSubmittedEvent>` — receives messages from MassTransit queue
  - [x] Delegates to `IGradingOrchestrator.GradeSessionAsync()` (same shared logic)
  - [x] Publishes `GradeCalculatedEvent` via MediatR after grading completes
  - [x] Idempotent: orchestrator checks `session.Status == InProgress` before grading
  - [x] SLA: `Exam` target < 5.0 seconds

- [x] **Transactional Atomicity** (DC-05):
  - [x] Wrap grading writes + session status update in single `using var tx = db.BeginTransaction()`
  - [x] On failure: rollback → session stays `InProgress`, not `Graded`
  - [x] Log failure with structured logging (session_id, error)

- [x] **ChatbotService** (UC-51):
  - [x] Implement `IChatbotService.AskAsync(questionContent, studentAnswer, studentId, sessionId)`
  - [x] POST to Google Gemini API (`gemini-2.0-flash` model) with system instruction: "math tutor, explain step-by-step in clear natural language; use simple Unicode/plain-text math notation where needed"
  - [x] Apply 10-second HttpClient timeout, Polly circuit breaker (3 fails = open 30s)
  - [x] Enforce 1 request per student per session using **in-memory rate limiter** (A2 — MVP only).
    - `ConcurrentDictionary<(Guid, Guid), DateTime>` keyed by `(studentId, sessionId)`.
    - TTL-based eviction: entries older than 1 hour cleaned up every 10 minutes.
    - **Do NOT use Redis** for this in MVP — Constitution §IV prohibits Redis unless spec-backed.
  - [x] Return explanation string — do NOT persist to database (BR-21)
  - [x] Throw `ChatbotRateLimitException` on duplicate requests → controller maps to HTTP 429


- [x] **Polly Retry Policy** (U2 — per Assumptions:L96):
  - [x] Configure Polly retry on grading DB transaction: 3 retries with exponential backoff (1s, 2s, 4s)
  - [x] On all retries exhausted: rollback transaction, log structured error, return failure to caller (session stays `InProgress`)

- [x] **GradeCalculatedEvent Contract** (G3):
  - [x] Use `MathInsight.Shared.Events.GradeCalculatedEvent` — do NOT define a separate local copy
  - [x] Populate `PerTagResults` from grading output: one `TopicGradeResult(TagId, TopicScore, CorrectCount, TotalCount)` per distinct **primary** tag in session. TopicScore calculated as `CorrectCount / TotalCount × 10.0`. Multi-tag `PerTagResults` (all tags incl. secondary) deferred to Phase 6.
  - [x] Populate `GradedAnswerDto.TagId` with the question's **primary** topic tag (`QuestionTopic.TagID WHERE IsPrimary = true`).
  - [x] Populate `GradedAnswerDto.IsAbandoned` using same `IsAbandoned()` logic as `GradingEngine`
  - [x] Populate `NumAbandoned` from count of unanswered/abandoned answers per BR-16b
  - [x] Publish via MediatR `IPublisher.Publish(event)` after transaction commit (not before)
  - [x] Note: `GradedAnswerDto.TagWeights`, `NormalizedScore`, and `MaxPoints` are defined in the shared event contract but **not yet populated** by `GradingOrchestrator.BuildGradeCalculatedEvent()` — deferred to Phase 6

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

---

## Phase 6: Multi-Tag PerTagResults & GradedAnswerDto Enhancement (Future)

> **Dependency note**: Module 005 (Recommender) Phase 5 multi-tag Elo v4.1 is already implemented and uses `TagWeights` with a backward-compatible fallback when `TagWeights` is empty. This phase completes the data flow by having `GradingOrchestrator` populate the fields.

- [x] **Populate `TagWeights` in `GradedAnswerDto`**:
  - [x] In `GradingOrchestrator.BuildGradeCalculatedEvent()`, read `QuestionTopics` for each answer
  - [x] For single-tag questions: `TagWeights = [{ TagId, Weight = 1.0, IsPrimary = true }]`
  - [x] For multi-tag questions: apply weight formula BR-13/14/15 (`w_main ∈ [0.60, 0.70]`, `w_sub_i = (1 − w_main) / N_sub`)
  - [x] Sum of all weights must equal 1.0

- [x] **Populate `NormalizedScore` in `GradedAnswerDto`**:
  - [x] `NormalizedScore = PointsEarned / MaxPoints × 10.0`

- [x] **Populate `MaxPoints` in `GradedAnswerDto`**:
  - [x] `MaxPoints = Question.DefaultPoint`

- [x] **Expand `PerTagResults` to all tags**:
  - [x] Include entries for secondary tags (not just primary)
  - [x] Use weighted Tầng 1–2 formula: `T_j^{(i)} = avg(c_{q,i})` where `c_{q,i} = s_q × w_{iq}`
