# Implementation Plan: Grading & Analytics Module

**Branch**: `004-grading-analytics` | **Date**: 2026-06-23 | **Updated**: 2026-07-22
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Grading` — handles synchronous MVP auto-grading, solution display, and AI chatbot assistance. It is called by the Testing submit flow and publishes `GradeCalculatedEvent` to Recommender module after commit.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (Exam consumer) |
| Storage | SQL Server; cross-reads current DB script tables owned by Testing and QuestionBank |
| External | Google Gemini API (chatbot, UC-51; model: `gemini-2.0-flash`) |
| Queue | MassTransit — `TestSubmittedConsumer` registered for Exam mode. InMemory transport for MVP; RabbitMQ for production. |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Grading_Analytics/
├── Consumers/
│   └── TestSubmittedConsumer.cs      # MassTransit IConsumer<TestSubmittedEvent> for Exam mode async grading
├── Handlers/
│   └── GradeSubmittedSessionHandler.cs  # MediatR INotificationHandler<TestSubmittedEvent> for Practice mode
├── Services/
│   ├── IGradingOrchestrator.cs          # Shared grading orchestration interface
│   ├── GradingOrchestrator.cs           # Core grading flow: load session → validate → grade → save → build event
│   ├── IGradingEngine.cs                # Grading algorithm interface
│   ├── GradingEngine.cs                 # Per-question-type grading logic
│   ├── IChatbotService.cs
│   └── ChatbotService.cs               # Google Gemini API REST client (gemini-2.0-flash)
├── Events/
│   └── (uses MathInsight.Shared.Events.GradeCalculatedEvent)
├── Controllers/
│   ├── GradingController.cs            # UC-51: chatbot endpoint (api/v1/chatbot)
│   └── StudentGradingController.cs     # UC-55/56: session result + history (api/v1/grading)
└── GradingModuleExtensions.cs
```

## Proposed Changes

### No Owned Database Tables

This module reads/writes cross-schema:
- **Reads**: `TestSession`, `TestAnswer`, `TestAnswerOption`, `TestAnswerPart`, `TestQuestion` (scoring snapshots), `Test` (MaxScore, ScoringPolicy), `Question`, `QuestionPart`, `Answer`
- **Writes**: `TestSession` (status, score, counts, `GradeRevision`), `TestAnswer` (is_correct, points_earned), `TestAnswerPart` (is_correct, points_earned)

All writes are executed within a **single transaction** (DC-05).

### Service & API Gateway — REST Endpoints

```
POST   /api/v1/chatbot/assist                  # UC-51: send question + student answer to AI
GET    /api/v1/grading/sessions/{sessionId}    # UC-55: view graded session result
GET    /api/v1/grading/student/history         # UC-56: paginated session history
GET    /api/v1/grading/student/stats           # UC-56: aggregate stats (totalSessions, avgScore, accuracy)
```

> Grading itself is **not a REST endpoint** — it is called by Testing during submit/force-submit.

---

## UC-55: GET /api/v1/grading/sessions/{sessionId}

### Files

```
src/MathInsight.Modules.Grading_Analytics/
├── Queries/
│   ├── GetSessionResult/
│   │   ├── GetSessionResultQuery.cs
│   │   ├── GetSessionResultQueryHandler.cs
│   │   └── SessionResultDto.cs          # SessionResultDto, GradedAnswerDetailDto, AnswerPartDetailDto
```

### Logic
- Load `TestSession` + `TestAnswers` (with `Question`, `SelectedOptions`, `AnswerParts`, `QuestionPart`) for the given `sessionId`.
- Guard: `StudentId == authenticatedStudentId` → 403. Not found → 404.
- Map to `SessionResultDto`. When `Status != Graded`, `isCorrect` fields will be `null`.
- Ordered by `TestAnswer.QuestionNo ASC`.

---

## UC-56: GET /api/v1/grading/student/history & /stats

### Files

```
src/MathInsight.Modules.Grading_Analytics/
├── Queries/
│   ├── GetSessionHistory/
│   │   ├── GetSessionHistoryQuery.cs
│   │   ├── GetSessionHistoryQueryHandler.cs
│   │   └── SessionHistoryDto.cs         # SessionHistoryDto, StudentHistoryStatsDto, PagedResult<T>
```

### Logic (history)
- Filter `TestSessions` by `StudentId == authenticatedStudentId` AND `Status == "Graded"`.
- Apply optional `testFormat`, `fromDate`, `toDate` filters.
- Order by `EndTime DESC`. Paginate with `Skip`/`Take`.
- Return `PagedResult<SessionHistoryDto>` with `totalCount`, `totalPages`.

### Logic (stats)
- Same filter scope (same student, `Status == "Graded"`).
- `totalSessions`: `COUNT(*)`
- `sessionsLast30Days`: `COUNT(*) WHERE EndTime >= DateTime.UtcNow.AddDays(-30)`
- `averageScore`: `AVG(Score)` (0 if no sessions)
- `accuracyPercent`: `SUM(NumCorrect) * 100.0 / SUM(TotalQuestion)` (0 if no sessions)

### Integration & Domain Events

| Event | Direction | Details |
|-------|-----------|---------|
| `GradeCalculatedEvent` | **Published** to Recommender (005) | Contains `session_id`, `student_id`, multi-tag per-answer weights (`TagWeights`), per-tag topic scores (Tầng 1–2), and detailed answers list |
| `GradeCalculatedEvent` | **Published** to Notification (008) | Triggers "test graded" push notification |

### Grading Pipeline

```
Testing submit flow calls GradeSubmittedSessionHandler
        │
GradingOrchestrator.GradeSessionInTransactionAsync(session):
  1. Load TestSession + TestAnswers + all navigation properties
  2. Load TestQuestion scoring snapshots for this Test (MaxPointsSnapshot, ScoringRuleSnapshot, IsScoreInvalidated)
  3. Resolve TestQuestion onto each TestAnswer.TestQuestion (manual join via TestId + QuestionId)
  4. Load Test entity for MaxScore and ScoringPolicy
  5. Run GradingEngine.Grade(session):
        │
  GradingEngine.Grade(session):
    foreach TestAnswer in session:
      ├── Resolve maxPoints = TestQuestion.MaxPointsSnapshot (fallback: Question.DefaultWeight)
      ├── If IsScoreInvalidated → award full maxPoints, IsCorrect = null, skip grading
      ├── If abandoned (BR-16b) → IsCorrect = false, PointsEarned = 0
      ├── Route by ScoringRuleSnapshot (priority) or QuestionType (fallback):
      │     ├── AllOrNothing → correct = full maxPoints, incorrect = 0
      │     ├── TieredTrueFalse → BR-23 non-linear table (0→0, 1→0.10×mp, 2→0.25×mp, 3→0.50×mp, N→1.00×mp)
      │     ├── WeightedParts → per-part weighted scoring using QuestionPart.DefaultWeight ratios
      │     ├── SINGLE_CHOICE / TRUE_FALSE → compare answer_id to correct answer
      │     ├── MULTIPLE_SELECT → compare selected options to correct set
      │     ├── COMPOSITE → dispatch to TieredTrueFalse or WeightedParts based on part types
      │     └── SHORT_ANSWER → case-insensitive string match
      └── Calculate: score = SUM(effective_points) / SUM(max_points) × 10.0 (BR-20)
        │
  6. Write in single transaction (DC-05):
      ├── TestAnswer: is_correct, points_earned
      ├── TestAnswerPart: is_correct, points_earned (for Composite parts)
      └── TestSession: status=Graded, score, num_correct, num_incorrect, num_abandoned, GradeRevision++
        │
  7. Build GradeCalculatedEvent (Unified Multi-Tag v4.1):
    foreach TestAnswer:
      ├── Load ALL QuestionTopics (primary + secondary)
      ├── Calculate tag weights w_{iq} per BR-13/14/15
      ├── MaxPoints from TestQuestion.MaxPointsSnapshot
      ├── NormalizedScore s_q = PointsEarned / MaxPoints × 10.0
      ├── IsScoreInvalidated from TestQuestion
      └── Emit TagWeights list per answer (exclude invalidated from tag stats)
    foreach distinct TagId across all non-invalidated answers:
      ├── Tầng 1: c_{q,i} = s_q × w_{iq}
      ├── Tầng 2: T_j^{(i)} = avg(c_{q,i})
      └── Emit TopicGradeResult with weighted TopicScore
        │
  8. Publish GradeCalculatedEvent (MediatR in-process):
    → Recommender module: update StudentTopicSessionResult + TagsMastery per tag
    → Notification module: send push notification
```

### Chatbot Integration (UC-51)

```csharp
// ChatbotService.AskAsync(questionContent, studentAnswer, studentId, sessionId):
// POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={ApiKey}
// System instruction: "You are a math tutor. Explain the solution step-by-step in clear natural language.
// Use simple Unicode/plain-text math notation where needed; do not require technical markup syntax."
// User message: questionContent + studentAnswer
// Returns: string explanation (not persisted)
```

- API key injected via `Chatbot:ApiKey` configuration section. Model configurable via `Chatbot:Model` (default: `gemini-2.0-flash`). Base URL: `Chatbot:BaseUrl` (default: `https://generativelanguage.googleapis.com/`).
- Timeout: 10 seconds (HttpClient timeout). Polly circuit breaker: 3 failures = open 30s.
- Rate limiting: 1 request per student per session — in-memory `ConcurrentDictionary<(Guid, Guid), DateTime>` with TTL-based eviction (1 hour cleanup interval).

## Verification Plan

1. `dotnet build` — zero compile errors.
2. Integration tests (xUnit):
   - Practice grading completes in < 2.0s for a 40-question test.
   - Exam grading completes synchronously and persists `status = Graded`.
   - SINGLE_CHOICE: correct answer selected → `is_correct = true`, `points_earned = MaxPointsSnapshot`.
   - MULTIPLE_SELECT: all correct + no incorrect → `is_correct = true`.
   - MULTIPLE_SELECT: partial selection → `is_correct = false`.
   - SHORT_ANSWER: case-insensitive match → `is_correct = true`.
   - Unanswered: `is_correct = false`, `points_earned = 0.00`.
   - DC-05: Grading failure mid-transaction → rollback (session stays `InProgress`).
   - UC-51: Chatbot returns explanation within 10s.
   - Score invalidation: `IsScoreInvalidated = true` → `PointsEarned = MaxPointsSnapshot`, `IsCorrect = null`.
   - GradeRevision increments on each grading pass.
