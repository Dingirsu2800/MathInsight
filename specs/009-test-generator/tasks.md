# Tasks Checklist: Test Generator Module

**Branch**: `009-test-generator` | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

---

## Phase 1: Persistence Setup

- [ ] Coordinate `tst` schema with Testing module (003) — same DbContext or separate registration
- [ ] Create EF `IEntityTypeConfiguration` for 2 entities:
  - [ ] `BlueprintConfiguration` — `status` enum; `expert_id` FK; `reviewed_by` FK (nullable)
  - [ ] `BlueprintDetailConfiguration` — composite UNIQUE `(blueprint_id, tag_id, difficulty_id)`; `quantity >= 1` CHECK
- [ ] Create `TestGenDbContext.cs` with shared connection, `tst` schema default
- [ ] Add EF migration: `dotnet ef migrations add Init_TestGen --project MathInsight.WebAPI`
- [ ] Seed: 2 blueprints (1 APPROVED, 1 DRAFT), with detail slots per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **CreateBlueprintCommand** (UC-43):
  - [ ] Create `Blueprint` with `status = DRAFT`, `expert_id = currentUserId`
  - [ ] Validate: each `BlueprintDetail.quantity >= 1`
  - [ ] Save `BlueprintDetail` rows in same transaction

- [ ] **SubmitBlueprintForReviewCommand**:
  - [ ] Validate `status = DRAFT` or `REJECTED` → 422 otherwise
  - [ ] Validate BR-07: `SUM(quantity) == total_questions` → 422 if mismatch
  - [ ] Validate at least 1 `BlueprintDetail` row exists
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
  - [ ] Update fields; re-validate BR-07 if `BlueprintDetail` changed

- [ ] **CloneBlueprintCommand** (UC-44):
  - [ ] Deep-copy: new `blueprint_id` (UUID), new `blueprint_name` (append " (Copy)")
  - [ ] Set `status = DRAFT`; `expert_id = currentUserId`
  - [ ] Copy all `BlueprintDetail` rows with new `blueprint_detail_id`s (BR-09)
  - [ ] Return new clone entity

- [ ] **DeleteBlueprintCommand** (UC-46):
  - [ ] If `status = DRAFT` or `REJECTED` AND no linked `Test` records → hard-delete
  - [ ] If `status = ACTIVE` or linked Tests exist → HTTP 409 (cannot delete active blueprint)
  - [ ] `APPROVED` with no linked tests → allow hard-delete (admin decision; expert must confirm)

- [ ] **GenerateTestCommand** (internal — called by Student via `/api/v1/tests/generate`):
  - [ ] Validate `blueprint.status = APPROVED` or `ACTIVE`
  - [ ] Call `IRecommenderService.GetStudentWeakTagsAsync(studentId)` for adaptive bias
  - [ ] Implement `GenerationEngine.GenerateAsync()` per plan:
    - WeakTag cap: `weakTagQuestions <= 0.20 * total_questions`
    - Bias probability 40% for WeakTag slots (reduced from 70%)
    - Difficulty downscale: Hard → Medium if WeakTag; Easy + P_tag < 5.0 → Remedial (10% bias)
    - Dedup: exclude `question_id`s from student's last 7 days of sessions
    - Random sample from `qnb.questions` matching criteria
  - [ ] Create `Test` with unique `test_code` (e.g., 8-char nanoid)
  - [ ] Create `TestQuestion` records ordered by `question_order`
  - [ ] Transition Blueprint to `ACTIVE` on first generation (BR-48)
  - [ ] Return `{ test_id, test_code, duration_minutes, total_questions }`

- [ ] **Queries**:
  - [ ] `GetBlueprintListQuery` (UC-42): paged; filter by `status`, `grade`, `expert_id`; include detail slot count
  - [ ] `GetPendingBlueprintsQuery` (UC-40): `status = PENDING_REVIEW` AND `expert_id != currentUserId` (BR-Blueprint-01)
  - [ ] `GetBlueprintByIdQuery`: full blueprint + all `BlueprintDetail` rows

---

## Phase 3: Controller and Routing

- [ ] `BlueprintsController` — ExpertOnly:
  - [ ] GET list, GET pending, GET by ID, POST create, POST submit, POST review, POST clone, PUT update, DELETE
- [ ] `TestGenerationController` — StudentOnly:
  - [ ] `POST /api/v1/tests/generate` — calls `GenerateTestCommand`
- [ ] Self-approval middleware/guard: inject check in `ReviewBlueprintHandler`
- [ ] Register inside `TestGenModuleExtensions.cs`:
  - DbContext, GenerationEngine, IRecommenderService (cross-module DI), MediatR handlers, domain events

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-43: Create blueprint → `status = DRAFT`, detail slots saved
  - [ ] Submit: slot sum = 39, total_questions = 40 → 422 (BR-07)
  - [ ] Submit: valid sum → `status = PENDING_REVIEW`
  - [ ] UC-41: Expert A approves own blueprint → 403 (BR-Blueprint-01)
  - [ ] UC-41: Expert B approves → `status = APPROVED`
  - [ ] UC-41: Reject without note → 400 (BR-Blueprint-03)
  - [ ] UC-44: Clone APPROVED → new UUID, `status = DRAFT`, all slots copied (BR-09)
  - [ ] UC-45: Update `APPROVED` blueprint → 422 (BR-47)
  - [ ] UC-46: Delete ACTIVE blueprint → 409
  - [ ] Test generation: 40 questions selected; `TestQuestion` records ordered
  - [ ] Test generation: Blueprint transitions to `ACTIVE` on first generation (BR-48)
  - [ ] WeakTag bias: ≤ 20% questions from WeakTag topics (WeakTag Cap)
  - [ ] Dedup: Previously answered questions excluded from generation pool
