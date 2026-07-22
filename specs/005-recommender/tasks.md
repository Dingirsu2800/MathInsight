# Tasks Checklist: Recommender Module

## Scoring Contract V2

- [x] Persist and compare GradeRevision per session/topic snapshot.
- [x] Consume earned/max weighted topic totals and replace newer revisions.
- [x] Replay affected mastery without applying scoring weight as `w_D`.

**Branch**: `005-recommender` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

> **Updated**: 2026-07-20 - aligned with Unified Multi-Tag Ptag v4.1 and migration 002

---

## Phase 1: Persistence Setup

- [x] Create EF `IEntityTypeConfiguration` for current DB script tables:
  - [x] `CompetencyPointConfiguration` - unique `(student_id, grade)`; `point` range `0.00..10.00`
  - [x] `TagsMasteryConfiguration` - unique `(student_id, tag_id)`; no `difficulty_id`
  - [x] `StudentTopicSessionResultConfiguration` - unique `(session_id, tag_id)`; values non-negative; `topic_score` range `0.00..10.00`
- [x] Map `TagsMastery` fields: `official_point`, `practice_point`, `exam_anchor`, `exam_history`, `series_answer_count`, `recommended_difficulty_level`, `last_calculated_at`.
- [x] Create `RecommenderDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Do not add EF migration unless the team switches from SQL script source-of-truth to EF migration source-of-truth.
- [x] Seed sample mastery rows at grain `(student_id, tag_id)`.

---

## Phase 2: Core Domain Logic

- [x] **TopicResultIngestionHandler**:
  - [x] Receive graded per-topic results from Grading module.
  - [x] Insert `StudentTopicSessionResult` per `(session_id, tag_id)`.
  - [x] Skip duplicate `(session_id, tag_id)` rows to keep update idempotent.

- [x] **CompetencyEngine**:
  - [x] `UpdateTagsMasteryFromSessionResult(studentId, tagId, topicScore, testMode, answers)`:
    - Upsert `TagsMastery(student_id, tag_id)`.
    - Update `number_done`, `num_correct`, `accuracy_rate`.
    - If official/exam result (TestFormat == "Exam"): update `exam_anchor` using Exponential Decay (RCM-05):
      - Prepend `topic_score` to `exam_history` (JSON array), keep at most 5 entries.
      - **Ordering contract (I2)**: `exam_history[0]` = most recent score (j=1); `exam_history[k-1]` = oldest. Always prepend new score; truncate last entry when `len > 5`. This ordering is mandatory for the Exponential Decay formula where j=1 must be the latest.
      - `exam_anchor = Σ(β^(j-1) × T_j) / Σ(β^(j-1))` with `β = 0.8`, `j=1` = latest (`history[0]`).
    - If practice/adaptive result (TestFormat == "Practice"): update `practice_point` using multi-tag Elo formula (RCM-06 v4.1) sequentially and retrospectively for each answer:
      - **Bước 1**: Compute Δ_total (unchanged):
        - Correct: `Δ_total = +0.05 × w_D × γ_time`
        - Wrong:   `Δ_total = -0.05 × (5 − w_D) × γ_time_penalty`
      - **Bước 2**: For EACH tag in `answer.TagWeights`:
        - `ΔP_tag_i = Δ_total × w_i`
        - `practice_point_i = clamp(practice_point_i + ΔP_tag_i, 0, 10)`
        - Increment `series_answer_count` per-tag independently
      - Degenerate case (single-tag, w_i=1.0): identical to MVP
      - `w_D ∈ {0.5, 1.0, 1.5, 2.0}` for difficulty levels 1–4.
      - `γ_time_penalty = 1.5` when answer time `t < 5 seconds` and not abandoned (`!IsAbandoned`); otherwise `γ_time = 1.0`.
      - Increment `series_answer_count`.
    - Recalculate `official_point = 0.7 * exam_anchor + 0.3 * practice_point`.
    - Recalculate `recommended_difficulty_level`.
    - Update `mastery_status`: `NotLearned`, `Learning`, `Mastered`.
    - Update `last_calculated_at`.
  - [x] Clamp all points to `0.00..10.00`.
  - [x] **Lazy-create** `TagsMastery` when no row exists for `(student_id, tag_id)` (U3 — RCM no-history rule):
    - Insert with `official_point = 5.00`, `practice_point = 5.00`, `exam_anchor = 5.00`.
    - Set `mastery_status = NotLearned`, `number_done = 0`, `series_answer_count = 0`.
  - [x] Update `mastery_status` using RCM-13 thresholds after each update:
    - `number_done = 0` → `NotLearned`
    - `number_done > 0` AND `official_point < 7.50` → `Learning`
    - `official_point >= 7.50` → `Mastered`

- [x] **Practice Series Logic**:
  - [x] When `series_answer_count >= 10`, blend:
    - `official_point = 0.7 * exam_anchor + 0.3 * practice_point`
    - `practice_point = official_point`
    - `series_answer_count = 0`

- [x] **CompetencyEngine — CompetencyPoint recalculation** (G1 — RCM-12):
  - [x] After each `TagsMastery` upsert, query all `TagsMastery.official_point` rows for `(student_id)` where the Tag's grade matches the student's grade level. Query `Student.current_grade` from the Identity module cross-schema (F5 resolution).
  - [x] `CompetencyPoint.point = AVERAGE(official_point)` for that grade. Clamp to `0.00..10.00`.
  - [x] Upsert `CompetencyPoint` by unique key `(student_id, grade)`.

- [x] **DifficultyMappingService**:
  - [x] `MapFromOfficialPoint(officialPoint)` returns level `1..4` (RCM-07).
  - [x] `IsWeak(officialPoint)` returns true when `< 5.00`.
  - [x] `IsRemedial(recommendedDifficultyLevel)` returns true when level `1` and weak.
  - [x] Cross-module contract: `WeakTagAdviceDto.RecommendedDifficultyLevel` is a level integer `1..4`.
    - Consumers (TestGen) **must not** use this value directly as a `difficulty_id` (PK of `TagDifficulty`).
    - Resolution: `SELECT DifficultyID FROM TagDifficulty WHERE LevelValue = RecommendedDifficultyLevel`.
    - This mapping is stable because `TagDifficulty.LevelValue` has a UNIQUE constraint (BR-63).

- [x] **RecommenderService**:
  - [x] `GetStudentWeakTagsAsync(studentId)` reads `TagsMastery` where `official_point < 5.00`.
    - **WeakTag tag type**: `TagsMastery.TagId` refers to `TagTopic` (topic tags). WeakTag evaluation is always per-topic, not per-difficulty.
    - **No-row behavior (MVP)**: Topics with no `TagsMastery` row are **not** returned as weak. They stay neutral until the first graded data point triggers lazy-create (see Phase 2 → CompetencyEngine → Lazy-create task). A newly created row starts at `official_point = 5.00` (above the `< 5.00` weak threshold), so it will not appear in WeakTags until real grading data lowers the score.
  - [x] `GetStudentWeakTagAdviceAsync(studentId)` returns `WeakTagAdviceDto` with `official_point`, `recommended_difficulty_level`, `is_remedial`, and reason.
  - [x] Keep SQL-only implementation for MVP; Redis cache is optional later.

- [x] **Recommendation Queries**:
  - [x] `GetWeakTagsQuery` - UC-52.
  - [x] `GetRecommendedLecturesQuery` - UC-53: match `Lecture.TagID` to weak `TagID`; remedial topics sorted first.
  - [x] `GetRecommendedMaterialsQuery` - UC-54: match materials through `LectureMaterial`; remedial topics sorted first.

---

## Phase 3: Controller and Routing

- [x] `RecommenderController` - StudentOnly:
  - [x] `GET /api/v1/recommender/weak-tags`
  - [x] `GET /api/v1/recommender/lectures`
  - [x] `GET /api/v1/recommender/materials`
  - [x] Enforce `[Authorize(Roles = "Student")]` on all three endpoints (G2).
- [x] Register inside `RecommenderModuleExtensions.cs`:
  - DbContext, CompetencyEngine, DifficultyMappingService, RecommenderService, MediatR handlers.
- [x] Do not require Redis, Python, SAR, Hangfire, or separate recommender service for MVP.

---

## Phase 5: Unified Multi-Tag v4.1 Upgrade

- [ ] **Multi-tag Elo Delta Distribution** (RCM-06 Bước 2):
  - [ ] Parse `answer.TagWeights` (list of `TagWeightEntry`) from `GradeCalculatedEvent.Answers`.
  - [ ] For each answer, compute `Δ_total` once (Bước 1, unchanged).
  - [ ] For each tag in `answer.TagWeights`: `ΔP_tag_i = Δ_total × w_i`; update that tag's `practice_point`.
  - [ ] Increment `series_answer_count` independently for each tag.
  - [ ] Verify degenerate case: single-tag (w_i=1.0) produces identical results to MVP.

- [ ] **Weighted Exam TopicScore** (RCM-05 Tầng 1–2):
  - [ ] Use `TopicGradeResult.TopicScore` (now pre-calculated with weighted T_j^{(i)} by Grading module).
  - [ ] No formula change needed in handler — TopicScore arrives already weighted.

- [ ] **Bottleneck Weak Tag** (BR-19, RCM-14):
  - [ ] Add `IsBottleneckWeak(officialPoint)` to `IDifficultyMappingService` / `DifficultyMappingService` returning true when `< 4.00`.
  - [ ] Update `RecommenderService.GetStudentWeakTagsAsync()` to include tags with `official_point < 4.00` with reason `BottleneckSubTag`.
  - [ ] Update `WeakTagAdviceDto` or response mapping to flag `IsBottleneckWeak`.

- [ ] **Multi-tag Exam PerTagResults ingestion**:
  - [ ] PerTagResults now contains entries for ALL tags (primary + secondary). Handler must iterate all of them.
  - [ ] Insert `StudentTopicSessionResult` for each `(session_id, tag_id)` pair — more rows per session than MVP.

---

## Phase 4: Verification

- [x] `dotnet build` - zero compile errors.
- [x] Unit tests:
  - [x] `official_point = 0.7 * exam_anchor + 0.3 * practice_point`.
  - [x] `official_point < 5.00` returns WeakTag.
  - [x] Difficulty mapping: `2.99 -> 1`, `3.00 -> 2`, `4.99 -> 2`, `5.00 -> 3`, `7.49 -> 3`, `7.50 -> 4`.
  - [x] Practice series count `10` resets `practice_point` and `series_answer_count`.
  - [x] **exam_anchor — Exponential Decay**:
    - 1 result: `exam_anchor = T1`.
    - 2 results: `exam_anchor = (T1 + 0.8×T2) / (1 + 0.8)`.
    - 5 results: weights `1.0, 0.8, 0.64, 0.512, 0.410`; older result beyond k=5 is ignored.
    - `exam_history` is capped at 5 entries after each update.
  - [x] **practice_point — Elo formula**:
    - Correct, level 2 (`w_D=1.0`), normal time: `Δ = +0.05`.
    - Wrong, level 1 (`w_D=0.5`), normal time: `Δ = −0.05×(5−0.5) = −0.225`.
    - Wrong, level 4 (`w_D=2.0`), normal time: `Δ = −0.05×(5−2.0) = −0.150`.
    - Wrong, level 1 (`w_D=0.5`), `t < 5s` (guessing): `Δ = −0.05×4.5×1.5 = −0.3375`.
    - Result never exceeds `10.0` or drops below `0.0`.
- [x] Integration tests:
  - [x] Duplicate `(session_id, tag_id)` result does not double-update `TagsMastery`.
  - [x] Graded session updates `StudentTopicSessionResult` and `TagsMastery`.
  - [x] `TagsMastery` unique key is `(student_id, tag_id)` only.
  - [x] WeakTags query returns only rows with `official_point < 5.00`.
  - [x] Lecture/material recommendations prioritize remedial weak topics first.
  - [x] SQL-only recommender works without Redis/SAR configured.
  - [ ] WeakTag API (`GET /weak-tags`) returns within **2 seconds** for a student with 50+ `TagsMastery` rows, SQL Server only, no Redis (G4 — SC SLA).
  - [x] `CompetencyPoint.point` is recalculated and persisted after `TagsMastery` update (RCM-12).

- [ ] **Multi-tag unit tests** (v4.1):
  - [ ] Single-tag answer: `ΔP = Δ_total × 1.0` (suy biến, same as MVP).
  - [ ] 1 primary (w=0.65) + 1 secondary (w=0.35), correct Mức 3: `Δ_total = +0.075` → `ΔP_main = 0.049`, `ΔP_sub = 0.026`.
  - [ ] 1 primary (w=0.65) + 2 secondary (w=0.175 each), wrong Mức 1 t<5s: `Δ_total = -0.3375` → `ΔP_main = -0.219`, `ΔP_sub = -0.059 each`.
  - [ ] series_answer_count reaches 10 independently per tag → blend + reset.
  - [ ] Exam TopicScore: 3 câu liên tag INT: `c_{q,INT}` = `[8.0, 3.9, 1.75]`, `T_j = 13.65/3 = 4.55`.
  - [ ] BR-19: sub-tag `official_point = 3.9` → `IsBottleneckWeak = true`; `4.1` → false.
