# Implementation Plan: Recommender Module

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-07-04
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` for Rule-Based/Ptag v2. The module tracks topic mastery at `(StudentID, TagID)`, diagnoses WeakTags from `OfficialPoint`, maps the recommended difficulty level, and exposes an in-process API for TestGen.

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

### Ptag Update Pipeline

```text
TestSession becomes Graded
  -> Grading emits GradeCalculatedEvent containing detailed answers list (F1 resolution)
  -> Recommender upserts StudentTopicSessionResult
  -> Recommender updates TagsMastery (practice_point is updated sequentially & retrospectively using event's Answers list) (F4 resolution)
  -> Recommender recalculates OfficialPoint
  -> Recommender queries Student.current_grade (F5 resolution) and updates CompetencyPoint
  -> Recommender maps RecommendedDifficultyLevel
  -> TestGen reads WeakTag advice for future tests
```

### Difficulty Mapping

```csharp
if (officialPoint < 3.00m) return 1;
if (officialPoint < 5.00m) return 2;
if (officialPoint < 7.50m) return 3;
return 4;
```

WeakTag is:

```csharp
officialPoint < 5.00m
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
