# Feature Specification: Recommender Module

**Feature Branch**: `005-recommender`

**Created**: 2026-06-23 | **Updated**: 2026-06-26 *(Dynamic Test Generator Loop added)*

**Status**: Approved

**Source Documents**: PRD §4 (FT-06), UCS UC-52–UC-54, TDS §2.2, §2.4 (Recommender layer)

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-52 | View WeakTags | Student | Student views dashboard — queries current weak topics |
| UC-53 | View Recommended Lectures | Student | Based on WeakTags → returns matching lectures |
| UC-54 | View Recommended Materials | Student | Based on WeakTags → returns matching PDFs/materials |
| — | Update Competency (internal) | System | `GradeCalculatedEvent` consumed from Grading module |
| — | **Provide WeakTag Advice (internal)** | **TestGen module** | `IRecommenderService.GetStudentWeakTagAdviceAsync()` — supplies difficulty mapping + upscale suggestions |

### Edge Cases

- **No test history**: Student has no `TagsMastery` records → return empty WeakTags; recommend introductory lectures.
- **All topics mastered**: No WeakTags (all `P_tag ≥ 5.0`) → return empty list; suggest challenge-level topics.
- **Remedial Learning**: If `P_tag < 5.0` at `Easy` level → flag as `Remedial_Learning`; no further downscaling; representation in TestGen capped to **10%** bias; lecture/material recommendations switch to **high-priority foundational** mode (sorted first, tagged `priority: REMEDIAL`).
- **SAR model not trained yet**: Fall back to rule-based WeakTag diagnosis from `TagsMastery` directly.
- **Upscale suggestion with no harder difficulty available**: If student is already at the highest difficulty and `P_tag ≥ 8.0` → return `suggestUpscaleTo: null` with a `challengeMode: true` flag instead.

## Requirements *(mandatory)*

### Functional Requirements

- **DC-04**: Competency points and mastery scores must fall strictly within **0.0 – 10.0** (or 0%–100%). Out-of-bounds values trigger error logs.
- **BR-23**: Competency updates are triggered **immediately** after test grading via `GradeCalculatedEvent`. Not batch-processed.
- **BR-24**: A topic-difficulty combination is classified as **WeakTag** when `P_tag < 5.0` (mastery index below threshold).
- **BR-25**: `TagsMastery.mastery_status` transitions:
  - `NOT_LEARNED` → `LEARNING` when `number_done >= 1`
  - `LEARNING` → `MASTERED` when `accuracy_rate >= 70%` AND `number_done >= 5`
- **BR-26 (Remedial Learning)**: If a student's `P_tag < 5.0` at `Easy` difficulty, it is flagged `Remedial_Learning`. This:
  - Halts further difficulty downgrading for that topic.
  - Reduces the topic's representation bias in TestGen to **10%**.
  - Triggers high-priority recommendations for foundational video lectures and basic materials.
- **BR-27 (Difficulty Upscaling)**: If `P_tag ≥ 8.0` and `mastery_status = MASTERED`, suggest upscaling to next difficulty level (e.g., `GEO_Polyhedrons_Medium` → `GEO_Polyhedrons_Hard`). This suggestion is surfaced via the internal API (`suggestUpscaleTo`) and used by TestGen to optionally challenge the student.
- **BR-28 (SAR Recommendation)**: The SAR (Smart Adaptive Recommendation) algorithm from `recommenders` library:
  - **Inputs**: User interaction matrix — rows=`student_id`, columns=`topic_tag_id`, values=`accuracy_rate`.
  - **Model Training**: Background scheduler (Hangfire, weekly) trains SAR on historical data.
  - **Output**: Affinity scores for untaken/low-mastery tags.
  - **Cache**: Recommendations stored in Redis per student (`rcm:weak-tags:{student_id}`).
- **BR-29 (Dynamic Test Generator Loop — Internal API)**: The Recommender module exposes `IRecommenderService` as an **in-process DI interface** for the TestGen module (009). This interface provides two distinct outputs:
  1. **`GetStudentWeakTagAdviceAsync(Guid studentId)`** — returns a `WeakTagAdviceDto` list, where each entry contains:
     - `tagId`, `tagName` — the topic.
     - `currentDifficultyId`, `currentDifficultyName` — the difficulty at which the weakness was diagnosed (e.g., `Hard`).
     - `recommendedPracticeDifficultyId`, `recommendedPracticeDifficultyName` — the difficulty TestGen should use to select questions (e.g., if diagnosed weak at `Hard` → recommend practicing at `Medium`; if diagnosed weak at `Medium` → recommend `Easy`). This prevents student fatigue from repeated failure at a difficulty they are not yet ready for.
     - `isRemedial` (bool) — `true` if the weakness is at `Easy` level. When `true`: no further difficulty downgrading is applied; TestGen limits this topic's selection bias to **10%** of total questions; the content recommendation engine treats this as a critical gap and returns foundational lectures/materials first.
     - `suggestUpscaleTo` (nullable) — populated when `P_tag ≥ 8.0` AND `mastery_status = MASTERED`. Contains the next difficulty level (e.g., `Hard`). Used by TestGen to optionally include harder questions for challenge mode.
     - `challengeMode` (bool) — `true` when `suggestUpscaleTo` is populated, signaling TestGen to bias toward the harder tier.
  2. **`GetStudentWeakTagsAsync(Guid studentId)`** — lightweight version returning raw `WeakTagDto` list (tagId, isRemedial) for simple lookup.
- **BR-30**: Lecture/Material recommendations are derived by matching WeakTag `TagID` to `Lecture.TagID` and materials through `LectureMaterial`. When `isRemedial = true`, the result set is sorted with `priority: REMEDIAL` items first, ensuring foundational videos and reading materials appear at the top of UC-53 and UC-54 responses.

### Key Entities *(include if feature involves data)*

- **CompetencyPoint**: `competency_id`, `student_id` (FK), `grade` (10/11/12), `point` (DECIMAL 5,2, range 0.00–10.00) — composite UNIQUE `(student_id, grade)`
- **TagsMastery**: `tags_mastery_id`, `student_id` (FK), `tag_id` (FK → tag_topics), `difficulty_id` (FK → tag_difficulties), `mastery_status` (**NOT_LEARNED** | **LEARNING** | **MASTERED**), `number_done`, `num_correct`, `accuracy_rate` (DECIMAL 5,2), `last_practiced_time` — composite UNIQUE `(student_id, tag_id, difficulty_id)`

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| TagsMastery | mastery_status | `NOT_LEARNED`, `LEARNING`, `MASTERED` |
| CompetencyPoint | grade | `10`, `11`, `12` |

### WeakTag Diagnosis & Difficulty Mapping Logic

```
P_tag = accuracy_rate (from TagsMastery)

─── WeakTag Classification ───────────────────────────────────────
if P_tag < 5.0 AND difficulty = Hard
    → WeakTag: recommendedPracticeDifficulty = Medium
      (student practices at Medium to rebuild confidence)

if P_tag < 5.0 AND difficulty = Medium
    → WeakTag: recommendedPracticeDifficulty = Easy
      (student practices at Easy before retrying Medium)

if P_tag < 5.0 AND difficulty = Easy
    → Remedial_Learning (CRITICAL GAP):
        isRemedial = true
        recommendedPracticeDifficulty = Easy (no further downscale)
        TestGen bias cap = 10% of total questions for this topic
        ContentRecommendation priority = REMEDIAL (foundational first)

─── Upscale Suggestion ───────────────────────────────────────────
if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Easy
    → suggestUpscaleTo = Medium, challengeMode = true

if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Medium
    → suggestUpscaleTo = Hard, challengeMode = true

if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Hard
    → suggestUpscaleTo = null, challengeMode = true
      (already at max difficulty; signal challenge mode only)
```

### WeakTagAdviceDto Contract (returned by BR-29 internal API)

```csharp
public record WeakTagAdviceDto
(
    Guid   TagId,
    string TagName,
    Guid   CurrentDifficultyId,
    string CurrentDifficultyName,
    Guid?  RecommendedPracticeDifficultyId,   // null only when isRemedial already at Easy
    string RecommendedPracticeDifficultyName,
    bool   IsRemedial,                         // true → Easy-level critical gap
    Guid?  SuggestUpscaleToId,                 // null if no upscale or not MASTERED
    string SuggestUpscaleToDifficultyName,
    bool   ChallengeMode                       // true when SuggestUpscaleToId is set
);
```

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Competency update completes within **5 seconds** of `GradeCalculatedEvent` received.
- WeakTag API returns within **2 seconds** (served from Redis cache on cache-hit).
- SAR model training completes weekly without blocking API.
- `P_tag` values always within 0.00–10.00 (DC-04).
- Backend maps Recommender entities to the current SQL script tables; no separate `rcm` schema is created for MVP.

## Assumptions

- Target database is SQL Server. Backend maps to current DB script tables (`CompetencyPoint`, `TagsMastery`, `Lecture`, `Material`, `LectureMaterial`) instead of schema-prefixed tables.
- Redis available for WeakTag cache (`rcm:weak-tags:{student_id}`).
- SAR model executed via Python script runner (or microservice) — C# module calls Python subprocess or HTTP endpoint.
- Grading module (004) publishes `GradeCalculatedEvent`; this module consumes it.
- Learning module (006) owns `Lecture`, `Material`, and `LectureMaterial`; Recommender reads those current DB script tables.
