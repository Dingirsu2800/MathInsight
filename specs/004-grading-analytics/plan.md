# Implementation Plan: Grading & Analytics Module

**Branch**: `004-grading-analytics` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
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
- **Reads**: `TestSession`, `TestAnswer`, `TestAnswerOption`, `Question`, `Answer`
- **Writes**: `TestSession` (status, score, counts), `TestAnswer` (is_correct, points_earned)

All writes are executed within a **single transaction** (DC-05).

### Service & API Gateway — REST Endpoints

```
POST   /api/v1/chatbot/assist            # UC-51: send question + student answer to AI
```

> Grading itself is **not a REST endpoint** — it is called by Testing during submit/force-submit.

### Integration & Domain Events

| Event | Direction | Details |
|-------|-----------|---------|
| `GradeCalculatedEvent` | **Published** to Recommender (005) | Contains `session_id`, `student_id`, per-tag correctness summary |
| `GradeCalculatedEvent` | **Published** to Notification (008) | Triggers "test graded" push notification |

### Grading Pipeline

```
Testing submit flow calls GradeSubmittedSessionHandler
        │
GradingEngine.Grade(session):
  foreach TestAnswer in session:
    ├── SINGLE_CHOICE / TRUE_FALSE: compare answer_id to correct answer
    ├── MULTIPLE_SELECT: compare selected options (TestAnswerOption) to correct set
    └── SHORT_ANSWER: case-insensitive compare short_answer_text
  Calculate: score = SUM(points_earned) / total_questions × 10.0
  Update in single transaction (DC-05):
    ├── TestAnswer: is_correct, points_earned
    └── TestSession: status=Graded, score, num_correct, num_incorrect, num_abandoned
        │
Publish GradeCalculatedEvent (MediatR in-process):
  → Recommender module: update StudentTopicSessionResult + TagsMastery idempotently
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
