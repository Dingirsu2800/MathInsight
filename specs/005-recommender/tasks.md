**Branch**: `005-recommender` | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

> **Updated**: 2026-06-26 — Added Dynamic Test Generator Loop tasks (BR-29)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for 2 entities under `rcm` schema:
  - [ ] `CompetencyPointConfiguration` — UNIQUE `(student_id, grade)`; CHECK constraint `point` in [0.00, 10.00]
  - [ ] `TagsMasteryConfiguration` — UNIQUE `(student_id, tag_id, difficulty_id)`; `mastery_status` enum constraint; `accuracy_rate` in [0.00, 100.00]
- [ ] Create `RecommenderDbContext.cs` with shared connection, `rcm` schema default
- [ ] Add EF migration: `dotnet ef migrations add Init_Recommender --project MathInsight.WebAPI`
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
    - Calculate average accuracy across all tags for that grade
    - Normalize to 0.00–10.00 range (DC-04)
    - Upsert `CompetencyPoint` record

- [ ] **WeakTag Diagnosis + Difficulty Mapping** (BR-29 — Dynamic Test Generator Loop):
  - [ ] `DiagnoseWeakTags(studentId)` — runs after each `CompetencyEngine.UpdateTagsMastery()` call:
    - Query all `TagsMastery` where `student_id = studentId` AND `accuracy_rate < 50.0` (P_tag < 5.0)
    - For each record, call `DifficultyMappingService.MapRecommendedDifficulty(currentDifficultyLevel)` (BR-29):
      - `Hard` → return `Medium` as `recommendedPracticeDifficulty`
      - `Medium` → return `Easy` as `recommendedPracticeDifficulty`
      - `Easy` → set `isRemedial = true`; `recommendedPracticeDifficulty = Easy` (no further downscale)
    - For each `TagsMastery` with `P_tag >= 8.0` AND `mastery_status = MASTERED`, call `DifficultyMappingService.MapUpscale(currentDifficultyLevel)` (BR-27):
      - `Easy` → `suggestUpscaleTo = Medium`, `challengeMode = true`
      - `Medium` → `suggestUpscaleTo = Hard`, `challengeMode = true`
      - `Hard` → `suggestUpscaleTo = null`, `challengeMode = true` (already at max)
    - Assemble `WeakTagAdviceDto` per tag and write to Redis `rcm:weak-tag-advice:{student_id}` (TTL 1 hour)
    - Also write simplified `WeakTagDto` to Redis `rcm:weak-tags:{student_id}` (TTL 1 hour)

- [ ] **GradeCalculatedConsumer** (MediatR `INotificationHandler<GradeCalculatedEvent>`):
  - [ ] For each tag-difficulty in `GradeCalculatedEvent.TagResults`:
    - Call `CompetencyEngine.UpdateTagsMastery()`
  - [ ] Call `CompetencyEngine.UpdateCompetencyPoint()` for student's grade
  - [ ] Call `DiagnoseWeakTags()` (includes difficulty mapping — writes both Redis keys)
  - [ ] Invalidate **both** Redis cache keys:
    - `DEL rcm:weak-tags:{student_id}`
    - `DEL rcm:weak-tag-advice:{student_id}`
  - [ ] Publish `CompetencyUpdatedEvent`

- [ ] **DifficultyMappingService** (`IDifficultyMappingService`) — NEW (BR-29):
  - [ ] `MapRecommendedDifficulty(currentDifficultyLevel)` — returns `recommendedPracticeDifficultyId`:
    - Hard → Medium; Medium → Easy; Easy → Easy (Remedial, no further change)
    - Look up difficulty IDs from `qnb.tag_difficulties` (ordered by `level_value`)
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
    - Cross-read `lrn.lectures` WHERE `tag_id IN (weakTagIds)` AND `status = PUBLISHED`
    - **Priority sort**: if `isRemedial = true` for a tag → its lectures get `priority = REMEDIAL` and appear first in result list (BR-30)
    - Non-remedial WeakTag lectures appear next; general recommendations last
  - [ ] `GetRecommendedMaterialsQuery` — UC-54:
    - Cross-read `lrn.materials` via `lecture_materials` junction
    - Apply same `REMEDIAL` priority sort for materials matching Remedial tags

- [ ] **SAR Model Integration** (optional for MVP, fallback to rule-based):
  - [ ] `SarModelRunner.TrainAsync()` — Hangfire weekly job; calls Python script
  - [ ] Configure Hangfire recurring job: `0 2 * * 0` (weekly, 2AM Sunday)

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
  - [ ] UC-52: Both cache keys (`weak-tags` + `weak-tag-advice`) invalidated after new `GradeCalculatedEvent`
  - [ ] `GetStudentWeakTagAdviceAsync()` → correct `WeakTagAdviceDto` with `recommendedPracticeDifficultyId`, `isRemedial`, `suggestUpscaleToId`
  - [ ] P_tag < 5.0 at Hard → `recommendedPracticeDifficulty = Medium`
  - [ ] P_tag < 5.0 at Medium → `recommendedPracticeDifficulty = Easy`
  - [ ] P_tag < 5.0 at Easy → `isRemedial = true`, `recommendedPracticeDifficulty = Easy`
  - [ ] P_tag >= 8.0 at Medium + MASTERED → `suggestUpscaleTo = Hard`, `challengeMode = true`
  - [ ] P_tag >= 8.0 at Hard + MASTERED → `suggestUpscaleTo = null`, `challengeMode = true`
  - [ ] UC-53: Remedial tag lectures returned with `priority: REMEDIAL` sorted first (BR-30)
  - [ ] UC-53: Non-remedial lectures after Remedial in response
  - [ ] UC-54: Materials for Remedial tags sorted to top
  - [ ] DC-04: `accuracy_rate` > 100 → error logged, clamped to 10.0
  - [ ] BR-25: After 5 correct answers → `MASTERED`
  - [ ] BR-25: 70% correct, 4 attempts → still `LEARNING` (need ≥ 5)