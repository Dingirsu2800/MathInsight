# Implementation Plan: Test Generator Module

**Branch**: `009-test-generator` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.TestGen` managing blueprint lifecycle (CRUD, peer review, clone) and adaptive test generation engine. Communicates with Recommender module in-process for WeakTag data. Creates `Test` + `TestQuestion` records consumed by Testing module (003).

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server; map to current DB script tables shared with Testing |
| Internal API | `IRecommenderService` from Recommender module (005) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.TestGen/
├── Commands/
│   ├── CreateBlueprint/         # UC-43: create DRAFT blueprint with detail slots
│   ├── SubmitBlueprintForReview/# Transition DRAFT → PENDING_REVIEW; validate sum (BR-07)
│   ├── ReviewBlueprint/         # UC-41: Approve (→ APPROVED) / Reject (→ REJECTED + note)
│   ├── UpdateBlueprint/         # UC-45: only if DRAFT or REJECTED
│   ├── CloneBlueprint/          # UC-44: deep-copy with new UUID
│   ├── DeleteBlueprint/         # UC-46: hard-delete if unused; soft if ACTIVE
│   └── GenerateTest/            # Create Test + TestQuestion records from approved blueprint
├── Queries/
│   ├── GetBlueprintList/        # UC-42: paged, filter by status/grade/name
│   ├── GetPendingBlueprints/    # UC-40: PENDING_REVIEW excluding currentExpert's own
│   └── GetBlueprintById/        # Single blueprint with detail slots
├── Services/
│   ├── IGenerationEngine.cs
│   └── GenerationEngine.cs      # Adaptive question selection with WeakTag bias
├── Events/
│   └── BlueprintRejectedEvent.cs # MediatR → Notification module (notify creator)
├── Persistence/
│   ├── TestGenDbContext.cs       # maps to current DB script table names
│   ├── Configurations/
│   │   ├── BlueprintConfiguration.cs
│   │   └── BlueprintDetailConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── BlueprintsController.cs
│   └── TestGenerationController.cs
└── TestGenModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Key Constraints |
|-------|----------------|
| `Blueprint` | Blueprint metadata; status check from DB script; `ExpertID` FK |
| `BlueprintDetail` | Blueprint slot details; `BlueprintID`, `TagID`, `DifficultyID`, `Quantity` |

**Note**: Tables `Test` and `TestQuestion` are created during test generation and read by Testing module (003).

### Service & API Gateway — REST Endpoints

**Expert (ExpertOnly policy)**
```
GET    /api/v1/blueprints                     # UC-42: list all (own + others, paged + filter)
GET    /api/v1/blueprints/pending             # UC-40: pending review (exclude own, BR-Blueprint-01)
GET    /api/v1/blueprints/{id}               # Single blueprint + detail slots
POST   /api/v1/blueprints                     # UC-43: create DRAFT blueprint
POST   /api/v1/blueprints/{id}/submit         # Submit DRAFT → PENDING_REVIEW
POST   /api/v1/blueprints/{id}/review         # UC-41: Approve or Reject (with note)
POST   /api/v1/blueprints/{id}/clone          # UC-44: deep-copy
PUT    /api/v1/blueprints/{id}               # UC-45: update (DRAFT/REJECTED only)
DELETE /api/v1/blueprints/{id}               # UC-46: hard/soft delete
```

**Student (StudentOnly policy)**
```
POST   /api/v1/tests/generate                 # Generate test from approved blueprint for student
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `BlueprintRejectedEvent` | TestGen | Notification (008) | Notify Expert creator of rejection + review_note |
| `BlueprintApprovedEvent` | TestGen | Notification (008) | Notify Expert creator of approval |

### Test Generation Engine

```csharp
// GenerationEngine.GenerateAsync(blueprintId, studentId):
// 1. Load Blueprint + BlueprintDetail slots
// 2. For each BlueprintDetail slot (tag_id, difficulty_id, quantity):
//    a. Call IRecommenderService.GetStudentWeakTagsAsync(studentId)
//    b. Check if this slot's (tag_id, difficulty_id) is in student's WeakTags:
//       - YES (WeakTag):
//         * Cap: WeakTag-biased questions ≤ 20% of total_questions (BR-WeakTag-Cap)
//         * Bias probability: 40% → prefer WeakTag questions (reduce from 70%)
//         * Difficulty downscale: if WeakTag at Hard → query Medium instead
//         * Remedial: if WeakTag at Easy AND P_tag < 5.0 → bias = 10%, no downscale
//       - NO or MASTERED: standard selection
//    c. Query Question WHERE:
//       - question_id NOT IN (previous test sessions for this student, last 7 days) — dedup
//       - tag_id = slot.tag_id (or adjusted difficulty)
//       - difficulty_id = slot.difficulty_id (adjusted if WeakTag)
//       - status = APPROVED AND is_active = true
//    d. Random sample `quantity` questions from candidate pool
// 3. Create Test record with generated test_code (short unique ID)
// 4. Create TestQuestion records (ordered by question_order)
// 5. Return Test entity with session-start URL
```

### Blueprint Validation (BR-07)

```csharp
// ValidateBlueprintSum(blueprintId):
// var totalSlotQty = blueprint_details.Sum(d => d.quantity);
// if (totalSlotQty != blueprint.total_questions)
//     throw ValidationException("Slot quantities must sum to total_questions (BR-07)");
```

### Self-Approval Guard (BR-Blueprint-01)

```csharp
// In ReviewBlueprintHandler:
// if (blueprint.expert_id == currentUserId)
//     throw ForbiddenException("Cannot review your own blueprint (BR-Blueprint-01)");
```

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests (xUnit):
   - UC-43: Create blueprint with 3 slots summing to 40 questions → DRAFT.
   - Submit for review → PENDING_REVIEW; slots summing to 39 → 422 (BR-07).
   - UC-41: Expert A approves Expert B's blueprint → APPROVED.
   - UC-41: Expert A approves own blueprint → 403 (BR-Blueprint-01).
   - UC-41: Reject without note → 400 (BR-Blueprint-03).
   - UC-44: Clone APPROVED blueprint → new UUID, all details copied, status = DRAFT.
   - UC-45: Update APPROVED blueprint → 422 (BR-47).
   - UC-46: Delete ACTIVE blueprint → soft-delete only.
   - Test generation from APPROVED blueprint → 40 `TestQuestion` records created.
   - WeakTag bias: student with WeakTag in Algebra-Medium → generation biases toward Medium questions (40% probability).
