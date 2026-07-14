# Tasks Checklist: Test Generator Module

**Branch**: `testgen-blueprint` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Current delivery slice**: Expert Blueprint MVP

## Phase 0: Specification Alignment

- [x] Confirm `Database/database/001_Create_MathInsight_Azure.sql` as MVP source of truth.
- [x] Separate Expert Blueprint MVP from later Student test generation.
- [x] Align documented field names, statuses, TestMode values, routes, and delete behavior with current SQL.
- [x] Define stable Blueprint errors and authorization rules.

## Phase 1: Persistence Foundation

- [x] Replace TestGen entity IDs from `Guid` to `string` for SQL `VARCHAR(36)` columns.
- [x] Correct `Blueprint` mapping:
  - [x] Exact PascalCase column names.
  - [x] Status values `Draft`, `PendingReview`, `Approved`, `Rejected`, `Active`, `Deactivated`.
  - [x] Map review audit to `ApprovedBy`, `ReviewNote`, `ReviewTime`.
  - [x] Remove non-existent `CreatedTime` mapping.
- [x] Correct `BlueprintSection` mapping:
  - [x] Required `SectionName`, required `DefaultPointPerQuestion`, and SQL decimal precision.
  - [x] Unique `(BlueprintID, SectionOrder)` and composite-section metadata constraints.
- [x] Correct `BlueprintDetail` mapping:
  - [x] Exact columns and string IDs.
  - [x] Composite FK `(BlueprintSectionID, BlueprintID)` to BlueprintSection.
  - [x] Unique `(BlueprintSectionID, TagID, DifficultyID)`.
- [x] Correct `Test` mapping to `TestMode`, SQL status casing, lengths, checks, and indexes.
- [x] Complete `TestQuestion` mapping, including source-detail and recommendation audit fields.
- [x] Add read models for `Expert`, `TagTopic`, and `TagDifficulty` with `ExcludeFromMigrations()`.
- [x] Register TestGen DbContext and MediatR in `TestGenModuleExtensions`.
- [x] Create `tests/MathInsight.Modules.TestGen.Tests` and add it to `MathInsight.sln`.
- [x] Add EF model metadata tests for all five owned tables and external read models.
- [x] Do not create EF migrations.

## Phase 2: Create and Read Blueprint

- [x] Add Blueprint aggregate request/response contracts.
- [x] Add `BlueprintAggregateValidator`:
  - [x] Validate names, grade, duration, total questions, section order, and duplicate section order.
  - [x] Validate allowed DB question types and Composite metadata.
  - [x] Require at least one section and one detail per section.
  - [x] Require detail quantity >= 1 and no duplicate tag/difficulty slot per section.
  - [x] Validate active topic/difficulty in bulk and topic grade matches blueprint grade.
- [x] Implement `CreateBlueprintCommand`:
  - [x] Use authenticated Expert as owner and ignore ownership fields from payload.
  - [x] Generate string UUIDs and set `Status = Draft`.
  - [x] Save Blueprint, Sections, and Details atomically.
- [x] Implement `GetBlueprintListQuery`:
  - [x] Paged filters for status, grade, owner, and search.
  - [x] Exclude Deactivated by default; stable name/ID ordering.
  - [x] Return section/detail counts.
- [x] Implement `GetPendingBlueprintsQuery` for `PendingReview` excluding current Expert ownership.
- [x] Implement `GetBlueprintDetailQuery` with ordered sections and details.
- [x] Add Expert-only GET/POST routes to `BlueprintsController`.

## Phase 3: Update and Submit

- [ ] Implement `UpdateBlueprintCommand`:
  - [ ] Owner-only and status limited to `Draft` or `Rejected`.
  - [ ] Validate full replacement before mutation.
  - [ ] Replace child sections/details in one transaction.
- [ ] Implement `SubmitBlueprintForReviewCommand`:
  - [ ] Owner-only and status limited to `Draft` or `Rejected`.
  - [ ] Require positive duration and totals.
  - [ ] Validate sum of sections equals Blueprint total.
  - [ ] Validate sum of details equals each Section total.
  - [ ] Revalidate active taxonomy.
  - [ ] Set `PendingReview` and clear old review audit fields.
- [ ] Map workflow errors to stable 403/404/422 responses.

## Phase 4: Peer Review

- [ ] Implement `ReviewBlueprintCommand`:
  - [ ] Require `PendingReview`.
  - [ ] Reject self-review with 403.
  - [ ] Approve -> `Approved`, clear note, set review actor/time.
  - [ ] Reject -> `Rejected`, require trimmed note of 1-2000 chars, set actor/time.
- [ ] Implement POST `/{blueprintId}/review`.
- [ ] Do not publish notification events in the Blueprint MVP.

## Phase 5: Clone and Delete

- [ ] Implement `CloneBlueprintCommand`:
  - [ ] Clone any visible non-deactivated aggregate.
  - [ ] Generate all new IDs, assign current Expert, set `Draft`, clear audit.
  - [ ] Append ` (Copy)` while respecting the 100-character name limit.
- [ ] Implement `DeleteBlueprintCommand`:
  - [ ] Owner-only.
  - [ ] Hard-delete unused `Draft`, `Rejected`, or `Approved` aggregate.
  - [ ] Return 409 for `PendingReview`.
  - [ ] Change `Active` or Test-linked aggregate to `Deactivated`.
- [ ] Implement clone and delete controller routes with 403/404/409 mapping.

## Phase 6: Expert Frontend

- [ ] Blueprint list with All/Mine/Pending views and status, grade, and search filters.
- [ ] Full-page Blueprint editor for sections and detail slots.
- [ ] Load active topic/difficulty catalogs from existing QuestionBank APIs.
- [ ] Blueprint detail view with state/ownership-aware actions.
- [ ] Confirm destructive delete/deactivate actions.
- [ ] Map backend error codes to Vietnamese; never show backend English messages directly.
- [ ] Run `npm run build` and desktop workflow smoke tests.

## Phase 7: Backend Verification

- [ ] Create aggregate -> `Draft`, owner from claims, all child rows persisted.
- [ ] Invalid/inactive/wrong-grade taxonomy -> 400 and no writes.
- [ ] Duplicate section order or detail slot -> 422 and no writes.
- [ ] Update non-owner -> 403; update Approved -> 422.
- [ ] Submit section sum mismatch -> 422.
- [ ] Submit detail sum mismatch -> 422.
- [ ] Valid submit -> `PendingReview` and audit reset.
- [ ] Self-review -> 403; non-owner approve -> `Approved`.
- [ ] Reject without note/over 2000 chars -> 400.
- [ ] Clone deep-copies all children with independent IDs.
- [ ] Delete unused Draft -> hard delete.
- [ ] Delete PendingReview -> 409.
- [ ] Delete Active/Test-linked -> `Deactivated` and history retained.
- [ ] Controller tests cover 400/403/404/409/422.
- [ ] `dotnet test` and `dotnet build MathInsight.sln --no-restore` pass.
- [ ] Manual SQL Server smoke: composite FK and concurrent submit/review transitions.

## Phase 8: Test Generation Backlog

- [ ] Add Question, QuestionTopic, TestSession, and taxonomy read models required by generation.
- [ ] Implement `GenerateBlueprintExamCommand` using SQL `TestMode = BlueprintExam`.
- [ ] Implement adaptive/topic practice generation with Recommender advice.
- [ ] Resolve `RecommendedDifficultyLevel` through `TagDifficulty.LevelValue`.
- [ ] Filter candidates by topic, difficulty, section QuestionType, Approved status, and active flag.
- [ ] Deduplicate recent questions and enforce WeakTag rules after their final product review.
- [ ] Create TestQuestion ordering, source-detail, and recommendation audit fields.
- [ ] Transition first-used Approved Blueprint to `Active`.
- [ ] Add Student generation endpoints only in this later phase.
