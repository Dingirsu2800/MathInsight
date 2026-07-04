# Tasks Checklist: Grading & Analytics Module

**Branch**: `004-grading-analytics` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] No owned tables — this module cross-reads current DB script tables owned by Testing and QuestionBank.
- [ ] Configure read access to `TestSession`, `TestAnswer`, `TestAnswerOption`.
- [ ] Configure read access to `Question` (`DefaultPoint`) and `Answer` (`IsCorrect`).
- [ ] Confirm shared `DbContext` strategy with Testing (003) and QuestionBank (002) modules

---

## Phase 2: Core Domain Logic

- [ ] **GradingEngine** (`IGradingEngine`):
  - [ ] `SINGLE_CHOICE` grading: compare `TestAnswer.answer_id` to the correct `Answer.answer_id`
  - [ ] `MULTIPLE_SELECT` grading: compare selected `TestAnswerOption` set to the full correct answer set — all correct + no incorrect = true
  - [ ] `TRUE_FALSE` grading: same as SINGLE_CHOICE
  - [ ] `SHORT_ANSWER` grading: `LOWER(short_answer_text) == LOWER(Answer.answer_content)` where `Answer.is_correct = true`
  - [ ] Calculate `score = SUM(points_earned) / total_questions × 10.0` (BR-20)
  - [ ] Count `num_correct`, `num_incorrect`, `num_abandoned` (null answer = abandoned)

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
  - [ ] Enforce 1 request per student per session (in-memory rate limiter or Redis key)
  - [ ] Return explanation string — do NOT persist to database (BR-21)

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
  - [ ] Null answer (abandoned) → `is_correct = false`, counted in `num_abandoned`
  - [ ] DC-05: Simulated DB failure mid-grade → rollback, session stays `InProgress`
  - [ ] UC-51: Chatbot returns explanation JSON within 10s
  - [ ] UC-51: Second chatbot call same session → rate limited (429)
