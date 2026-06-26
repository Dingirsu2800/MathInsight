# Implementation Plan: Recommender Module

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-06-26 *(Dynamic Test Generator Loop)*
**Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/005-recommender/spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` — handles competency tracking (`CompetencyPoint`, `TagsMastery`), WeakTag diagnosis, SAR-based lecture/material recommendations, and internal API for TestGen. Consumes `GradeCalculatedEvent` from Grading module; caches results in Redis.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server (Schema: `rcm`) |
| Cache | Redis (`rcm:weak-tags:{student_id}`, TTL 1 hour) |
| SAR Engine | Python `recommenders` library (subprocess or separate service) |
| Scheduler | Hangfire (weekly SAR model training) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Recommender/
├── Consumers/
│   └── GradeCalculatedConsumer.cs      # MediatR handler: process GradeCalculatedEvent
├── Services/
│   ├── IRecommenderService.cs          # Internal interface for TestGen module (BR-29)
│   ├── RecommenderService.cs           # Implements both GetStudentWeakTagsAsync &
│   │                                   #   GetStudentWeakTagAdviceAsync (Dynamic Loop)
│   ├── IDifficultyMappingService.cs    # Maps current difficulty → recommended practice difficulty
│   ├── DifficultyMappingService.cs     # Hard→Medium, Medium→Easy, Easy→Remedial logic
│   ├── ICompetencyEngine.cs
│   ├── CompetencyEngine.cs             # TagsMastery update, P_tag calculation, mastery transition
│   └── SarModelRunner.cs               # Python subprocess caller for SAR training/prediction
├── Queries/
│   ├── GetWeakTags/                    # UC-52: return WeakTags from Redis or DB
│   ├── GetRecommendedLectures/         # UC-53: match weak tag_id to lrn.lectures.tag_id
│   └── GetRecommendedMaterials/        # UC-54: match weak tag_id to lrn.materials
├── Persistence/
│   ├── RecommenderDbContext.cs         # `rcm` schema
│   ├── Configurations/
│   │   ├── CompetencyPointConfiguration.cs  # UNIQUE (student_id, grade)
│   │   └── TagsMasteryConfiguration.cs      # UNIQUE (student_id, tag_id, difficulty_id)
│   └── Migrations/
├── Controllers/
│   └── RecommenderController.cs        # UC-52, 53, 54 REST endpoints
└── RecommenderModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Schema: `rcm`)

| Table | Key Indexes |
|-------|-------------|
| `rcm.competency_points` | UNIQUE `(student_id, grade)`; `point` CHECK 0.00–10.00 |
| `rcm.tags_mastery` | UNIQUE `(student_id, tag_id, difficulty_id)` |

### Service & API Gateway — REST Endpoints

**Student (StudentOnly policy)**
```
GET    /api/v1/recommender/weak-tags     # UC-52: list current WeakTags (from Redis or DB)
GET    /api/v1/recommender/lectures      # UC-53: recommended lectures based on WeakTags
GET    /api/v1/recommender/materials     # UC-54: recommended materials based on WeakTags
```

### Internal Interface (for TestGen module) — BR-29

```csharp
// IRecommenderService — registered as scoped DI in Modular Monolith
public interface IRecommenderService
{
    // Lightweight: simple WeakTag lookup (tagId, isRemedial only)
    Task<List<WeakTagDto>> GetStudentWeakTagsAsync(Guid studentId);

    // Full Dynamic Test Generator Loop contract (BR-29):
    // Returns per-tag difficulty advice for TestGen to apply adaptive question selection.
    Task<List<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(Guid studentId);
}

// WeakTagAdviceDto — full contract used by TestGen (009):
// See spec.md § WeakTagAdviceDto Contract for field semantics.
// Key fields:
//   RecommendedPracticeDifficultyId  — difficulty to use for question selection in this topic
//   IsRemedial                        — if true: cap topic to 10% of test; recommend foundational content
//   SuggestUpscaleToId               — non-null when P_tag >= 8.0 AND MASTERED → use harder tier
//   ChallengeMode                     — true when upscale is suggested
```

### Integration & Domain Events

| Event | Direction | Details |
|-------|-----------|---------|
| `GradeCalculatedEvent` | **Consumed** from Grading (004) | Contains `session_id`, `student_id`, per-question results |
| `CompetencyUpdatedEvent` | **Published** to Notification (008) | Notifies student of competency update |

### GradeCalculatedConsumer Logic

```csharp
// GradeCalculatedConsumer handles GradeCalculatedEvent:
// 1. For each question in session:
//    - Find TagsMastery record for (student_id, tag_id, difficulty_id)
//    - Increment number_done; if correct → increment num_correct
//    - Recalculate accuracy_rate = num_correct / number_done × 100
//    - Transition mastery_status (BR-25):
//      NOT_LEARNED → LEARNING if number_done >= 1
//      LEARNING → MASTERED if accuracy_rate >= 70% AND number_done >= 5
// 2. Recalculate CompetencyPoint.point for student's grade
// 3. Classify WeakTags (BR-24): P_tag < 5.0 → add to WeakTag list
// 4. Check Remedial Learning (BR-26): P_tag < 5.0 at Easy → flag isRemedial = true
// 5. Apply Difficulty Mapping (BR-29 — Dynamic Loop):
//    - For each WeakTag: compute recommendedPracticeDifficulty via DifficultyMappingService
//      Hard   → Medium
//      Medium → Easy
//      Easy   → Easy (Remedial: isRemedial=true, cap 10%)
//    - For each MASTERED tag: if P_tag >= 8.0 → compute suggestUpscaleTo
//      Easy   → suggestUpscaleTo = Medium, challengeMode = true
//      Medium → suggestUpscaleTo = Hard,   challengeMode = true
//      Hard   → suggestUpscaleTo = null,   challengeMode = true
// 6. Invalidate Redis cache:
//    DEL rcm:weak-tags:{student_id}
//    DEL rcm:weak-tag-advice:{student_id}
// 7. Publish CompetencyUpdatedEvent
```

### Redis Cache Keys

| Key | Value | TTL |
|-----|-------|-----|
| `rcm:weak-tags:{student_id}` | JSON list of `WeakTagDto` (lightweight) | 1 hour |
| `rcm:weak-tag-advice:{student_id}` | JSON list of `WeakTagAdviceDto` (full Dynamic Loop payload) | 1 hour |
| `rcm:recommendations:lectures:{student_id}` | JSON list of `LectureDto` (with `priority` field) | 1 hour |
| `rcm:recommendations:materials:{student_id}` | JSON list of `MaterialDto` (with `priority` field) | 1 hour |

Cache is invalidated on every `GradeCalculatedEvent` for that student (both `weak-tags` and `weak-tag-advice` keys).

### SAR Model (Hangfire Weekly Job)

```
Weekly Hangfire job:
1. Export interaction matrix from rcm.tags_mastery (student_id × tag_id × accuracy_rate)
2. Call Python script: python sar_train.py --input interactions.csv --output model.pkl
3. Run predictions for all active students
4. Write recommendation scores to Redis or temporary rcm.sar_recommendations table
```

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF migration applies cleanly.
3. Integration tests (xUnit):
   - `GradeCalculatedEvent` → `TagsMastery` updated correctly for each tag.
   - `accuracy_rate < 50%` after 5 attempts → `mastery_status = LEARNING` (not MASTERED).
   - `accuracy_rate >= 70%` after 5 attempts → `mastery_status = MASTERED`.
   - `P_tag < 5.0` at Medium → WeakTag with `recommendedPracticeDifficulty = Easy`.
   - `P_tag < 5.0` at Hard → WeakTag with `recommendedPracticeDifficulty = Medium`.
   - `P_tag < 5.0` at Easy → `isRemedial = true`, `recommendedPracticeDifficulty = Easy`, no further downscale.
   - `P_tag >= 8.0` at Medium + MASTERED → `suggestUpscaleTo = Hard`, `challengeMode = true`.
   - `P_tag >= 8.0` at Hard + MASTERED → `suggestUpscaleTo = null`, `challengeMode = true`.
   - `GetStudentWeakTagAdviceAsync()` → correct `WeakTagAdviceDto` list returned.
   - Redis `rcm:weak-tag-advice:{student_id}` set; cache-hit on second call (< 100ms).
   - Both cache keys invalidated on new `GradeCalculatedEvent`.
   - UC-52: GET /weak-tags returns correct WeakTags list.
   - UC-53: Remedial topics return lectures with `priority: REMEDIAL` sorted first.
   - UC-53/54: Non-remedial lectures/materials matched to WeakTag `tag_id`.