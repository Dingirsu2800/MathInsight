# Implementation Plan: Grading & Analytics Module

> **Current checkpoint**: implement snapshot scoring and recalculation from [Scoring Contract V2](../scoring-contract-v2.md).

**Branch**: `004-grading-analytics` | **Date**: 2026-06-23 | **Updated**: 2026-07-14
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Grading` — handles synchronous MVP auto-grading, solution display, and AI chatbot assistance. It is called by the Testing submit flow and publishes `GradeCalculatedEvent` to Recommender module after commit.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, Polly |
| Storage | SQL Server; cross-reads current DB script tables owned by Testing and QuestionBank |
| External | OpenAI / Claude API (chatbot, UC-51) |
| Queue | None for MVP; async grading requires a future `PendingGrading` state or grading job table |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Grading/
├── Handlers/
│   └── GradeSubmittedSessionHandler.cs  # Synchronous MVP grading from Testing submit flow
├── Services/
│   ├── IGradingEngine.cs               # Grading algorithm interface
│   ├── GradingEngine.cs                # Per-question-type grading logic
│   ├── IChatbotService.cs
│   └── ChatbotService.cs               # OpenAI/Claude REST client
├── Events/
│   └── GradeCalculatedEvent.cs         # MediatR notification → Recommender module
├── Controllers/
│   └── GradingController.cs            # UC-51: chatbot endpoint
└── GradingModuleExtensions.cs
```

## Proposed Changes

### No Owned Database Tables

This module reads/writes cross-schema:
- **Reads**: `TestSession`, `TestAnswer`, `TestAnswerOption`, `TestAnswerPart`, `Question`, `QuestionPart`, `Answer`
- **Writes**: `TestSession` (status, score, counts), `TestAnswer` (is_correct, points_earned), `TestAnswerPart` (is_correct, points_earned)

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
GradingEngine.Grade(session):
  foreach TestAnswer in session:
    ├── SINGLE_CHOICE / TRUE_FALSE (standalone): compare answer_id to correct answer
    ├── MULTIPLE_SELECT: compare selected options (TestAnswerOption) to correct set
    ├── COMPOSITE:
    │     ├── if ALL QuestionParts are TRUE_FALSE → apply BR-23 non-linear table
    │     │     (0 correct=0, 1=0.10×dp, 2=0.25×dp, 3=0.50×dp, N=1.00×dp)
    │     │     (update each TestAnswerPart.is_correct for solution display; set TestAnswerPart.points_earned = 0 — parent TestAnswer.points_earned is the source of truth)
    │     └── otherwise → grade each QuestionPart and update TestAnswerPart (is_correct, points_earned); parent score = sum of part points
    └── SHORT_ANSWER: case-insensitive compare short_answer_text
  Calculate: score = SUM(points_earned) / SUM(max_points) × 10.0 (where max_points is Question.default_point for each parent question)
  Update in single transaction (DC-05):
    ├── TestAnswer: is_correct, points_earned
    ├── TestAnswerPart: is_correct, points_earned (for Composite parts)
    └── TestSession: status=Graded, score, num_correct, num_incorrect, num_abandoned
        │
Build GradeCalculatedEvent (Unified Multi-Tag v4.1):
  foreach TestAnswer:
    ├── Load ALL QuestionTopics (primary + secondary)
    ├── Calculate tag weights w_{iq} per BR-13/14/15:
    │     - Single tag: w = 1.0
    │     - Primary (w_main): default 0.65
    │     - Secondary (w_sub_i): (1 − w_main) / N_sub
    ├── NormalizedScore s_q = PointsEarned / MaxPoints × 10.0
    └── Emit TagWeights list per answer
  foreach distinct TagId across all answers:
    ├── Tầng 1: c_{q,i} = s_q × w_{iq} for each question containing this tag
    ├── Tầng 2: T_j^{(i)} = avg(c_{q,i}) across all questions with this tag
    └── Emit TopicGradeResult with weighted TopicScore
        │
Publish GradeCalculatedEvent (MediatR in-process):
  → Recommender module: update StudentTopicSessionResult + TagsMastery per tag (multi-tag delta distribution)
  → Notification module: send push notification
```

### Chatbot Integration (UC-51)

```csharp
// ChatbotService.AskAsync(questionContent, studentAnswer):
// POST https://api.openai.com/v1/chat/completions
// System prompt: "You are a math tutor. Explain the solution step-by-step in clear natural language.
// Use simple Unicode/plain-text math notation where needed; do not require technical markup syntax."
// User message: questionContent + studentAnswer
// Returns: string explanation (not persisted)
```

- API key injected via `Chatbot:ApiKey` environment variable.
- Timeout: 10 seconds (Polly circuit breaker after 3 failures).
- Rate limiting: 1 request per student per session (enforced in service layer).

## Verification Plan

1. `dotnet build` — zero compile errors.
2. Integration tests (xUnit):
   - Practice grading completes in < 2.0s for a 40-question test.
   - Exam grading completes synchronously and persists `status = Graded`.
   - SINGLE_CHOICE: correct answer selected → `is_correct = true`, `points_earned = default_point`.
   - MULTIPLE_SELECT: all correct + no incorrect → `is_correct = true`.
   - MULTIPLE_SELECT: partial selection → `is_correct = false`.
   - SHORT_ANSWER: case-insensitive match → `is_correct = true`.
   - Unanswered: `is_correct = false`, `points_earned = 0.00`.
   - DC-05: Grading failure mid-transaction → rollback (session stays `InProgress`).
   - UC-51: Chatbot returns explanation within 10s.
