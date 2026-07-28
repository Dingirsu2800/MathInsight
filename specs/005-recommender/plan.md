# Implementation Plan: Recommender Module

> **Current checkpoint**: consume the Recommender integration contract in [Scoring Contract V2](../scoring-contract-v2.md).

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-07-04
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` for Rule-Based/Ptag v4.1 (Unified Multi-Tag). The module tracks topic mastery at `(StudentID, TagID)`, supports multi-tag Elo delta distribution, diagnoses WeakTags and Bottleneck Weak Tags, maps the recommended difficulty level, and exposes an in-process API for TestGen.

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
    Task<IReadOnlyList<WeakTagDto>> GetStudentWeakTagsAsync(Guid studentId);
    Task<IReadOnlyList<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(Guid studentId);
}
```

TestGen uses `WeakTagAdviceDto.RecommendedDifficultyLevel` to select questions. It does not need `BlueprintSectionID`.

> **Resolution required**: `RecommendedDifficultyLevel` is a level integer `1..4`, **not** a `difficulty_id` PK.
> TestGen must resolve it via: `SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = RecommendedDifficultyLevel`
> before filtering `Question.DifficultyID`. This is documented as a task for `DifficultyMappingService` (module 005).

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
