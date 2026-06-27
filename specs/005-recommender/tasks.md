**Branch**: `005-recommender` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

> **Updated**: 2026-06-27 — Formula & SAR input corrected; Redis ordering fixed; LEARNING zone added

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for 2 entities mapped to current DB script tables:
  - [ ] `CompetencyPointConfiguration` — UNIQUE `(student_id, grade)`; CHECK constraint `point` in [0.00, 10.00]
  - [ ] `TagsMasteryConfiguration` — UNIQUE `(student_id, tag_id, difficulty_id)`; `mastery_status` enum constraint; `accuracy_rate` in [0.00, 100.00]
- [ ] Create `RecommenderDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [ ] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 5 TagsMastery records for `student_01` (mix of statuses)

---

## Phase 2: Core Domain Logic

- [ ] **CompetencyEngine**:
  - [ ] `UpdateTagsMastery(studentId, tagId, difficultyId, isCorrect)`:
    - Upsert `TagsMastery` record (find or create)
    - Increment `number_done`; if `isCorrect` → increment `num_correct`
    - Recalculate `accuracy_rate = (num_correct / number_done) * 100`
    - Update `mastery_status` (BR-25):
      - If `number_done >= 1` → at least `LEARNING`
      - If `accuracy_rate >= 70` AND `number_done >= 5` → `MASTERED`
    - Update `last_practiced_time = NOW`
  - [ ] `UpdateCompetencyPoint(studentId, grade)`:
    - Calculate average `accuracy_rate` across all tags for that grade **where `number_done >= 1`** (exclude NOT_LEARNED tags with 0 attempts)
    - Normalize to 0.00–10.00 range (DC-04)
    - Upsert `CompetencyPoint` record

- [ ] **WeakTag Diagnosis + Difficulty Mapping** (BR-29 — Dynamic Test Generator Loop):
  - [ ] `DiagnoseWeakTags(studentId)` — runs after each `CompetencyEngine.UpdateTagsMastery()` call:
    - For each (tag, difficulty) combo: compute `P_tag` via **exponential decay formula** (β=0.8, k≤5 sessions):
      ```
      P_tag = Σ(β^(j-1) * T_j) / Σ(β^(j-1))  [j=1..k, T_j in 0–10, j=1 = most recent]
      Cold-start (k=1): P_tag = T_1
      ```
    - Query all (tag, difficulty) combos with `P_tag < 5.0` (WeakTag threshold)
    - For each WeakTag, call `DifficultyMappingService.MapRecommendedDifficulty(currentDifficultyLevel)` (BR-29):
      - `Hard` → return `Medium` as `recommendedPracticeDifficulty`
      - `Medium` → return `Easy` as `recommendedPracticeDifficulty`
      - `Easy` → set `isRemedial = true`; `recommendedPracticeDifficulty = Easy` (no further downscale)
    - For each `TagsMastery` with `P_tag >= 8.0` AND `mastery_status = MASTERED`, call `DifficultyMappingService.MapUpscale(currentDifficultyLevel)` (BR-27):
      - `Easy` → `suggestUpscaleTo = Medium`, `challengeMode = true`
      - `Medium` → `suggestUpscaleTo = Hard`, `challengeMode = true`
      - `Hard` → `suggestUpscaleTo = null`, `challengeMode = true` (already at max)
    - Assemble `WeakTagAdviceDto` per tag
    - Assemble `WeakTagDto` per tag

- [ ] **GradeCalculatedConsumer** (MediatR `INotificationHandler<GradeCalculatedEvent>`):
  - [ ] For each tag-difficulty in `GradeCalculatedEvent.TagResults`:
    - Call `CompetencyEngine.UpdateTagsMastery()`
  - [ ] Call `CompetencyEngine.UpdateCompetencyPoint()` for student's grade
  - [ ] **INVALIDATE Redis cache keys FIRST** (before writing new results):
    - `DEL rcm:weak-tags:{student_id}`
    - `DEL rcm:weak-tag-advice:{student_id}`
  - [ ] Call `DiagnoseWeakTags()` (computes P_tag, applies difficulty mapping, writes both Redis keys)
  - [ ] Publish `CompetencyUpdatedEvent`

- [ ] **DifficultyMappingService** (`IDifficultyMappingService`) — NEW (BR-29):
  - [ ] `MapRecommendedDifficulty(currentDifficultyLevel)` — returns `recommendedPracticeDifficultyId`:
    - Hard → Medium; Medium → Easy; Easy → Easy (Remedial, no further change)
    - Look up difficulty IDs from `TagDifficulty` ordered by `LevelValue`
  - [ ] `MapUpscale(currentDifficultyLevel)` — returns `suggestUpscaleTo` (nullable):
    - Easy → Medium; Medium → Hard; Hard → null
    - Always sets `challengeMode = true`
  - [ ] Unit-test all 6 mapping combinations

- [ ] **RecommenderService** (`IRecommenderService`) — EXPANDED:
  - [ ] `GetStudentWeakTagsAsync(studentId)` — read `WeakTagDto` list from `rcm:weak-tags:{student_id}`; fallback to DB call of `DiagnoseWeakTags()`
  - [ ] `GetStudentWeakTagAdviceAsync(studentId)` — **NEW (BR-29)**:
    - Read `WeakTagAdviceDto` list from `rcm:weak-tag-advice:{student_id}`; fallback to `DiagnoseWeakTags()`
    - Return full `WeakTagAdviceDto` list with all difficulty mapping fields populated
    - **Used exclusively by TestGen module (009)** — not exposed via REST API

- [ ] **Recommendation Queries** — UPDATED:
  - [ ] `GetWeakTagsQuery` — UC-52: call `GetStudentWeakTagsAsync()` (simplified DTO for frontend)
  - [ ] `GetRecommendedLecturesQuery` — UC-53:
    - Cross-read `Lecture` WHERE `TagID IN (weakTagIds + learningTagIds)` AND `Status = 'Published'`
    - **Three-tier priority sort** (BR-30):
      1. `priority = REMEDIAL`: lectures for tags with `isRemedial = true` (sorted first)
      2. `priority = WEAK`: lectures for non-remedial WeakTags (`P_tag < 5.0`, not Remedial)
      3. `priority = NORMAL`: lectures for LEARNING-zone tags (`5.0 ≤ P_tag < 8.0`, `number_done >= 1`) — still shown to reinforce progress, sorted last
  - [ ] `GetRecommendedMaterialsQuery` — UC-54:
    - Cross-read `Material` via `LectureMaterial` junction
    - Apply same three-tier `REMEDIAL → WEAK → NORMAL` priority sort

- [ ] **SAR Model Integration** (optional for MVP, fallback to rule-based):
  - [ ] `SarModelRunner.TrainAsync()` — Hangfire weekly job; exports event log (StudentID, TagID, EventTimestamp, EventWeight) and calls Python script
  - [ ] Configure Hangfire recurring job: `0 2 * * 0` (weekly, 2AM Sunday)
  - [ ] After training, update Redis key `sar:model:trained_at = NOW`
  - [ ] Fallback trigger: if `model.pkl` missing OR `sar:model:trained_at` older than 8 days → use rule-based `DiagnoseWeakTags()` instead

---

## Phase 3: Controller and Routing

- [ ] `RecommenderController` — StudentOnly:
  - [ ] `GET /api/v1/recommender/weak-tags`
  - [ ] `GET /api/v1/recommender/lectures`
  - [ ] `GET /api/v1/recommender/materials`
- [ ] Register inside `RecommenderModuleExtensions.cs`:
  - DbContext, CompetencyEngine, RecommenderService, MediatR handlers, Redis, Hangfire job

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Unit tests for `DifficultyMappingService`:
  - [ ] `MapRecommendedDifficulty(Hard)` → returns `Medium` difficulty ID
  - [ ] `MapRecommendedDifficulty(Medium)` → returns `Easy` difficulty ID
  - [ ] `MapRecommendedDifficulty(Easy)` → returns `Easy` (Remedial; no further downscale)
  - [ ] `MapUpscale(Easy)` → `suggestUpscaleTo = Medium`, `challengeMode = true`
  - [ ] `MapUpscale(Medium)` → `suggestUpscaleTo = Hard`, `challengeMode = true`
  - [ ] `MapUpscale(Hard)` → `suggestUpscaleTo = null`, `challengeMode = true`
- [ ] Integration tests (xUnit):
  - [ ] UC-52: Student with 3 WeakTags → correct list returned
  - [ ] UC-52: Redis cache hit → response < 100ms
  - [ ] UC-52: Both cache keys (`weak-tags` + `weak-tag-advice`) invalidated **before** `DiagnoseWeakTags()` writes on new `GradeCalculatedEvent`
  - [ ] `GetStudentWeakTagAdviceAsync()` → correct `WeakTagAdviceDto` with `recommendedPracticeDifficultyId`, `isRemedial`, `suggestUpscaleToId`
  - [ ] P_tag formula: 5 sessions `[10,10,5,0,0]` with β=0.8 → P_tag ≈ 7.26 (weighted, not simple average)
  - [ ] P_tag formula: Cold-start k=1, T=8.0 → P_tag = 8.0
  - [ ] P_tag < 5.0 at Hard → `recommendedPracticeDifficulty = Medium`
  - [ ] P_tag < 5.0 at Medium → `recommendedPracticeDifficulty = Easy`
  - [ ] P_tag < 5.0 at Easy → `isRemedial = true`, `recommendedPracticeDifficulty = Easy`
  - [ ] P_tag >= 8.0 at Medium + MASTERED → `suggestUpscaleTo = Hard`, `challengeMode = true`
  - [ ] P_tag >= 8.0 at Hard + MASTERED → `suggestUpscaleTo = null`, `challengeMode = true`
  - [ ] UC-53: Remedial tag lectures returned first (`priority: REMEDIAL`) (BR-30)
  - [ ] UC-53: Non-remedial WeakTag lectures returned second (`priority: WEAK`)
  - [ ] UC-53: LEARNING-zone tag lectures returned last (`priority: NORMAL`)
  - [ ] UC-54: Materials follow same three-tier sort (REMEDIAL → WEAK → NORMAL)
  - [ ] DC-04: `accuracy_rate` > 100 → error logged, clamped to 10.0
  - [ ] BR-25: After 5 correct answers → `MASTERED`
  - [ ] BR-25: 70% correct, 4 attempts → still `LEARNING` (need ≥ 5)
  - [ ] CompetencyPoint: tags with `number_done = 0` (NOT_LEARNED) excluded from average
  - [ ] SAR fallback: if `sar:model:trained_at` > 8 days old → rule-based `DiagnoseWeakTags()` used
