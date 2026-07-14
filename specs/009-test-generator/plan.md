# Implementation Plan: Test Generator Module

**Branch**: `testgen-blueprint` | **Updated**: 2026-07-14
**Spec**: [spec.md](spec.md)

## Summary

Implement the Expert Blueprint lifecycle first. Test generation remains a later slice after Blueprint APIs and frontend are stable. The current SQL creation script is the persistence contract; no migration or table rename is permitted in this work.

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
|   `-- GetBlueprintDetail/
|-- Contracts/Blueprints/
|-- Errors/TestGenErrors.cs
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
- Add `Expert`, `TagTopic`, and `TagDifficulty` read models needed for validation, configured with `ExcludeFromMigrations()`.
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

### Checkpoint 6: Test Generation (Later)

- Complete QuestionBank and Testing read models.
- Implement BlueprintExam and practice generation against SQL `TestMode` values.
- Integrate `IRecommenderService`, resolve difficulty levels, deduplicate recent questions, and populate TestQuestion audit fields.

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

## Aggregate Write Strategy

- Validate the complete request before mutating tracked entities.
- Validate taxonomy in bulk, not one query per detail.
- Generate IDs with `Guid.NewGuid().ToString()`.
- Use an explicit transaction for aggregate create/update/clone/delete.
- Submit/review/delete should use a SQL transaction and reload current state before transition to avoid stale workflow decisions.
- Do not expose `ApprovedBy` in write requests.

## Cross-Module Boundaries

- TestGen does not reference QuestionBank persistence classes.
- Read-only external tables are represented by TestGen-owned read models and excluded from migrations.
- Recommender remains an in-process service used only by generation; Blueprint CRUD has no Recommender dependency.
- Testing consumes Test/TestQuestion rows but does not own Blueprint workflow.

## Verification

1. `dotnet build MathInsight.sln --no-restore` passes.
2. EF metadata tests assert exact table/column names, SQL types, nullability, keys, indexes, relationships, and status values.
3. Handler tests cover ownership, state transitions, sum validation, taxonomy validation, deep cloning, and delete/deactivate behavior.
4. Controller tests cover 400/403/404/409/422 mapping and authenticated claim extraction.
5. A disposable SQL Server smoke test verifies composite BlueprintDetail FK and concurrent submit/review transitions before merge.
6. Frontend checkpoint runs `npm run build` and performs desktop workflow smoke tests.

## Commit Boundaries

- `fix(testgen): align persistence with current SQL schema`
- `feat(testgen): add blueprint create and query workflow`
- `feat(testgen): add blueprint submit and review workflow`
- `feat(testgen): add blueprint clone and delete workflow`
- `feat(testgen-ui): add expert blueprint management`
