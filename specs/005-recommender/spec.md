# Feature Specification: Recommender Module

> **Approved scoring amendment**: [Scoring Contract V2](../scoring-contract-v2.md) defines revision-aware weighted topic input.

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

- **No history**: If the grading pipeline encounters a `(student_id, tag_id)` pair with no existing `TagsMastery` row, **lazy-create** it with `official_point = 5.00` (neutral/unknown). The row is inserted with `mastery_status = NotLearned`, `number_done = 0`, and `series_answer_count = 0`. **Trigger**: lazy-create happens only when the Recommender processes a `GradeCalculatedEvent` that references a tag the student has never been graded on before. Topics with no `TagsMastery` row stay neutral and are **not** treated as weak — `GetStudentWeakTagsAsync` only returns rows where `official_point < 5.00`, so topics without a row are correctly excluded until real graded data arrives.
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

- **RCM-05**: `exam_anchor` is updated from Exam format sessions (using `GradeCalculatedEvent.TestFormat == "Exam"`) using an **Exponential Decay weighted average** over the `k ≤ 5` most recent per-topic session scores:

  ```text
  exam_anchor = Σ(j=1→k) [β^(j-1) × T_j]  /  Σ(j=1→k) [β^(j-1)]
  ```

  Where:
  - `T_j` — topic score (0–10) of the j-th most recent graded session (j=1 is the latest)
  - `k ≤ 5` — sliding window of up to 5 recent sessions stored in `exam_history` (JSON array)
  - `β = 0.8` — exponential decay factor (Ebbinghaus Forgetting Curve)

  **`exam_history` ordering contract (I2)**: `exam_history` is a JSON array stored on `TagsMastery`.
  - `exam_history[0]` = most recent session score (j=1, weight β⁰ = 1.0).
  - `exam_history[k-1]` = oldest session score in the window.
  - On each update: **prepend** the new score; if `len > 5`, remove the last element.
  - This ordering is mandatory — the formula's weight assignment depends on it.

  Decay weights: β⁰ = 1.0 → β¹ = 0.8 → β² = 0.64 → β³ = 0.512 → β⁴ = 0.410
- **RCM-06**: `practice_point` is updated sequentially and retrospectively per-answer after a Practice format session is submitted and graded (using `GradeCalculatedEvent.TestFormat == "Practice"`) (F4 resolution). The calculation processes the session's answers in sequential order of their `question_no` (using the detailed answers provided in `GradeCalculatedEvent.Answers` which includes `QuestionNo` and `IsAbandoned` fields) (F1 resolution):

  ```text
  If CORRECT:  practice_point(t+1) = min(10.0,  practice_point(t) + α × w_D × γ_time)
  If WRONG:    practice_point(t+1) = max(0.0,   practice_point(t) − α × (5 − w_D) × γ_time_penalty)
  ```

  Where:
  - `α = 0.05` — base learning rate (K-factor, inspired by Elo rating system)
  - `w_D ∈ {0.5, 1.0, 1.5, 2.0}` — difficulty weight for levels 1–4 (inspired by IRT)
  - `γ_time = 1.0` — normal time multiplier
  - `γ_time_penalty = 1.5` — guessing penalty when answer time `t < 5 seconds` and the student actively selected an answer or input short answer text. For unanswered/abandoned questions (no student selection), `γ_time_penalty = 1.0` (no penalty).

  After `series_answer_count` reaches **10** for a topic, the accumulated practice gains are incorporated into `official_point`, then `practice_point` is reset to the new baseline:

  ```text
  official_point  = 0.7 × exam_anchor + 0.3 × practice_point   ← blend (already calculated)
  practice_point ← official_point                               ← reset to new baseline
  series_answer_count ← 0
  ```

  **Design intent (A1 clarification)**: This is NOT erasing progress. Practice gains accumulated over the 10-answer series have already been permanently incorporated into `official_point` through the blend formula. Resetting `practice_point ← official_point` sets the new starting point for the *next* series at the student's current level. The student's real progress is preserved in `official_point` and `exam_anchor`.
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
- **RCM-12 (CompetencyPoint update)**: After each `TagsMastery` update for a student, recalculate `CompetencyPoint` for that student's grade level:

  ```text
  CompetencyPoint.point = AVERAGE(official_point)
                          for all TagsMastery rows of that student
                          where the Tag belongs to the student's grade (10, 11, or 12)
  ```

  To determine the student's grade, the Recommender queries the cross-schema `Student` table from the Identity module (`Student.current_grade`) (F5 resolution).
  Upsert `CompetencyPoint` using `(student_id, grade)`. Clamp result to `0.00..10.00`.

- **RCM-13 (mastery_status thresholds)**: Set `mastery_status` based on the following rules applied after each `TagsMastery` update:

  | Condition | `mastery_status` |
  |-----------|------------------|
  | `number_done = 0` | `NotLearned` |
  | `number_done > 0` AND `official_point < 7.50` | `Learning` |
  | `official_point >= 7.50` | `Mastered` |

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

> **Cross-module resolution contract**: `WeakTagAdviceDto.RecommendedDifficultyLevel` is a **level integer** (`1..4`, see RCM-07). It is **not** a `difficulty_id` (the PK of `TagDifficulty` table in QuestionBank module). Consumers such as TestGen **must** resolve this level to an actual `DifficultyID` by querying:
> ```sql
> SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = @RecommendedDifficultyLevel
> ```
> This resolution is stable because `TagDifficulty.LevelValue` has a UNIQUE constraint (BR-63 in module 002).

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
