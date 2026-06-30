# Implementation Plan: Question Bank Module

**Branch**: `002-question-bank` | **Date**: 2026-06-23 | **Updated**: 2026-06-30
**Spec**: [spec.md](spec.md)

## Summary

Builds the `MathInsight.Modules.QuestionBank` component managing the full lifecycle of math questions authored through a rich-text/WYSIWYG editor, tag taxonomy (topics + difficulties), expert peer reporting, version history, and bulk import parsing. Registers with YARP and DI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ client) |
| Storage | SQL Server; map to current DB script tables |
| Media Storage | Cloudinary (image upload for UC-22) |
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
│   ├── DeleteQuestion/         # UC-27: soft-delete check, hard-delete if unused
│   ├── ReportQuestion/         # UC-28/29: Student/Expert report → QuestionReport
│   ├── ResolveReport/          # UC-33: Expert resolves their own question's report
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
│   ├── GetReportedQuestions/   # UC-30: Expert views reports for own questions
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

### Database Layer (Current DB Script Tables)

| Table | Key Indexes |
|-------|-------------|
| `Question` | `(Status, IsActive)` index; `ExpertID` index |
| `Answer` | `QuestionID` FK |
| `QuestionVersion` | `QuestionID` FK |
| `QuestionReport` | `QuestionID`, `ReporterAccountID` |
| `TagTopic` | `ParentTagID` self-FK; `TagName` unique |
| `TagDifficulty` | `DifficultyName`, `LevelValue` |
| `QuestionTopic` | Question-topic junction |

### Service & API Gateway — REST Endpoints

**Expert (ExpertOnly policy)**
```
GET    /api/v1/questions                          # UC-19: list (paged + filter)
GET    /api/v1/questions/dashboard                # UC-18: stats
POST   /api/v1/questions                          # UC-20/21: create single question
POST   /api/v1/questions/image-upload             # UC-22: upload image to Cloudinary
POST   /api/v1/questions/import                   # UC-23: bulk import file → queue
GET    /api/v1/questions/{id}/versions            # UC-24: version history
PUT    /api/v1/questions/{id}                     # UC-25: update (auto-snapshot)
PUT    /api/v1/questions/{id}/active              # UC-26: toggle active
DELETE /api/v1/questions/{id}                     # UC-27: delete (soft if in tests)
POST   /api/v1/questions/{id}/report              # UC-29: Expert report question
GET    /api/v1/questions/reports                  # UC-30: view own questions' reports
POST   /api/v1/questions/reports/{reportId}/resolve # UC-33: resolve report
GET    /api/v1/tags/topics                        # UC-34: topic tree (hierarchical)
POST   /api/v1/tags/topics                        # UC-35: create topic tag
PUT    /api/v1/tags/topics/{id}                   # UC-37: update topic tag
DELETE /api/v1/tags/topics/{id}                   # UC-38: delete (if no linked questions)
GET    /api/v1/tags/difficulties                  # UC-34: difficulty list
POST   /api/v1/tags/difficulties                  # UC-36: create difficulty tag
PUT    /api/v1/tags/difficulties/{id}             # UC-37: update difficulty tag
DELETE /api/v1/tags/difficulties/{id}             # UC-38: delete difficulty tag
```

**Student (StudentOnly policy)**
```
POST   /api/v1/questions/{id}/report              # UC-28: report question (creates QuestionReport, no status change)
```

**Admin (AdminOnly policy)**
```
GET    /api/v1/admin/questions/reports            # View all pending question reports
POST   /api/v1/admin/questions/{id}/approve       # UC-31: set status = APPROVED
POST   /api/v1/admin/questions/{id}/reject        # UC-32: set status = REJECTED with note
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `QuestionReportedEvent` | QuestionBank | Notification | Notify question owner of new report |
| `QuestionApprovedEvent` | QuestionBank | Notification | Notify Expert their question is now active |
| `BulkImportCompletedEvent` | MassTransit consumer | Notification | Admin/Expert notified of import result |

### Cross-Module Dependencies

- **TestGen module** reads from `Question` (`Status = Approved`, `IsActive = true`) for blueprint generation.
- **Testing module** references `question_id` in `test_questions` — questions cannot be hard-deleted if referenced.
- **Cloudinary** integration for image upload (UC-22): REST call returns `picture_url`.
- **MassTransit queue**: `excel_import_queue` — file upload pushed to background worker.

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:
   - Create SingleChoice question with 2 answers, 1 correct → 201.
   - Create with no Topic tag → 400 (BR-05).
   - Create TrueFalse with 3 options → 400 (BR-62).
   - Student report question → `QuestionReport` created, question `status` unchanged (BR-58).
   - Teacher attempt to report → 403 (BR-59).
   - Update APPROVED question → `QuestionVersion` snapshot created before save (BR-54).
   - Delete question used in active test → 409 (DC-02).
   - Expert question created → status = APPROVED (BR-55).
