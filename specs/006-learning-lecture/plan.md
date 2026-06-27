# Implementation Plan: Learning & Lecture Module

**Branch**: `006-learning-lecture` | **Date**: 2026-06-23 | **Updated**: 2026-06-27

**Spec**: [spec.md](spec.md)

## Summary

Build `MathInsight.Modules.Learning_Lecture` for lectures, lecture likes, downloadable materials, topic-based browsing, and discussion Q&A moderation. Register the module with the WebAPI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit abstraction already registered by WebAPI |
| Storage | SQL Server; map explicitly to current DB script tables and columns |
| Media Storage | Cloudinary |
| Testing | xUnit / integration tests when test project exists |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Learning_Lecture/
├── Commands/
│   ├── CreateLecture/           # UC-61: Teacher creates Draft lecture
│   ├── UpdateLecture/           # UC-62: update own lecture
│   ├── PublishLecture/          # UC-63: Draft → Published
│   ├── DeactivateLecture/       # UC-64: Published → Deactivated
│   ├── LikeLecture/             # UC-80: Student likes a Published lecture
│   ├── UnlikeLecture/           # UC-80: Student removes own like
│   ├── UploadMaterial/          # UC-66: upload to Cloudinary → save Material
│   ├── UpdateMaterial/          # UC-67
│   ├── DeactivateMaterial/      # UC-68: Active → Deactivated
│   ├── AttachMaterialToLecture/ # UC-69: insert LectureMaterial junction record
│   ├── AskDiscussionQuestion/   # UC-73: Student posts question
│   ├── AnswerDiscussionQuestion/# UC-74: Teacher/Admin posts answer
│   ├── UpdateDiscussionComment/ # UC-77: own comment update
│   ├── DeleteDiscussionComment/ # UC-78: set status Deleted
│   ├── HideDiscussionComment/   # UC-79: set status Hidden
│   └── ReportDiscussion/        # UC-76: create DiscussionReport
├── Queries/
│   ├── GetLectureList/
│   ├── GetLecture/
│   ├── GetMaterialList/
│   ├── GetTopicList/
│   ├── GetDiscussions/
│   └── GetModerationQueue/
├── Events/
│   ├── ActivityLoggedEvent.cs
│   ├── DiscussionQuestionPostedEvent.cs
│   └── DiscussionAnsweredEvent.cs
├── Services/
│   └── CloudinaryService.cs
├── Persistence/
│   ├── LearningDbContext.cs
│   └── Configurations/
│       ├── LectureConfiguration.cs
│       ├── LectureLikeConfiguration.cs
│       ├── MaterialConfiguration.cs
│       ├── LectureMaterialConfiguration.cs
│       ├── DiscussionQuestionConfiguration.cs
│       ├── DiscussionAnswerConfiguration.cs
│       └── DiscussionReportConfiguration.cs
├── Controllers/
│   ├── LecturesController.cs
│   ├── MaterialsController.cs
│   └── DiscussionsController.cs
└── LearningModuleExtensions.cs
```

## Database Layer (Current DB Script Tables)

| Table | Key Mapping / Constraints |
|-------|---------------------------|
| `Lecture` | `LectureID`, `TeacherID`, `TagID`, `Status` = `Draft`/`Published`/`Deactivated`, `Likes >= 0` |
| `LectureLike` | Composite PK `(LectureID, StudentID)`; FK to `Lecture` + `Student` |
| `Material` | `MaterialID`, `TeacherID`, `Status` = `Active`/`Deactivated` |
| `LectureMaterial` | Composite PK `(LectureID, MaterialID)` |
| `DiscussionQuestion` | `Status` = `Active`/`Hidden`/`Deleted` |
| `DiscussionAnswer` | `Status` = `Active`/`Hidden`/`Deleted` |
| `DiscussionReport` | `Status` = `Pending`/`Resolved`/`Dismissed`; exactly one target FK must be non-null |

Do not create an `lrn` schema or EF migration for MVP implementation unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.

## REST Endpoints

**Teacher**

```text
GET    /api/v1/lectures
POST   /api/v1/lectures
PUT    /api/v1/lectures/{id}
PUT    /api/v1/lectures/{id}/publish
PUT    /api/v1/lectures/{id}/deactivate
GET    /api/v1/materials
POST   /api/v1/materials/upload
PUT    /api/v1/materials/{id}
PUT    /api/v1/materials/{id}/deactivate
POST   /api/v1/lectures/{id}/materials/{materialId}
POST   /api/v1/discussions/questions/{questionId}/answers
PUT    /api/v1/discussions/comments/{id}
DELETE /api/v1/discussions/comments/{id}
GET    /api/v1/teacher/discussions/moderation
POST   /api/v1/teacher/discussions/moderation/{reportId}/resolve
PUT    /api/v1/discussions/comments/{id}/hide
```

**Student**

```text
GET    /api/v1/lectures
GET    /api/v1/lectures/{id}
POST   /api/v1/lectures/{id}/like
DELETE /api/v1/lectures/{id}/like
GET    /api/v1/materials/{id}/download
GET    /api/v1/discussions/{lectureId}
POST   /api/v1/discussions/{lectureId}/questions
POST   /api/v1/discussions/reports
PUT    /api/v1/discussions/comments/{id}
DELETE /api/v1/discussions/comments/{id}
```

**Admin**

```text
PUT    /api/v1/admin/lectures/{id}/deactivate
GET    /api/v1/admin/discussions/moderation
POST   /api/v1/admin/discussions/moderation/{id}/resolve
PUT    /api/v1/admin/discussions/comments/{id}/hide
```

## Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `ActivityLoggedEvent` | Learning | Gamification (007) | Streak + badge tracking |
| `DiscussionQuestionPostedEvent` | Learning | Notification (008) | Notify Teacher |
| `DiscussionAnsweredEvent` | Learning | Notification (008) | Notify Student |

## Verification Plan

1. `dotnet build` succeeds.
2. Database script verification confirms `Lecture.Likes`, `LectureLike`, and `LectureMaterial` exist.
3. Integration behavior to verify:
   - UC-61: Create lecture → `Status = 'Draft'`.
   - UC-63: Publish → `Status = 'Published'`.
   - UC-64: Deactivate → `Status = 'Deactivated'`; cannot publish again.
   - UC-62: Teacher edits another Teacher's lecture → 403.
   - UC-66: Upload `.exe` → 415; upload > 500MB → 413; valid PDF → Cloudinary URL saved.
   - UC-69: Attach material to lecture → `LectureMaterial` row created.
   - UC-80: Like lecture twice by same Student → no duplicate `LectureLike`.
   - UC-80: Unlike lecture → `LectureLike` removed and `Lecture.Likes` decremented but never below zero.
   - UC-73: Student posts question → `DiscussionQuestionPostedEvent` published.
   - UC-76: Invalid report target payload → 422.
   - UC-76: Valid report → `DiscussionReport.Status = 'Pending'`; target discussion status remains unchanged.
   - UC-79: Teacher hides question/answer → `Status = 'Hidden'`.
   - UC-78: Delete question/answer → `Status = 'Deleted'`.
   - UC-71: View lecture → `ActivityLoggedEvent` published.
