# Implementation Plan: Recommender Module

**Branch**: `005-recommender` | **Date**: 2026-06-23 | **Updated**: 2026-06-27 *(formula & SAR input corrected; Redis ordering fixed)*
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Recommender` — handles competency tracking (`CompetencyPoint`, `TagsMastery`), WeakTag diagnosis, SAR-based lecture/material recommendations, and internal API for TestGen. Consumes `GradeCalculatedEvent` from Grading module; caches results in Redis.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server; map to current DB script tables |
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
│   ├── GetRecommendedLectures/         # UC-53: match WeakTag TagID to Lecture.TagID
│   └── GetRecommendedMaterials/        # UC-54: match through LectureMaterial and Material
├── Persistence/
│   ├── RecommenderDbContext.cs         # maps to current DB script table names
│   ├── Configurations/
│   │   ├── CompetencyPointConfiguration.cs  # UNIQUE (student_id, grade)
│   │   └── TagsMasteryConfiguration.cs      # UNIQUE (student_id, tag_id, difficulty_id)
│   └── Migrations/
├── Controllers/
│   └── RecommenderController.cs        # UC-52, 53, 54 REST endpoints
└── RecommenderModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Key Indexes |
|-------|-------------|
| `CompetencyPoint` | UNIQUE `(StudentID, Grade)`; `Point` range check |
| `TagsMastery` | UNIQUE `(StudentID, TagID, DifficultyID)` |

> **SAR Item ID format — Combo Tag**: SAR treats each `(TagTopic, TagDifficulty)` pair as a single item.
> Item ID is constructed as `"{TopicCode}_{DifficultyCode}"`, e.g. `CALC_Integration_Hard`, `GEO_Polyhedrons_Medium`.
> This allows SAR to learn cross-difficulty similarity: a student weak at `CALC_Integration_Hard` will
> have high co-occurrence with `CALC_Integration_Medium`, naturally driving the difficulty downscale recommendation.

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
//   ChallengeMode                     — true when student is ready to be challenged:
//                                         • SuggestUpscaleToId is non-null (next tier available), OR
//                                         • already at Hard + MASTERED (max tier; challengeMode still true)
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
//    (average accuracy_rate across tags with number_done >= 1, normalized to 0–10)
// 3. INVALIDATE Redis cache BEFORE writing new results:
//    DEL rcm:weak-tags:{student_id}
//    DEL rcm:weak-tag-advice:{student_id}
// 4. Run DiagnoseWeakTags (BR-24, BR-26, BR-27, BR-29):
//    - Compute P_tag via exponential decay formula (β=0.8, k≤5 sessions) for each (student, tag, difficulty)
//    - Classify WeakTags (P_tag < 5.0) and apply Difficulty Mapping
//      Hard   → Medium
//      Medium → Easy
//      Easy   → Easy (Remedial: isRemedial=true, cap 10%)
//    - For each MASTERED tag: if P_tag >= 8.0 → compute suggestUpscaleTo
//      Easy   → suggestUpscaleTo = Medium, challengeMode = true
//      Medium → suggestUpscaleTo = Hard,   challengeMode = true
//      Hard   → suggestUpscaleTo = null,   challengeMode = true
//    - Write WeakTagAdviceDto list to rcm:weak-tag-advice:{student_id} (TTL 1h)
//    - Write WeakTagDto list to rcm:weak-tags:{student_id} (TTL 1h)
// 5. Publish CompetencyUpdatedEvent
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
1. Export implicit interaction event log from `TagsMastery` + session history:
   - Rows: (StudentID, ComboTagId, EventTimestamp, EventWeight)
   - ComboTagId = "{TopicCode}_{DifficultyCode}" (e.g. "CALC_Integration_Hard")
   - WeakTag-triggered interactions carry w_e = 2.0; normal interactions w_e = 1.0
2. Compute per-student Affinity scores (UNBOUNDED — A(u,i) ≥ 0, may exceed 1.0):
   A(u,i) = Σ(w_e * 2^(-Δt / T_half))  for all events of student u on combo-tag i
   (T_half default = 30 days; configurable)
   Note: A(u,i) is used for RANKING only — do not apply absolute thresholds.
3. Compute Tag–Tag Jaccard similarity matrix (S ∈ [0.0, 1.0]):
   S(j,i) = |U_j ∩ U_i| / |U_j ∪ U_i|
4. Compute recommendation scores (UNBOUNDED — R(u,i) ≥ 0):
   R(u,i) = Σ_j A(u,j) * S(j,i)
   Note: R(u,i) used for ranking only; select Top-K by score.
5. POST-FILTER: Keep only combo-tags where P_tag < 5.0 (WeakTag).
   Discard MASTERED tags (P_tag ≥ 5.0) from recommendation output.
   This prevents the system from recommending topics the student has already mastered.
6. Call Python script: python sar_train.py --input event_log.csv --output model.pkl
7. Update Redis key: sar:model:trained_at = NOW
8. Write Top-K filtered recommendation scores to Redis per student; do not add new DB table unless explicitly approved.
```

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
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
   - Both cache keys DEL-ed **before** `DiagnoseWeakTags()` writes on new `GradeCalculatedEvent`.
   - UC-52: GET /weak-tags returns correct WeakTags list.
   - UC-53: Remedial topics return lectures with `priority: REMEDIAL` sorted first.
   - UC-53/54: Non-remedial lectures/materials matched to WeakTag `tag_id`.
   - SAR post-filter: MASTERED combo-tags (P_tag >= 5.0) are **not** present in recommendation output.
   - Combo-tag ID format: `"{TopicCode}_{DifficultyCode}"` consistently applied across event log and recommendations.
