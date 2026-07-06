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
- [ ] Seed sample mastery rows at grain `(student_id, tag_id)`.

---

## Phase 2: Core Domain Logic

- [ ] **TopicResultIngestionHandler**:
  - [ ] Receive graded per-topic results from Grading module.
  - [ ] Insert `StudentTopicSessionResult` per `(session_id, tag_id)`.
  - [ ] Skip duplicate `(session_id, tag_id)` rows to keep update idempotent.

- [ ] **CompetencyEngine**:
  - [ ] `UpdateTagsMasteryFromSessionResult(studentId, tagId, topicScore, testMode)`:
    - Upsert `TagsMastery(student_id, tag_id)`.
    - Update `number_done`, `num_correct`, `accuracy_rate`.
    - If official/exam result: update `exam_anchor` using Exponential Decay (RCM-05):
      - Prepend `topic_score` to `exam_history` (JSON array), keep at most 5 entries.
      - `exam_anchor = Σ(β^(j-1) × T_j) / Σ(β^(j-1))` with `β = 0.8`, `j=1` = latest.
    - If practice/adaptive result (per-answer): update `practice_point` using Elo formula (RCM-06):
      - Correct: `practice_point = min(10.0, practice_point + 0.05 × w_D × γ_time)`
      - Wrong:   `practice_point = max(0.0,  practice_point − 0.05 × (5 − w_D) × γ_time_penalty)`
      - `w_D ∈ {0.5, 1.0, 1.5, 2.0}` for difficulty levels 1–4.
      - `γ_time_penalty = 1.5` when answer time `t < 5 seconds`; otherwise `γ_time = 1.0`.
      - Increment `series_answer_count`.
    - Recalculate `official_point = 0.7 * exam_anchor + 0.3 * practice_point`.
    - Recalculate `recommended_difficulty_level`.
    - Update `mastery_status`: `NotLearned`, `Learning`, `Mastered`.
    - Update `last_calculated_at`.
  - [ ] Clamp all points to `0.00..10.00`.

- [ ] **Practice Series Logic**:
  - [ ] When `series_answer_count >= 10`, blend:
    - `official_point = 0.7 * exam_anchor + 0.3 * practice_point`
    - `practice_point = official_point`
    - `series_answer_count = 0`

- [ ] **DifficultyMappingService**:
  - [ ] `MapFromOfficialPoint(officialPoint)` returns level `1..4`.
  - [ ] `IsWeak(officialPoint)` returns true when `< 5.00`.
  - [ ] `IsRemedial(recommendedDifficultyLevel)` returns true when level `1` and weak.

- [ ] **RecommenderService**:
  - [ ] `GetStudentWeakTagsAsync(studentId)` reads `TagsMastery` where `official_point < 5.00`.
  - [ ] `GetStudentWeakTagAdviceAsync(studentId)` returns `WeakTagAdviceDto` with `official_point`, `recommended_difficulty_level`, `is_remedial`, and reason.
  - [ ] Keep SQL-only implementation for MVP; Redis cache is optional later.

- [ ] **Recommendation Queries**:
  - [ ] `GetWeakTagsQuery` - UC-52.
  - [ ] `GetRecommendedLecturesQuery` - UC-53: match `Lecture.TagID` to weak `TagID`; remedial topics sorted first.
  - [ ] `GetRecommendedMaterialsQuery` - UC-54: match materials through `LectureMaterial`; remedial topics sorted first.

---

## Phase 3: Controller and Routing

- [ ] `RecommenderController` - StudentOnly:
  - [ ] `GET /api/v1/recommender/weak-tags`
  - [ ] `GET /api/v1/recommender/lectures`
  - [ ] `GET /api/v1/recommender/materials`
- [ ] Register inside `RecommenderModuleExtensions.cs`:
  - DbContext, CompetencyEngine, DifficultyMappingService, RecommenderService, MediatR handlers.
- [ ] Do not require Redis, Python, SAR, Hangfire, or separate recommender service for MVP.

---

## Phase 4: Verification

- [ ] `dotnet build` - zero compile errors.
- [ ] Unit tests:
  - [ ] `official_point = 0.7 * exam_anchor + 0.3 * practice_point`.
  - [ ] `official_point < 5.00` returns WeakTag.
  - [ ] Difficulty mapping: `2.99 -> 1`, `3.00 -> 2`, `4.99 -> 2`, `5.00 -> 3`, `7.49 -> 3`, `7.50 -> 4`.
  - [ ] Practice series count `10` resets `practice_point` and `series_answer_count`.
  - [ ] **exam_anchor — Exponential Decay**:
    - 1 result: `exam_anchor = T1`.
    - 2 results: `exam_anchor = (T1 + 0.8×T2) / (1 + 0.8)`.
    - 5 results: weights `1.0, 0.8, 0.64, 0.512, 0.410`; older result beyond k=5 is ignored.
    - `exam_history` is capped at 5 entries after each update.
  - [ ] **practice_point — Elo formula**:
    - Correct, level 2 (`w_D=1.0`), normal time: `Δ = +0.05`.
    - Wrong, level 1 (`w_D=0.5`), normal time: `Δ = −0.05×(5−0.5) = −0.225`.
    - Wrong, level 4 (`w_D=2.0`), normal time: `Δ = −0.05×(5−2.0) = −0.150`.
    - Wrong, level 1 (`w_D=0.5`), `t < 5s` (guessing): `Δ = −0.05×4.5×1.5 = −0.3375`.
    - Result never exceeds `10.0` or drops below `0.0`.
- [ ] Integration tests:
  - [ ] Duplicate `(session_id, tag_id)` result does not double-update `TagsMastery`.
  - [ ] Graded session updates `StudentTopicSessionResult` and `TagsMastery`.
  - [ ] `TagsMastery` unique key is `(student_id, tag_id)` only.
  - [ ] WeakTags query returns only rows with `official_point < 5.00`.
  - [ ] Lecture/material recommendations prioritize remedial weak topics first.
  - [ ] SQL-only recommender works without Redis/SAR configured.
