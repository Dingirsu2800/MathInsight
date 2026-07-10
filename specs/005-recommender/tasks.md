# Tasks Checklist: Recommender Module

**Branch**: `005-recommender` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

> **Updated**: 2026-07-04 - aligned with Rule-Based/Ptag v2 and migration 002

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
    - If practice/adaptive result (TestFormat == "Practice"): update `practice_point` using Elo formula (RCM-06) sequentially and retrospectively for each answer in the provided `answers` list (ordered by `QuestionNo`) (F1 & F4 resolution):
      - Correct: `practice_point = min(10.0, practice_point + 0.05 × w_D × γ_time)`
      - Wrong:   `practice_point = max(0.0,  practice_point − 0.05 × (5 − w_D) × γ_time_penalty)`
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
