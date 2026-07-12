# Implementation Plan: Question Bank Module

**Branch**: `002-question-bank` | **Date**: 2026-06-23 | **Updated**: 2026-07-10
**Spec**: [spec.md](spec.md)

## Summary

Builds the `MathInsight.Modules.QuestionBank` component managing the full lifecycle of math questions authored through a rich-text/WYSIWYG editor, tag taxonomy (topics + difficulties), expert peer reporting, version history, and bulk import parsing. Registers with YARP and DI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server; map to current DB script tables |
| Media/OCR | Cloudinary (image upload for UC-22), Mistral OCR (unpersisted draft for UC-39) |
| File Parsing | EPPlus (Excel), OpenXml SDK (Word) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.QuestionBank/
├── Commands/
│   ├── CreateQuestion/         # UC-20/21: CreateQuestionCommand + Handler
│   ├── ImportQuestions/        # UC-23: BulkImportCommand → MassTransit queue
│   ├── UpdateQuestion/         # UC-25: captures QuestionVersion snapshot before save
│   ├── ToggleQuestionActive/   # UC-26: set is_active true/false
│   ├── DeleteQuestion/         # UC-27: reject if used in TestQuestion, hard-delete if unused
│   ├── ReportQuestion/         # UC-28/29: Student/Expert report → QuestionReport
│   ├── HandleQuestionReport/   # UC-33: Question owner resolves or dismisses a report
│   ├── AdminApproveQuestion/   # UC-31: set status = APPROVED
│   ├── AdminRejectQuestion/    # UC-32: set status = REJECTED
│   ├── CreateTagTopic/         # UC-35
│   ├── CreateTagDifficulty/    # UC-36
│   ├── UpdateTag/              # UC-37
│   └── DeleteTag/              # UC-38: check no linked questions
├── Queries/
│   ├── GetDashboard/           # UC-18: counts by status, type, grade
│   ├── GetQuestionList/        # UC-19: paged, filter by status/grade/tag/type
│   ├── GetQuestionVersions/    # UC-24: version history for a question
│   ├── GetOwnedReportedQuestions/ # UC-30: Expert views reports on own questions
│   ├── GetQuestionReports/     # UC-33: Expert views report details for one owned question
│   ├── GetTagList/             # UC-34: hierarchical topic tree + difficulty list
│   └── GetAdminReports/        # Admin views all pending question reports
├── Events/
│   └── QuestionReportedEvent.cs        # MediatR notification → Notification module
├── Parsers/
│   ├── IQuestionFileParser.cs
│   ├── ExcelQuestionParser.cs  # EPPlus
│   └── WordQuestionParser.cs   # OpenXml SDK
├── Persistence/
│   ├── QuestionBankDbContext.cs
│   ├── Configurations/
│   │   ├── QuestionConfiguration.cs
│   │   ├── AnswerConfiguration.cs
│   │   ├── QuestionVersionConfiguration.cs
│   │   ├── QuestionReportConfiguration.cs
│   │   ├── TagTopicConfiguration.cs
│   │   ├── TagDifficultyConfiguration.cs
│   │   └── QuestionTopicConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── QuestionsController.cs
│   ├── TagsController.cs
│   └── ReportsController.cs
└── QuestionBankModuleExtensions.cs
```

## Proposed Changes

### Reporting Consistency

Migration `003_Admin_QuestionReport_Review_Workflow.sql` extends `QuestionReport` with review audit fields and workflow statuses. On SQL Server, report creation, report handling, Admin review, and Question deletion use a `Serializable` transaction and an `UPDLOCK, HOLDLOCK` row lock on the related `QuestionID`. This serializes mutations for one Question, enforcing one active Admin workflow and preventing stale Question status transitions. The in-memory test provider uses the same business rules without SQL Server locking.

### Database Layer (Current DB Script Tables)

| Table | Key Indexes |
|-------|-------------|
| `Question` | `(Status, IsActive)` index; `ExpertID` index |
| `Answer` | `QuestionID` FK |
| `QuestionVersion` | `QuestionID` FK |
| `QuestionReport` | `QuestionID`, `ReporterAccountID` |
| `TagTopic` | `ParentTagID` self-FK; `TagName` unique |
| `TagDifficulty` | `DifficultyName` unique; `LevelValue` unique/stable for Recommender/TestGen v2 mapping |
| `QuestionTopic` | Question-topic junction |

### Service & API Gateway — REST Endpoints

**Expert (ExpertOnly policy)**
```
GET    /api/v1/questions                          # UC-19: list (paged + filter)
GET    /api/v1/questions/dashboard                # UC-18: stats
POST   /api/v1/questions                          # UC-20/21: create single question
POST   /api/question-bank/questions/image-upload  # UC-22: authenticated backend upload to Cloudinary
POST   /api/question-bank/questions/ocr-draft     # UC-39: one-image OCR draft; 10 requests/minute per Expert
POST   /api/v1/questions/import                   # UC-23: bulk import file → queue
GET    /api/v1/questions/{id}/versions            # UC-24: version history
PUT    /api/v1/questions/{id}                     # UC-25: update (auto-snapshot)
PUT    /api/v1/questions/{id}/active              # UC-26: toggle active
DELETE /api/question-bank/questions/{id}          # UC-27: hard-delete only when no TestQuestion reference; otherwise 409
POST   /api/question-bank/questions/{id}/reports  # UC-28/29: Student, Expert, or Admin creates a report
GET    /api/question-bank/reports/mine            # UC-30: Expert views reports on own questions
GET    /api/question-bank/questions/{id}/reports  # UC-33: Expert views report details for an owned question
PATCH  /api/question-bank/reports/{reportId}      # UC-33: Expert resolves or dismisses a report
POST   /api/question-bank/reports/{reportId}/submit-review # Expert submits Admin report after fixing
GET    /api/question-bank/tags/topics              # UC-34: topic tree; `includeInactive=false` by default
POST   /api/v1/tags/topics                        # UC-35: create topic tag
PUT    /api/question-bank/tags/topics/{id}         # UC-37: update; cannot disable with active descendants
DELETE /api/question-bank/tags/topics/{id}         # UC-38: soft-disable; cannot bypass active-descendant rule
GET    /api/question-bank/tags/difficulties        # UC-34: difficulty list; `includeInactive=false` by default
POST   /api/v1/tags/difficulties                  # UC-36: create difficulty tag
PUT    /api/v1/tags/difficulties/{id}             # UC-37: update difficulty tag
DELETE /api/v1/tags/difficulties/{id}             # UC-38: delete difficulty tag
```

**Student (StudentOnly policy)**
```
POST   /api/question-bank/questions/{id}/reports  # UC-28: report question (creates QuestionReport, no status change)
```

**Admin (AdminOnly policy)**
```
GET    /api/question-bank/admin/reports/mine      # Admin views own Admin-report workflows
POST   /api/question-bank/admin/reports/{reportId}/approve # Original Admin reporter approves
POST   /api/question-bank/admin/reports/{reportId}/reject  # Original Admin reporter rejects with note
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `QuestionReportedEvent` | QuestionBank | Notification | Notify question owner of new report |
| `QuestionApprovedEvent` | QuestionBank | Notification | Notify Expert their question is now active |
| `BulkImportCompletedEvent` | MassTransit consumer | Notification | Admin/Expert notified of import result |

### Cross-Module Dependencies

- **TestGen module** reads from `Question` (`Status = Approved`, `IsActive = true`) for blueprint generation.
- **Testing/Grading modules** read `QuestionPart` for `Composite` questions and write per-part student answers to `TestAnswerPart`.
- **Recommender/TestGen v2 contract**: Recommender stores Ptag by `StudentID + TagID` only. TestGen maps `TagsMastery.RecommendedDifficultyLevel` to `TagDifficulty.LevelValue`, then filters `Question.DifficultyID` plus `QuestionTopic.TagID`. If a `BlueprintSection` is used, TestGen also filters `Question.QuestionType` by the section's `QuestionType`. Do not remove `Question.DifficultyID`, `Question.QuestionType`, or `TagDifficulty` from QuestionBank.
- **Testing module** references `question_id` in `test_questions` — a Question with any existing reference cannot be hard-deleted or deactivated and returns `409 QUESTION_IN_USE`.
- **Cloudinary** integration for image upload (UC-22): an Expert posts multipart field `file` to `POST /api/question-bank/questions/image-upload`. `IQuestionImageStorage` authenticates and forwards JPEG/PNG/WebP files (max 5 MB) to Cloudinary REST using server-side HTTP Basic authentication, then returns only `picture_url`. No Cloudinary secret, authorization header, raw response, or OCR behavior is exposed to frontend clients.
- **Mistral OCR** integration (UC-39): an Expert posts exactly one complete question image as multipart field `file` to `POST /api/question-bank/questions/ocr-draft`. The backend applies the same JPEG/PNG/WebP 5 MB magic-byte validation, rate limits by authenticated Expert (10/minute, no queue), and returns an unpersisted draft plus up to three detected image candidates. The Expert may choose one candidate or the original source image; selection is uploaded through the existing Cloudinary endpoint only when applying the draft. Mistral credentials and raw provider failures stay server-side; answer suggestions and image candidates are never authoritative.
- **MassTransit queue**: `excel_import_queue` — file upload pushed to background worker.

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:
   - Create SingleChoice question with 2 answers, 1 correct → 201.
   - Create Composite question with 4 TrueFalse parts (`a`-`d`) → 201; student-facing response does not include `CorrectBoolean`, `CorrectText`, `CorrectNumeric`, or `NumericTolerance`.
   - Create with no Topic tag → 400 (BR-05).
   - Create TrueFalse with 3 options → 400 (BR-62).
   - Student report question → `QuestionReport` created, question `status` unchanged (BR-58).
   - Teacher attempt to report → 403 (BR-59).
   - Update APPROVED or REPORTED question → `QuestionVersion` snapshot created before save (BR-54).
   - Delete or deactivate question used in any existing TestQuestion record → 409 and no data mutation (DC-02).
   - Tag queries exclude inactive records by default and include them only when `includeInactive=true` (BR-65).
   - Disable or soft-delete a topic with an active child/grandchild → 409 and no data mutation (BR-66).
   - In a disposable SQL Server test database, run `tests/MathInsight.Modules.QuestionBank.Tests/Manual/QuestionReportSqlServerLockSmoke.sql` in two sessions and verify the second `UPDLOCK, HOLDLOCK` request blocks while the first transaction is open.
   - Expert question created → status = APPROVED (BR-55).
   - Recommender/TestGen mapping: `RecommendedDifficultyLevel = 2` resolves to `TagDifficulty.LevelValue = 2`, then selects only approved active questions with the matching `Question.DifficultyID`, `QuestionTopic.TagID`, and section `Question.QuestionType` when provided.
   - API enum mapping persists correct DB values: `TRUE_FALSE` -> `TrueFalse`, `MULTIPLE_SELECT` -> `MultipleChoice`, `COMPOSITE` -> `Composite`.
   - Image upload accepts valid JPEG/PNG/WebP and rejects empty, oversized, unsupported, forged MIME/magic-byte files. Mock Cloudinary tests cover missing configuration, timeout, HTTP error, and response without `secure_url`.
