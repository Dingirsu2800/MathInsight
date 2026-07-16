# Implementation Plan: Recommender Module

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-07-16
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` for Rule-Based/Ptag v2. The module tracks topic mastery at `(StudentID, TagID)`, diagnoses WeakTags from `OfficialPoint`, maps the recommended difficulty level, and exposes an in-process API for TestGen.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core |
| Storage | SQL Server; map exactly to `Database/database/001_Create_MathInsight_Azure.sql` |
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

Cross-module components introduced by hardening:

- `MathInsight.Shared/Recommendation/IStudentRecommendationProvider.cs`
- `MathInsight.Shared/Events/GradeCalculatedEvent.cs`
- `Persistence/Entities/StudentReadOnly.cs` and its excluded-from-migrations configuration
- Read-only canonical mappings for `TagTopic`, `Lecture`, `Material`, and `LectureMaterial`

## Proposed Changes

### Database Layer

| Table | Key Constraints |
|-------|-----------------|
| `CompetencyPoint` | Unique `(StudentID, Grade)`; `Point` range `0.00..10.00` |
| `TagsMastery` | Unique `(StudentID, TagID)`; stores `OfficialPoint`, `PracticePoint`, `ExamAnchor` |
| `StudentTopicSessionResult` | Unique `(SessionID, TagID)`; stores `TotalItems`, `CorrectItems`, `EarnedPoints`, `MaxPoints`, `TopicScore` |

`TagsMastery.DifficultyID` is intentionally removed. Difficulty is an output of recommendation through `RecommendedDifficultyLevel`, not part of the mastery key.

### Internal API

```csharp
public interface IStudentRecommendationProvider
{
    Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId,
        CancellationToken cancellationToken = default);
}
```

The interface and DTO live in `MathInsight.Shared`. `RecommenderService` implements this provider; TestGen depends only on Shared. TestGen uses `WeakTagAdvice.RecommendedDifficultyLevel` to select questions. It does not need `BlueprintSectionID`.

> **Resolution required**: `RecommendedDifficultyLevel` is a level integer `1..4`, **not** a `difficulty_id` PK.
> TestGen must resolve it via: `SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = RecommendedDifficultyLevel`
> before filtering `Question.DifficultyID`. This is documented as a task for `DifficultyMappingService` (module 005).

### Ptag Update Pipeline

```text
TestSession becomes Graded
  -> Grading emits GradeCalculatedEvent containing detailed answers list (F1 resolution)
  -> Recommender upserts StudentTopicSessionResult
  -> Recommender updates TagsMastery:
       - If Exam format: update exam_anchor using Exponential Decay (RCM-05)
       - If Practice format: update practice_point sequentially using Elo formula (RCM-06)
  -> Recommender recalculates OfficialPoint
  -> Recommender queries Student.CurrentGrade (F5 resolution) and updates CompetencyPoint
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

## SQL Contract Hardening

- Use `string` for every Recommender/API/event identifier and map it as non-Unicode `VARCHAR(36)`.
- Map owned entities to the canonical PascalCase columns, precision, key, index, and constraint names.
- Map `Student`, `TagTopic`, `Lecture`, `Material`, and `LectureMaterial` as cross-module read models excluded from migrations.
- Ingest each `GradeCalculatedEvent` under the SQL execution strategy and a `Serializable` transaction.
- Persist weighted topic snapshots and compute `TopicScore = EarnedPoints / MaxPoints * 10` in Grading.
- Read `Student.CurrentGrade` before recalculating competency; never create grade `0` when it is null.
- Keep REST routes unchanged. Missing/empty account claims return stable `AUTH_INVALID_TOKEN` with HTTP 401.
- Add an opt-in disposable SQL Server smoke test controlled by `RECOMMENDER_SQLSERVER_CONNECTION`; never run it against Azure/shared databases.
