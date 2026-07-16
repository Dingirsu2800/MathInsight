# Implementation Plan: Test Generator Module

**Branch**: `testgen-test-generation` | **Updated**: 2026-07-16
**Spec**: [spec.md](spec.md)

## Summary

The Expert Blueprint lifecycle is complete. Implement Checkpoint 6A as a Student-facing, baseline non-adaptive BlueprintExam generation slice that creates Test and TestQuestion rows from an approved blueprint without Recommender or Testing-session concerns. The current SQL creation script is the persistence contract; no migration or table rename is permitted.

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

- Repair and verify the Recommender SQL contract before integration.
- Introduce a stable cross-module advice contract.
- Resolve recommended difficulty levels and apply the approved WeakTag cap/bias/downscale rules.
- Populate adaptive TestQuestion audit fields and add recent-question deduplication after its product rule is finalized.

### Checkpoint 6C: TopicPractice

- Generate exactly 10 questions for one selected WeakTag using `TestMode = TopicPractice`.
- Keep BlueprintID null and hand the generated Test to Testing for a Practice TestSession.

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
