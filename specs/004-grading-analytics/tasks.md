# Tasks Checklist: Grading & Analytics Module

**Branch**: `004-grading-analytics` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] No owned tables — this module cross-reads current DB script tables owned by Testing and QuestionBank.
- [x] Configure read access to `TestSession`, `TestAnswer`, `TestAnswerOption`.
- [x] Configure read access to `Question` (`DefaultPoint`) and `Answer` (`IsCorrect`).
- [x] Confirm shared `DbContext` strategy with Testing (003) and QuestionBank (002) modules

---

## Phase 2: Core Domain Logic

- [ ] **GradingEngine** (`IGradingEngine`):
  - [ ] `SINGLE_CHOICE` grading: compare `TestAnswer.answer_id` to the correct `Answer.answer_id`
  - [ ] `MULTIPLE_SELECT` grading: compare selected `TestAnswerOption` set to the full correct answer set — all correct + no incorrect = true
  - [ ] `TRUE_FALSE` grading (standalone): same as `SINGLE_CHOICE`
  - [ ] `COMPOSITE` grading — general: grade each `QuestionPart`; `points_earned` = sum of correct part points
  - [ ] `COMPOSITE` grading — all-TRUE_FALSE parts (BR-23): count correct parts → look up non-linear table (0→0, 1→0.10×dp, 2→0.25×dp, 3→0.50×dp, N→1.00×dp); `is_correct` on parent = true only when all parts correct; each `TestAnswerPart.is_correct` still recorded individually
  - [ ] `SHORT_ANSWER` grading: `LOWER(short_answer_text) == LOWER(Answer.answer_content)` where `Answer.is_correct = true`
  - [ ] Calculate `score = SUM(points_earned) / total_question × 10.0` (BR-20)
  - [ ] Count `num_correct`, `num_incorrect`, `num_abandoned` (abandoned per BR-16b)

- [ ] **GradeSubmittedSessionHandler** (MVP synchronous):
  - [ ] Called in-process by Testing submit/force-submit flow
  - [ ] Validate `TestSession.status = InProgress`
  - [ ] Run `GradingEngine.Grade()` synchronously for `Practice` and `Exam`
  - [ ] Write results in the same transaction as submission (DC-05): update `test_answers` + `test_sessions`
  - [ ] Set `TestSession.status = Graded`; preserve `submission_type` from Testing (`StudentSubmit`, `TimeoutSubmit`, `SystemSubmit`)
  - [ ] Publish `GradeCalculatedEvent` after commit
  - [ ] SLA: `Practice` < 2.0 seconds, `Exam` target < 5.0 seconds

- [ ] **Transactional Atomicity** (DC-05):
  - [ ] Wrap grading writes + session status update in single `using var tx = db.BeginTransaction()`
  - [ ] On failure: rollback → session stays `InProgress`, not `Graded`
  - [ ] Log failure with structured logging (session_id, error)

- [ ] **ChatbotService** (UC-51):
  - [ ] Implement `IChatbotService.AskAsync(questionContent, studentAnswer)`
  - [ ] POST to OpenAI/Claude API with system prompt: "math tutor, explain step-by-step in clear natural language; use simple Unicode/plain-text math notation where needed"
  - [ ] Apply 10-second timeout, Polly circuit breaker (3 fails = open 30s)
  - [ ] Enforce 1 request per student per session using **in-memory rate limiter** (A2 — MVP only).
    - Keyed by `(studentId, sessionId)`; TTL-based or flag per request scope.
    - **Do NOT use Redis** for this in MVP — Constitution §IV prohibits Redis unless spec-backed. Redis becomes relevant only under multi-instance deployment (post-MVP).
  - [ ] Return explanation string — do NOT persist to database (BR-21)

- [ ] **Polly Retry Policy** (U2 — per Assumptions:L96):
  - [ ] Configure Polly retry on grading DB transaction: 3 retries with exponential backoff (1s, 2s, 4s)
  - [ ] On all retries exhausted: rollback transaction, log structured error, return failure to caller (session stays `InProgress`)

- [ ] **GradeCalculatedEvent Contract** (G3):
  - [ ] Use `MathInsight.Shared.Events.GradeCalculatedEvent` — do NOT define a separate local copy
  - [ ] Populate `PerTagResults` from grading output: one `TopicGradeResult(TagId, TopicScore, CorrectCount, TotalCount)` per distinct tag in session
  - [ ] Populate `NumAbandoned` from count of unanswered/abandoned answers per BR-16b
  - [ ] Publish via MediatR `IPublisher.Publish(event)` after transaction commit (not before)

---

## Phase 3: Controller and Routing

- [ ] `GradingController`:
  - [ ] `POST /api/v1/chatbot/assist` — StudentOnly, UC-51
  - [ ] Accepts `{ sessionId, questionId, studentAnswer }` in request body
  - [ ] Returns `{ explanation: "..." }` as JSON

- [ ] Register inside `GradingModuleExtensions.cs`:
  - GradingEngine, ChatbotService, MediatR handlers

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] Practice: 40-question session graded in < 2.0s
  - [ ] Exam: session graded synchronously and persisted as `Graded`
  - [ ] SINGLE_CHOICE correct → `is_correct = true`, `points_earned = default_point`
  - [ ] MULTIPLE_SELECT partial → `is_correct = false`, `points_earned = 0`
  - [ ] SHORT_ANSWER case-insensitive match → `is_correct = true`
  - [ ] Abandoned answer (per BR-16b) → `is_correct = false`, counted in `num_abandoned`
  - [ ] COMPOSITE all-TRUE_FALSE — 0 correct → `points_earned = 0` (BR-23)
  - [ ] COMPOSITE all-TRUE_FALSE — 1/N correct → `points_earned = 0.10 × default_point` (BR-23)
  - [ ] COMPOSITE all-TRUE_FALSE — 2/N correct → `points_earned = 0.25 × default_point` (BR-23)
  - [ ] COMPOSITE all-TRUE_FALSE — 3/N correct → `points_earned = 0.50 × default_point` (BR-23)
  - [ ] COMPOSITE all-TRUE_FALSE — N/N correct → `points_earned = default_point`; `is_correct = true` (BR-23)
  - [ ] COMPOSITE general (mixed parts) — parent score = sum of part points earned
  - [ ] DC-05: Simulated DB failure mid-grade → rollback, session stays `InProgress`
  - [ ] UC-51: Chatbot returns explanation JSON within 10s (happy path)
  - [ ] UC-51: Chatbot API times out after 10s → endpoint returns structured error (e.g. 503); student session is NOT affected (U1)
  - [ ] UC-51: Second chatbot call same session → rate limited (429)
