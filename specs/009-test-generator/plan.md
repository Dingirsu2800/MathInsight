# Implementation Plan: Test Generator Module

> **Current checkpoint**: implement Blueprint budgets and generated snapshots from [Scoring Contract V2](../scoring-contract-v2.md).

**Branch**: `testgen-test-generation` | **Updated**: 2026-07-16
**Spec**: [spec.md](spec.md)

## Summary

The Expert Blueprint lifecycle and baseline BlueprintExam are complete. Checkpoint 6C adds Student-facing baseline TopicPractice generation without Recommender or Testing-session concerns. The current SQL creation script remains the persistence contract; EF migrations and table renames are not permitted. Approved SQL migration 005 relaxes only the existing Test duration constraint so a TopicPractice Test can use zero for unlimited duration.

## Technical Context

| Property | Decision |
|---|---|
| Runtime | .NET 10, ASP.NET Core modular monolith |
| Application pattern | Thin controller, MediatR commands/queries, `Result<T>` and stable errors |
| Persistence | EF Core + SQL Server, separate `TestGenDbContext` |
| IDs | C# `string`; SQL `VARCHAR(36)` UUID text |
| Database naming | Exact PascalCase SQL columns from `001_Create_MathInsight_Azure.sql` |
| Migrations | Disabled; SQL scripts remain source of truth |
| Tests | New `MathInsight.Modules.TestGen.Tests` xUnit project |
| Frontend | React JavaScript + existing Tailwind/UI components, after backend contracts |

## Resolved Schema Drift

The existing TestGen foundation must be corrected before use:

- Replace `Guid` entity IDs with `string` IDs.
- Replace snake_case column mappings with exact names such as `BlueprintID`, `SectionOrder`, and `TestMode`.
- Persist Blueprint statuses exactly as `Draft`, `PendingReview`, `Approved`, `Rejected`, `Active`, `Deactivated`.
- Map review audit to existing `ApprovedBy`, `ReviewNote`, and `ReviewTime`; remove the non-existent Blueprint `CreatedTime` mapping.
- Align section required/null fields and decimal precision with SQL.
- Map BlueprintDetail through composite FK `(BlueprintSectionID, BlueprintID)`.
- Replace Test `TestFormat` with SQL `TestMode`, and complete TestQuestion recommendation audit mappings before generation work.

## Module Structure

```text
MathInsight.Modules.TestGen/
|-- Commands/
|   |-- CreateBlueprint/
|   |-- UpdateBlueprint/
|   |-- SubmitBlueprintForReview/
|   |-- ReviewBlueprint/
|   |-- CloneBlueprint/
|   `-- DeleteBlueprint/
|-- Queries/
|   |-- GetBlueprintList/
|   |-- GetPendingBlueprints/
|   |-- GetBlueprintDetail/
|   `-- GetBlueprintExamOptions/
|-- Generation/
|   |-- BlueprintExamCandidateProvider.cs
|   `-- CapacityAwareQuestionSelector.cs
|-- Commands/GenerateBlueprintExam/
|-- Contracts/Tests/
|-- Contracts/Blueprints/
|-- Errors/
|-- Persistence/
|   |-- Entities/
|   |-- Configurations/
|   `-- ReadModels/
|-- Validation/BlueprintAggregateValidator.cs
|-- Controllers/BlueprintsController.cs
`-- TestGenModuleExtensions.cs
```

## Delivery Checkpoints

### Checkpoint 0: Persistence Foundation

- Correct all five existing entity/configuration mappings against the SQL script.
- Add `Account`, `Expert`, `TagTopic`, and `TagDifficulty` read models needed for display/validation, configured with `ExcludeFromMigrations()`.
- Keep Question/QuestionTopic read models for the later generation checkpoint.
- Register MediatR and TestGen services in `TestGenModuleExtensions`.
- Create TestGen test project with EF model metadata tests for every owned table.

### Checkpoint 1: Create and Read Blueprint

- Define request/response contracts for the full aggregate.
- Add shared validation for field lengths, section order, question type, composite metadata, detail quantity, active taxonomy, and topic grade.
- Create Blueprint + Sections + Details in one transaction with `Status = Draft` and authenticated owner.
- Implement paged list, pending list, and aggregate detail queries.
- Deactivated records are excluded by default.

### Checkpoint 2: Update and Submit

- Replace the entire owned aggregate only for `Draft` or `Rejected`.
- Use one transaction; delete/recreate child rows after request validation succeeds.
- Submit reloads and validates totals from persisted data.
- Successful submit sets `PendingReview` and clears old review audit fields.

### Checkpoint 3: Peer Review

- Review only `PendingReview` blueprints created by another Expert.
- Approve sets `Approved`, clears note, and writes review actor/time.
- Reject sets `Rejected`, requires a 1-2000 character note, and writes review actor/time.
- No notification event is required for MVP.

### Checkpoint 4: Clone and Delete

- Clone any visible blueprint to a new owned `Draft` with all new aggregate IDs.
- Hard-delete unused `Draft`, `Rejected`, or `Approved` aggregates.
- Reject delete of `PendingReview` with 409.
- Change `Active` or Test-linked blueprints to `Deactivated`, preserving Test history.

### Checkpoint 5: Expert Frontend

- Blueprint list with own/all/pending views and status/grade/search filters.
- Full-page editor for sections and topic/difficulty slots; do not use a large nested modal.
- Detail view with clone, owner edit/submit/delete, and non-owner review actions according to state.
- Frontend maps stable error codes to Vietnamese.

### Checkpoint 6A: Baseline BlueprintExam

- Add TestGen-owned read models for Student, Question, and QuestionTopic, all excluded from migrations.
- Add Student blueprint-option query filtered by current grade and `Approved`/`Active` status.
- Implement exact capacity-aware assignment from Questions to BlueprintDetails.
- Create personal `BlueprintExam` Test and baseline-audit TestQuestion rows atomically.
- Transition first-used `Approved` blueprint to `Active` in the same transaction.
- Add stable generation errors, Student-only controller endpoints, metadata tests, handler tests, and controller tests.

### Checkpoint 6B: Adaptive BlueprintExam

- Reuse the existing batch `IStudentTopicMasteryProvider` contract from `MathInsight.Shared`; TestGen does not reference Recommender directly.
- Resolve each blueprint detail's original active level and a preferred level using qualified mastery: below `5.00` lowers one, `5.00..<7.50` keeps it, and `7.50..10.00` raises one, clamped to levels `1..4`.
- Load candidates for the union of original and preferred difficulties, then use a global minimum-cost capacity assignment: preferred edges cost less than original-difficulty fallback edges, while each Question retains capacity one.
- Preserve exact blueprint structure and scoring. Populate recommendation audit fields only when the selected Question actually uses the preferred adjusted difficulty.
- Keep shared Fixed/Random generation and Testing session creation unchanged. Defer recent-question deduplication until its product window is approved.
- Add a Student UI command `Tạo đề theo năng lực` above the shared catalog. Keep `Kho đề` filters `Đề cố định` and `Đề theo cấu trúc` separate from this create action.

### Checkpoint 6C: TopicPractice

- Return active direct-child topics at or below the Student grade, with exact-topic candidate capacity in a flat response.
- Generate exactly 10 unique questions for one selected active direct-child topic using `TestMode = TopicPractice`, baseline level quota 3/4/2/1, nearest fallback, at most two Composite questions, and unseen-then-oldest preference.
- Keep BlueprintID null, persist `DurationMinutes = 0` and `NormalizedWeight`, then hand the generated Test to Testing for a Practice TestSession.
- Extract immutable candidate validation so BlueprintExam and TopicPractice share the same QuestionVersion V2 gate.

### Checkpoint 6D: WeakTag-Aware TopicPractice

- Keep the completed TopicPractice request and baseline behavior, then consume `IStudentRecommendationProvider` only through `MathInsight.Shared`.
- Resolve qualified WeakTag advice only for the exact selected active direct-child topic, then choose the approved level-1 or level-2 profile.
- Store recommendation audit in existing `TestQuestion` fields; no schema change, Recommender project reference, REST call, Redis, or Adaptive BlueprintExam implementation is permitted.
- Return additive option/generation metadata and stable `503` errors; frontend remains a display client and sends only `tagId`.

### Checkpoint 6E: Student-Selected Topic Practice Difficulty

- Extend TopicPractice request compatibly with nullable `difficultyId`; absent means the existing recommended path.
- For a supplied active level 1-4 difficulty, bypass `ITopicPracticeRecommendationResolver`, query only the selected difficulty, and select exactly ten Questions without fallback or mixing.
- Add per-topic difficulty availability in the options response using the already batched candidate pools; no per-topic or per-difficulty catalog queries.
- Persist manual audit with `TopicPractice-Manual-v1` and return additive selection metadata. No schema, migration, SQL, or frontend change is part of this checkpoint.

### Phase 9: Expert Shared BlueprintExam

- Preserve the completed Student personal BlueprintExam API and characterization tests.
- Add owner-only shared generation from Approved or Active Blueprints at `POST /api/test-generator/blueprints/{blueprintId}/tests`.
- Generate TestID server-side once outside the SQL execution strategy. Reuse TestID and CreatedTime through transient retry and ambiguous-commit verification.
- Generate a cryptographically random eight-character TestCode. A 2601/2627 collision on the TestCode unique index rolls back, clears tracking, creates a new code, and reruns the entire transaction up to five times.
- Persist shared Tests with null GeneratedForStudentID and GeneratedBy System. Transition Approved to Active atomically on first successful use.
- Add owner-only immutable Expert preview and owner-only Active-to-Archived status transition. Existing sessions may finish after archive.
- Add owner-only paged generated-Test listing per Blueprint so Active and Archived variants remain reachable after reload.
- Add paged Student shared-Test discovery and generic, rate-limited TestCode resolution filtered by exact Student grade and active Blueprint state.
- Keep authorization and transaction orchestration separate for Student and Expert flows. Extract only pure requirement, selection, ordering, score-allocation, and TestQuestion-construction logic after characterization coverage exists.
- Do not add durable HTTP idempotency, a Draft Test status, Adaptive generation, Diagnostic, or Recommender integration.

### Phase 10: Expert Fixed BlueprintExam

- Add owner-only candidate search per BlueprintDetail and exact-question generation from Approved or Active Blueprints.
- Enforce unique questions, continuous global order, exact detail quantities, and full topic/difficulty/type/scoring/part-count eligibility on the server.
- Reuse immutable QuestionVersion V2 validation and section score allocation; persist `GeneratedBy = Expert` and `SelectionReason = FixedExam`.
- Derive additive `generationType` metadata for Expert list and preview. Random and fixed tests coexist under one Blueprint and archive independently.
- Add only the SelectionReason check-constraint migration; no new table, column, TestSession, frontend-owned validation, or Adaptive behavior.

## API Design

`BlueprintsController` uses `[Authorize(Roles = "Expert")]` and route `api/test-generator/blueprints`.

```text
GET    /api/test-generator/blueprints
GET    /api/test-generator/blueprints/pending
GET    /api/test-generator/blueprints/{blueprintId}
POST   /api/test-generator/blueprints
PUT    /api/test-generator/blueprints/{blueprintId}
POST   /api/test-generator/blueprints/{blueprintId}/submit
POST   /api/test-generator/blueprints/{blueprintId}/review
POST   /api/test-generator/blueprints/{blueprintId}/clone
DELETE /api/test-generator/blueprints/{blueprintId}
```

Controllers obtain the Expert ID from `account_id`, falling back to `ClaimTypes.NameIdentifier`, consistent with QuestionBank. Controllers only map HTTP outcomes; workflow logic stays in handlers.

Checkpoint 6A adds a separate Student-only controller because blueprint visibility and generation rules materially differ from Expert management:

```text
GET  /api/test-generator/tests/blueprint-options
POST /api/test-generator/tests/blueprint-exams
```

Phase 9 adds Expert and shared-discovery routes without replacing these endpoints:

### Mock Readiness Additive Contracts

Student discovery retains its existing route and gains only an optional `generationType=Fixed|Random` query parameter. The query projects persisted selection reasons, filters before count/pagination, and rejects mixed or unsupported reason sets. Topic Practice consumes a new batch shared `IStudentTopicMasteryProvider`; `IStudentRecommendationProvider` remains the contract for WeakTag, lecture, and material recommendation. The mastery provider returns only requested topic rows for one student and never performs one query per topic.

```text
POST  /api/test-generator/blueprints/{blueprintId}/tests
GET   /api/test-generator/blueprints/{blueprintId}/fixed-test-candidates
POST  /api/test-generator/blueprints/{blueprintId}/fixed-tests
GET   /api/test-generator/blueprints/{blueprintId}/tests
GET   /api/test-generator/tests/{testId}/expert-preview
PATCH /api/test-generator/tests/{testId}/status
GET   /api/test-generator/tests/shared-blueprint-exams
POST  /api/test-generator/tests/resolve-code
```

The Student ID comes from `account_id`, falling back to `ClaimTypes.NameIdentifier`. The POST request contains only `blueprintId`.

## Checkpoint 6A Selection Design

- Load eligible Question and QuestionTopic data through TestGen-owned read models.
- Build a bipartite graph with Question nodes of capacity 1 and BlueprintDetail nodes with capacity `Quantity`.
- An edge exists only when the Question satisfies grade, status, active flag, section QuestionType, detail DifficultyID, and detail TagID.
- Compute a complete capacity assignment. Randomize candidate tie order through an injected randomizer so tests can remain deterministic.
- If maximum flow is below Blueprint.TotalQuestions, return `TEST_GENERATION_INSUFFICIENT_QUESTIONS` before any write.
- Persist Test, TestQuestion, and blueprint activation through the SQL execution strategy with a stable TestID and post-commit verification.

## Aggregate Write Strategy

- Validate the complete request before mutating tracked entities.
- Validate taxonomy in bulk, not one query per detail.
- Generate IDs with `Guid.NewGuid().ToString()`.
- Use an explicit transaction for aggregate create/update/clone/delete.
- Submit/review/delete should use a SQL transaction and reload current state before transition to avoid stale workflow decisions.
- Supply a persisted post-condition verifier to the SQL execution strategy so an ambiguous commit is not blindly replayed.
- Do not expose `ApprovedBy` in write requests.

## Cross-Module Boundaries

- TestGen does not reference QuestionBank persistence classes.
- Read-only external tables are represented by TestGen-owned read models and excluded from migrations.
- Recommender remains an in-process service used only by generation; Blueprint CRUD has no Recommender dependency.
- Testing consumes Test/TestQuestion rows but does not own Blueprint workflow.
- Checkpoint 6A does not reference Recommender and does not create TestSession/TestAnswer rows.
- Student, Question, and QuestionTopic are TestGen read models marked `ExcludeFromMigrations()`.

## Verification

1. `dotnet build MathInsight.sln --no-restore` passes.
2. EF metadata tests assert exact table/column names, SQL types, nullability, keys, indexes, relationships, and status values.
3. Handler tests cover ownership, state transitions, sum validation, taxonomy validation, deep cloning, and delete/deactivate behavior.
4. Controller tests cover 400/403/404/409/422 mapping and authenticated claim extraction.
5. The opt-in disposable SQL Server smoke test verifies the current schema, composite BlueprintDetail FK, and concurrent submit/review transitions. Set `TESTGEN_SQLSERVER_CONNECTION` to a disposable SQL Server `master` connection to run it.
6. Frontend checkpoint runs `npm run build` and performs desktop workflow smoke tests.

## Commit Boundaries

- `fix(testgen): align persistence with current SQL schema`
- `feat(testgen): add blueprint create and query workflow`
- `feat(testgen): add blueprint submit and review workflow`
- `feat(testgen): add blueprint clone and delete workflow`
- `feat(testgen-ui): add expert blueprint management`
