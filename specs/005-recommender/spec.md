# Feature Specification: Recommender Module

**Feature Branch**: `005-recommender`

**Created**: 2026-06-23 | **Updated**: 2026-07-04

**Status**: Approved

**Source Documents**: PRD §4 (FT-06), UCS UC-52-UC-54, algorithm report v2, schema migration 002

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-52 | View WeakTags | Student | Student views dashboard; backend queries current weak topics |
| UC-53 | View Recommended Lectures | Student | Based on WeakTags; returns matching lectures |
| UC-54 | View Recommended Materials | Student | Based on WeakTags; returns matching PDFs/materials |
| - | Update Topic Mastery | System | After a session is graded |
| - | Provide WeakTag Advice | TestGen module | `IRecommenderService.GetStudentWeakTagAdviceAsync()` |

### Edge Cases

- **No history**: If a student has no `TagsMastery` row for a topic, create it lazily with neutral points (`5.00`) or exclude it from WeakTags until data exists.
- **All topics strong**: If no topic has `official_point < 5.00`, WeakTags is empty; TestGen may use normal blueprint/topic practice.
- **Repeated grade event**: Recommender update must be idempotent per `(session_id, tag_id)` by using `StudentTopicSessionResult`.
- **Low point at easiest mapped difficulty**: Keep recommended difficulty at level 1 and mark `is_remedial = true`.
- **Redis/SAR not available**: MVP must still work from SQL Server only. Redis/SAR are future optimizations, not required infrastructure.

## Requirements *(mandatory)*

### Functional Requirements

- **RCM-01**: `TagsMastery` stores mastery at grain `(student_id, tag_id)`, not `(student_id, tag_id, difficulty_id)`.
- **RCM-02**: `TagsMastery.official_point`, `practice_point`, and `exam_anchor` must stay in range `0.00..10.00`.
- **RCM-03**: WeakTag classification is derived, not stored as a separate status: `isWeak = official_point < 5.00`.
- **RCM-04**: `official_point` is calculated from the role-based formula:

```text
official_point = 0.7 * exam_anchor + 0.3 * practice_point
```

- **RCM-05**: `exam_anchor` is updated from official/graded sessions using recent per-topic session results. MVP may use the latest result or a weighted average of up to 5 recent results.
- **RCM-06**: `practice_point` is updated during practice/adaptive sessions. After each practice series reaches 10 answers for a topic, blend into `official_point`, reset `practice_point = official_point`, and reset `series_answer_count = 0`.
- **RCM-07**: `recommended_difficulty_level` is derived from `official_point`:

| official_point | recommended_difficulty_level |
|---|---|
| `0.00 <= p < 3.00` | `1` |
| `3.00 <= p < 5.00` | `2` |
| `5.00 <= p < 7.50` | `3` |
| `7.50 <= p <= 10.00` | `4` |

- **RCM-08**: `StudentTopicSessionResult` stores the per-session per-topic snapshot used to update `TagsMastery`. This is required for audit and idempotency.
- **RCM-09**: TestGen reads Recommender advice in-process. No external recommender service is required for MVP.
- **RCM-10**: Lecture/material recommendations are simple rule-based matches from weak `tag_id` to `Lecture.TagID` and `LectureMaterial`.
- **RCM-11**: `mastery_status` remains a coarse learning label only: `NotLearned`, `Learning`, `Mastered`. Do not add `WeakTag` to this enum.

### Key Entities *(include if feature involves data)*

- **CompetencyPoint**: `competency_id`, `student_id`, `grade`, `point` (`0.00..10.00`), unique `(student_id, grade)`.
- **TagsMastery**: `tags_mastery_id`, `student_id`, `tag_id`, `mastery_status`, `number_done`, `num_correct`, `accuracy_rate`, `official_point`, `practice_point`, `exam_anchor`, `exam_history`, `series_answer_count`, `recommended_difficulty_level`, `last_calculated_at`, unique `(student_id, tag_id)`.
- **StudentTopicSessionResult**: `student_topic_session_result_id`, `student_id`, `session_id`, `tag_id`, `total_questions`, `correct_count`, `wrong_count`, `topic_score`, `point_before`, `point_after`, `created_time`, unique `(session_id, tag_id)`.

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| TagsMastery | mastery_status | `NotLearned`, `Learning`, `Mastered` |
| TagsMastery | recommended_difficulty_level | `1`, `2`, `3`, `4` |
| CompetencyPoint | grade | `10`, `11`, `12` |

### Internal API Contract

```csharp
public interface IRecommenderService
{
    Task<IReadOnlyList<WeakTagDto>> GetStudentWeakTagsAsync(Guid studentId);
    Task<IReadOnlyList<WeakTagAdviceDto>> GetStudentWeakTagAdviceAsync(Guid studentId);
}
```

```csharp
public sealed record WeakTagAdviceDto(
    Guid TagId,
    string TagName,
    decimal OfficialPoint,
    bool IsWeak,
    byte RecommendedDifficultyLevel,
    bool IsRemedial,
    string Reason
);
```

`Reason` is for audit/debugging, for example `OfficialPointBelow5`, `RemedialLevel1`, or `NormalPractice`.

## Success Criteria *(mandatory)*

- WeakTag API returns within **2 seconds** from SQL Server without Redis.
- Topic mastery update completes within **5 seconds** after grading.
- `official_point`, `practice_point`, `exam_anchor`, and `CompetencyPoint.point` never exceed `0.00..10.00`.
- No separate `rcm` schema is created for MVP; backend maps to current DB script tables.
- No Python/SAR service or Redis is required for MVP operation.

## Assumptions

- Target database is SQL Server.
- Grading module provides per-topic session results after `TestSession.status = Graded`.
- Recommender updates can run in-process after grading succeeds.
- Redis/SAR can be added later as performance or ranking improvements, but they must not be MVP dependencies.
