# Feature Specification: Recommender Module

**Feature Branch**: `005-recommender`

**Created**: 2026-06-23 | **Updated**: 2026-06-27 *(Dynamic Test Generator Loop added; formula & SAR input corrected)*

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
- **SAR model not trained yet**: Fall back to rule-based WeakTag diagnosis from `TagsMastery` directly. Trigger condition: model file `model.pkl` is missing **or** Redis key `sar:model:trained_at` is older than 8 days.
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
  - **Inputs**: Implicit interaction event log — rows=`(student_id, tag_id, event_timestamp)`, weighted by event type (`w_e`). Affinity score per student–tag pair is computed as:
    $$A(u,i) = \sum_{\text{events}} w_e \cdot 2^{-\Delta t / T_{\text{half}}}$$
    where $\Delta t$ is time elapsed since the event and $T_{\text{half}}$ is the half-life constant (configurable, default 30 days). WeakTag events carry boosted weight $w_e = 2.0$ to amplify recency signal for weak topics.
  - **Tag–Tag Similarity**: Jaccard similarity between interaction sets:
    $$S(j,i) = \frac{|U_j \cap U_i|}{|U_j \cup U_i|}$$
  - **Recommendation Score**: $R(u,i) = \sum_j A(u,j) \cdot S(j,i)$ — propagates affinity through the tag similarity graph.
  - **Model Training**: Background scheduler (Hangfire, weekly) trains SAR on historical event log data.
  - **Output**: Top-K recommendation scores per student for untaken/low-mastery tags; post-filtered to keep only tags where `P_tag < 5.0`.
  - **Cache**: Recommendations stored in Redis per student (`rcm:weak-tags:{student_id}`).
- **BR-29 (Dynamic Test Generator Loop — Internal API)**: The Recommender module exposes `IRecommenderService` as an **in-process DI interface** for the TestGen module (009). This interface provides two distinct outputs:
  1. **`GetStudentWeakTagAdviceAsync(Guid studentId)`** — returns a `WeakTagAdviceDto` list, where each entry contains:
     - `tagId`, `tagName` — the topic.
     - `currentDifficultyId`, `currentDifficultyName` — the difficulty at which the weakness was diagnosed (e.g., `Hard`).
     - `recommendedPracticeDifficultyId`, `recommendedPracticeDifficultyName` — the difficulty TestGen should use to select questions (e.g., if diagnosed weak at `Hard` → recommend practicing at `Medium`; if diagnosed weak at `Medium` → recommend `Easy`). This prevents student fatigue from repeated failure at a difficulty they are not yet ready for.
     - `isRemedial` (bool) — `true` if the weakness is at `Easy` level. When `true`: no further difficulty downgrading is applied; TestGen limits this topic's selection bias to **10%** of total questions; the content recommendation engine treats this as a critical gap and returns foundational lectures/materials first.
     - `suggestUpscaleTo` (nullable) — populated when `P_tag ≥ 8.0` AND `mastery_status = MASTERED`. Contains the next difficulty level (e.g., `Hard`). Used by TestGen to optionally include harder questions for challenge mode.
     - `challengeMode` (bool) — `true` when the student is ready to be challenged: either `suggestUpscaleTo` is populated (next tier available) **or** the student is already at the highest difficulty and `P_tag ≥ 8.0 AND MASTERED` (no higher tier exists but challenge mode still applies).
  2. **`GetStudentWeakTagsAsync(Guid studentId)`** — lightweight version returning raw `WeakTagDto` list (tagId, isRemedial) for simple lookup.
- **BR-30**: Lecture/Material recommendations are derived by matching WeakTag `TagID` to `Lecture.TagID` and materials through `LectureMaterial`. Priority sort order for UC-53 and UC-54 responses:
  1. `priority: REMEDIAL` — tags where `isRemedial = true` (foundational videos/materials first).
  2. `priority: WEAK` — tags where `P_tag < 5.0` but not Remedial (standard WeakTag lectures).
  3. `priority: NORMAL` — tags in LEARNING state (`5.0 ≤ P_tag < 8.0`, `number_done ≥ 1`) — lectures are still recommended to reinforce progress, sorted last.

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
─── P_tag Calculation (Công thức 1 — Exponential Decay) ──────────
P_tag = Σ(β^(j-1) * T_j) / Σ(β^(j-1))   for j = 1..k

where:
  k   = number of recent sessions for this (student, tag, difficulty), capped at 5
  T_j = score of session j on a 0–10 scale (j=1 is most recent, j=k is oldest)
  β   = 0.8 (time-decay factor — models Ebbinghaus Forgetting Curve)

Weights: β^0=1.0, β^1=0.8, β^2=0.64, β^3=0.512, β^4=0.4096
P_tag ∈ [0.0, 10.0].  Cold-start (k=1): P_tag = T_1.

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

─── LEARNING Zone (not WeakTag, not yet MASTERED) ────────────────
if 5.0 <= P_tag < 8.0 AND number_done >= 1
    → status = LEARNING: no WeakTag flag.
      ContentRecommendation priority = NORMAL (lectures still shown, sorted last).
      No difficulty mapping applied.

─── Upscale Suggestion ───────────────────────────────────────────
if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Easy
    → suggestUpscaleTo = Medium, challengeMode = true

if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Medium
    → suggestUpscaleTo = Hard, challengeMode = true

if P_tag >= 8.0 AND mastery_status = MASTERED AND difficulty = Hard
    → suggestUpscaleTo = null, challengeMode = true
      (already at max difficulty; challengeMode signals TestGen to apply
       challenge-variant questions even without an upscale tier)
```

### WeakTagAdviceDto Contract (returned by BR-29 internal API)

```csharp
public record WeakTagAdviceDto
(
    Guid   TagId,
    string TagName,
    Guid   CurrentDifficultyId,
    string CurrentDifficultyName,
    Guid?  RecommendedPracticeDifficultyId,   // null only when entry is MASTERED (no weakness; upscale advice only)
                                               // Easy-Remedial still returns Easy difficulty ID (not null)
    string RecommendedPracticeDifficultyName,
    bool   IsRemedial,                         // true → Easy-level critical gap (P_tag < 5.0 at Easy)
    Guid?  SuggestUpscaleToId,                 // non-null when P_tag >= 8.0 AND MASTERED AND next tier exists
                                               // null when already at Hard (max difficulty) — challengeMode still true
    string SuggestUpscaleToDifficultyName,
    bool   ChallengeMode                       // true when student is ready to be challenged:
                                               //   • SuggestUpscaleToId is set (next tier available), OR
                                               //   • already at Hard + MASTERED (max tier, challenge-only mode)
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
