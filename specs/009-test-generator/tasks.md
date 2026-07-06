# Tasks Checklist: Test Generator Module

**Branch**: `009-test-generator` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] Coordinate current DB script table mappings with Testing module (003) — same DbContext or separate registration.
- [ ] Create EF `IEntityTypeConfiguration` for 3 entities:
  - [ ] `BlueprintConfiguration` — `status` enum; `expert_id` FK; `reviewed_by` FK (nullable)
  - [ ] `BlueprintSectionConfiguration` — FK to `Blueprint`, UNIQUE `(blueprint_id, section_order)`, `question_type` constraint, composite section metadata constraint
  - [ ] `BlueprintDetailConfiguration` — FK to `BlueprintSection`, composite UNIQUE `(blueprint_section_id, tag_id, difficulty_id)`; `quantity >= 1` CHECK
- [ ] Create `TestGenDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [ ] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 2 blueprints (1 APPROVED, 1 DRAFT), each with at least one section and detail slots per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **CreateBlueprintCommand** (UC-43):
  - [ ] Create `Blueprint` with `status = DRAFT`, `expert_id = currentUserId`
  - [ ] Create at least one `BlueprintSection`; for legacy/pre-2025 tests create one default `SingleChoice` section
  - [ ] Validate: each `BlueprintDetail.quantity >= 1`
  - [ ] Validate: each `BlueprintDetail` belongs to exactly one `BlueprintSection`
  - [ ] Save `BlueprintSection` and `BlueprintDetail` rows in same transaction

- [ ] **SubmitBlueprintForReviewCommand**:
  - [ ] Validate `status = DRAFT` or `REJECTED` → 422 otherwise
  - [ ] Validate BR-07: `SUM(BlueprintSection.total_questions) == Blueprint.total_questions` → 422 if mismatch
  - [ ] Validate BR-07 per section: `SUM(BlueprintDetail.quantity) == BlueprintSection.total_questions` → 422 if mismatch
  - [ ] Validate at least 1 `BlueprintSection` and at least 1 `BlueprintDetail` row exists
  - [ ] Transition to `PENDING_REVIEW`

- [ ] **ReviewBlueprintCommand** (UC-41):
  - [ ] Validate `status = PENDING_REVIEW` → 422 otherwise (BR-Blueprint-02)
  - [ ] Validate `blueprint.expert_id != currentUserId` → 403 (BR-Blueprint-01)
  - [ ] On **Approve**: `status = APPROVED`, set `reviewed_by`, `reviewed_time`
  - [ ] On **Reject**: `status = REJECTED`, set `review_note` (non-empty, BR-Blueprint-03), `reviewed_by`, `reviewed_time`
  - [ ] Publish `BlueprintApprovedEvent` or `BlueprintRejectedEvent`

- [ ] **UpdateBlueprintCommand** (UC-45):
  - [ ] Validate `status = DRAFT` or `REJECTED` → 422 otherwise (BR-47)
  - [ ] Validate ownership: `expert_id = currentUserId`
  - [ ] Update fields; re-validate BR-07 if `BlueprintSection` or `BlueprintDetail` changed

- [ ] **CloneBlueprintCommand** (UC-44):
  - [ ] Deep-copy: new `blueprint_id` (UUID), new `blueprint_name` (append " (Copy)")
  - [ ] Set `status = DRAFT`; `expert_id = currentUserId`
  - [ ] Copy all `BlueprintSection` rows with new `blueprint_section_id`s
  - [ ] Copy all `BlueprintDetail` rows with new `blueprint_detail_id`s and remapped `blueprint_section_id`s (BR-09)
  - [ ] Return new clone entity

- [ ] **DeleteBlueprintCommand** (UC-46):
  - [ ] If `status = DRAFT` or `REJECTED` AND no linked `Test` records → hard-delete
  - [ ] If `status = ACTIVE` or linked Tests exist → HTTP 409 (cannot delete active blueprint)
  - [ ] `APPROVED` with no linked tests → allow hard-delete (admin decision; expert must confirm)

- [ ] **GenerateTestCommand** (internal — called by Student via `POST /api/v1/tests/generate` for Exam Mode):
  - [ ] Validate `blueprint.status = APPROVED` or `ACTIVE`.
  - [ ] Call `IRecommenderService.GetStudentWeakTagsAsync(studentId)` for adaptive bias.
  - [ ] Implement `GenerationEngine.GenerateTestFromBlueprintAsync()` per plan:
    - WeakTag cap: `weakTagQuestions <= 0.20 * total_questions`.
    - Bias probability 40% for WeakTag slots (reduced from 70%).
    - Difficulty downscale (F2 resolution): Hard/Very Hard → Medium if WeakTag; Medium → Easy if `official_point < 3.00` (Level 1).
    - Remedial (F6 resolution): Easy + `official_point < 5.0` → Remedial (10% bias, no downscale).
    - Rollback downscale (F3 resolution): Scale back to slot difficulty when `official_point >= 5.00`.
    - Dedup: exclude `question_id`s from student's last 7 days of sessions.
    - Query per section and filter `Question.QuestionType = BlueprintSection.QuestionType`.
    - Random sample from `Question` matching topic, difficulty, section question type, status, and active flag.
  - [ ] Create `Test`; generate unique `test_code` only for shareable/code-entry tests, keep `NULL` for personal adaptive/recommendation tests.
  - [ ] Set `Test.generated_by = System`; `test_mode = Exam`.
  - [ ] Create `TestQuestion` records ordered by `section_order`, then slot order; set `SourceBlueprintDetailID`.
  - [ ] Transition Blueprint to `ACTIVE` on first generation (BR-48).
  - [ ] Return `{ test_id, test_code, duration_minutes, total_questions }`; `test_code` may be `NULL`.

- [ ] **GeneratePracticeSeriesCommand** (internal — called by Student via `POST /api/v1/tests/generate-practice` for Practice Mode — BR-54):
  - [ ] Accepts `tagId` and `studentId`.
  - [ ] Call `IRecommenderService.GetStudentWeakTagAdviceAsync(studentId)` to get target difficulty level.
  - [ ] Validate: `tagId` is a WeakTag for the student (`official_point < 5.00`).
  - [ ] Query 10 candidate questions from `Question` where:
    - `tag_id = tagId`
    - `difficulty_id` matches `WeakTagAdviceDto.RecommendedDifficultyLevel` (1 or 2)
    - `status = APPROVED` AND `is_active = true`.
  - [ ] Exclude `question_id`s from student's last 7 days of sessions (dedup).
  - [ ] Create `Test` session with `test_mode = Practice`, `total_questions = 10`, `generated_by = System`, `generated_for_student_id = studentId`.
  - [ ] Create `TestQuestion` records.
  - [ ] Return `{ test_id, duration_minutes, total_questions }`.

- [ ] **Queries**:
  - [ ] `GetBlueprintListQuery` (UC-42): paged; filter by `status`, `grade`, `expert_id`; include section count and detail slot count.
  - [ ] `GetPendingBlueprintsQuery` (UC-40): `status = PENDING_REVIEW` AND `expert_id != currentUserId` (BR-Blueprint-01).
  - [ ] `GetBlueprintByIdQuery`: full blueprint + all `BlueprintSection` rows + all `BlueprintDetail` rows.

---

## Phase 3: Controller and Routing

- [ ] `BlueprintsController` — ExpertOnly:
  - [ ] GET list, GET pending, GET by ID, POST create, POST submit, POST review, POST clone, PUT update, DELETE.
- [ ] `TestGenerationController` — StudentOnly:
  - [ ] `POST /api/v1/tests/generate` — calls `GenerateTestCommand`.
  - [ ] `POST /api/v1/tests/generate-practice` — calls `GeneratePracticeSeriesCommand` (BR-54).
- [ ] Do not add an Expert `TestsController` for MVP; expert workflow stops at question bank + blueprint management
- [ ] Self-approval middleware/guard: inject check in `ReviewBlueprintHandler`
- [ ] Register inside `TestGenModuleExtensions.cs`:
  - DbContext, GenerationEngine, IRecommenderService (cross-module DI), MediatR handlers, domain events

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-43: Create blueprint → `status = DRAFT`, sections and detail slots saved
  - [ ] Submit: section total sum = 39, blueprint total_questions = 40 → 422 (BR-07)
  - [ ] Submit: section detail sum = 9, section total_questions = 10 → 422 (BR-07)
  - [ ] Submit: valid sum → `status = PENDING_REVIEW`
  - [ ] UC-41: Expert A approves own blueprint → 403 (BR-Blueprint-01)
  - [ ] UC-41: Expert B approves → `status = APPROVED`
  - [ ] UC-41: Reject without note → 400 (BR-Blueprint-03)
  - [ ] UC-44: Clone APPROVED → new UUID, `status = DRAFT`, all sections and slots copied (BR-09)
  - [ ] UC-45: Update `APPROVED` blueprint → 422 (BR-47)
  - [ ] UC-46: Delete ACTIVE blueprint → 409
  - [ ] Test generation: 40 questions selected; `TestQuestion` records ordered by section; `SourceBlueprintDetailID` populated
  - [ ] Test generation: personal adaptive/recommendation test has `generated_by = System` and `test_code = NULL`
  - [ ] Test generation: Section I `SingleChoice`, Section II `Composite`, Section III `ShortAnswer` each selects matching `Question.QuestionType`
  - [ ] Legacy blueprint: one default `SingleChoice` section can generate old/pre-2025 multiple-choice-only tests
  - [ ] Test generation: Blueprint transitions to `ACTIVE` on first generation (BR-48)
  - [ ] WeakTag bias: ≤ 20% questions from WeakTag topics (WeakTag Cap)
  - [ ] Dedup: Previously answered questions excluded from generation pool
