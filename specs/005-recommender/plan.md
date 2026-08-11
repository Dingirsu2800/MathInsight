# Implementation Plan: Recommender Module

> **Current checkpoint**: consume the Recommender integration contract in [Scoring Contract V2](../scoring-contract-v2.md).

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-08-03
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` for Rule-Based/Ptag v4.1 (Unified Multi-Tag). The module tracks topic mastery at `(StudentID, TagID)`, supports multi-tag Elo delta distribution, diagnoses WeakTags and Bottleneck Weak Tags, maps the recommended difficulty level, and exposes an in-process API for TestGen. The stable cross-module contract is `IStudentRecommendationProvider` in `MathInsight.Shared`; it uses semantic string IDs and returns only qualified WeakTag evidence.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core |
| Storage | SQL Server; map to current DB script tables |
| Cache | None required for MVP |
| External ML | None required for MVP |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Recommender/
├── Handlers/
│   └── TopicResultIngestionHandler.cs
├── Services/
│   ├── IRecommenderService.cs
│   ├── RecommenderService.cs
│   ├── ICompetencyEngine.cs
│   ├── CompetencyEngine.cs
│   ├── IDifficultyMappingService.cs
│   └── DifficultyMappingService.cs
├── Queries/
│   ├── GetWeakTags/
│   ├── GetAllTagsMastery/
│   ├── GetRecommendedLectures/
│   └── GetRecommendedMaterials/
├── Persistence/
│   ├── RecommenderDbContext.cs
│   └── Configurations/
│       ├── CompetencyPointConfiguration.cs
│       ├── TagsMasteryConfiguration.cs
│       └── StudentTopicSessionResultConfiguration.cs
├── Controllers/
│   └── RecommenderController.cs
└── RecommenderModuleExtensions.cs
```

## Proposed Changes

### Database Layer

| Table | Key Constraints |
|-------|-----------------|
| `CompetencyPoint` | Unique `(StudentID, Grade)`; `Point` range `0.00..10.00` |
| `TagsMastery` | Unique `(StudentID, TagID)`; stores `OfficialPoint`, `PracticePoint`, `ExamAnchor` |
| `StudentTopicSessionResult` | Unique `(SessionID, TagID)`; stores per-session topic snapshot |

`TagsMastery.DifficultyID` is intentionally removed. Difficulty is an output of recommendation through `RecommendedDifficultyLevel`, not part of the mastery key.

### Internal API

```csharp
public interface IRecommenderService
{
    Task<IReadOnlyList<WeakTagDto>> GetStudentWeakTagsAsync(string studentId);
    Task<IReadOnlyList<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(string studentId);
}
```

TestGen uses `WeakTagAdviceDto.RecommendedDifficultyLevel` to select questions. It does not need `BlueprintSectionID`.

> **Resolution required**: `RecommendedDifficultyLevel` is a level integer `1..4`, **not** a `difficulty_id` PK.
> TestGen must resolve it via: `SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = RecommendedDifficultyLevel`
> before filtering `Question.DifficultyID`. This is documented as a task for `DifficultyMappingService` (module 005).

### All-Tag Mastery Query (UC-55 / RCM-17)

`GET /api/v1/recommender/topic-mastery` returns all eligible mastery rows on active direct-child topics
with an active same-grade root parent, ordered by `OfficialPoint ascending, TagId ascending`.

Unlike `GET /weak-tags` (OfficialPoint < 5.00 only), this endpoint covers all mastery statuses
(`NotLearned`, `Learning`, `Mastered`) and is consumed by the Competency page components:
- **TopicMasteryGrid** — shows a card per topic with server-authoritative `MasteryStatus`.
- **CompetencySummaryCard** — computes average score from all practiced topics (`numberDone > 0`).
- **RadarChartCard** — renders up to 8 axes using all topics (weakest first for visibility).

### Ptag Update Pipeline (Unified Multi-Tag v4.1)

```text
TestSession becomes Graded
  -> Grading emits GradeCalculatedEvent containing:
       - Answers with TagWeights (all tags + weights per answer)
       - PerTagResults with weighted TopicScore (Tầng 1-2)
  -> Recommender upserts StudentTopicSessionResult per tag
  -> Recommender updates TagsMastery for EACH tag:
       - If Exam format: update exam_anchor using Exponential Decay (RCM-05)
         with weighted T_j^{(i)} from TopicGradeResult
       - If Practice format:
         1. Compute Δ_total per answer (Bước 1, unchanged)
         2. For EACH tag in answer.TagWeights:
              ΔP_tag_i = Δ_total × w_i (Bước 2, multi-tag)
              practice_point += ΔP_tag_i (clamped)
              series_answer_count++ (per-tag independent)
         3. If series_answer_count >= 10: blend + reset
  -> Recommender recalculates OfficialPoint per tag
  -> Recommender queries Student.current_grade and updates CompetencyPoint
  -> Recommender maps RecommendedDifficultyLevel per tag
  -> TestGen reads WeakTag advice (including BR-19 Bottleneck) for future tests
```

### Difficulty Mapping

```csharp
if (officialPoint < 3.00m) return 1;
if (officialPoint < 5.00m) return 2;
if (officialPoint < 7.50m) return 3;
return 4;
```

WeakTag classification:

```csharp
// Standard WeakTag (RCM-03)
officialPoint < 5.00m

// Bottleneck WeakTag for sub-tags (BR-19, RCM-14)
officialPoint < 4.00m
```

### Difficulty-Aware Lecture Recommendation

`GET /api/v1/recommender/lectures` remains unchanged. Recommender reads `Lecture.DifficultyID`, active `TagTopic`, active `TagDifficulty`, and `Student.CurrentGrade` through migration-excluded read models. A qualified personalized context has `TagsMastery.NumberDone >= 3`; it may be Weak, Learning, or Mastered progression. The target is `RecommendedDifficultyLevel`. For a topic, only `Published` candidates at or below the target level are eligible; choose exact level first, then nearest lower level. Return at most two candidates per topic and six overall with deterministic ordering.

When no qualified context exists, grade-based cold start returns active, `Published`, level-1 lectures for `Student.CurrentGrade`; null grade returns `[]`. The response includes difficulty metadata, nullable `OfficialPoint`, evidence count, fallback flag, and an audit reason. Material recommendation keeps its existing weak-topic behavior and has no difficulty-ranking scope.

## Verification Plan

1. `dotnet build` - zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:
   - Graded topic result creates one `StudentTopicSessionResult`.
   - Duplicate `(SessionID, TagID)` result is ignored or rejected without double update.
   - `TagsMastery` upsert uses `(StudentID, TagID)`.
   - `OfficialPoint` formula matches `0.7 * ExamAnchor + 0.3 * PracticePoint`.
   - WeakTag query returns only `OfficialPoint < 5.00`.
   - `RecommendedDifficultyLevel` mapping returns levels `1..4`.
   - SQL-only recommender works without Redis/SAR.
   - Personalized exact/lower difficulty matching never returns a harder lecture.
   - Cold start returns only active level-1 lectures in the student's grade.
   - `GetStudentAllTagsMasteryAsync` returns all mastery rows without score filter.
   - Student with mixed topics (weak + learning + mastered) → `GET /topic-mastery` returns all of them.
   - `GET /topic-mastery` returns `401` for unauthenticated requests.
