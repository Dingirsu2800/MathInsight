# Implementation Plan: Learning & Lecture Module

**Branch**: `006-learning-lecture` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/specs/006-learning-lecture/spec.md)

## Summary

Builds `MathInsight.Modules.Learning_Lecture` managing lectures (video+text), downloadable materials (PDF/MP4/DOCX via Cloudinary), topic-based browsing, and discussion Q&A moderation. Registers with YARP and DI composition root.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit (RabbitMQ) |
| Storage | SQL Server (Schema: `lrn`) |
| Media Storage | Cloudinary (UC-66: file upload) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Learning_Lecture/
├── Commands/
│   ├── CreateLecture/          # UC-61: Teacher creates DRAFT lecture
│   ├── UpdateLecture/          # UC-62: update content/video (own lectures only)
│   ├── PublishLecture/         # UC-63: DRAFT → PUBLISHED
│   ├── ArchiveLecture/         # UC-64: PUBLISHED → ARCHIVED
│   ├── UploadMaterial/         # UC-66: upload to Cloudinary → save Material
│   ├── UpdateMaterial/         # UC-67
│   ├── ArchiveMaterial/        # UC-68: ACTIVE → INACTIVE
│   ├── AttachMaterialToLecture/# UC-69: insert LectureMaterial junction record
│   ├── AskDiscussionQuestion/  # UC-73: Student posts question
│   ├── AnswerDiscussionQuestion/# UC-74: Teacher/Admin posts answer
│   ├── UpdateDiscussionComment/ # UC-77: own comment update
│   ├── DeleteDiscussionComment/ # UC-78: own comment delete (or Teacher/Admin)
│   ├── HideDiscussionComment/   # UC-79: Teacher/Admin hide
│   └── ReportDiscussion/        # UC-76: Student reports question or answer
├── Queries/
│   ├── GetLectureList/          # UC-60: paged, filter by status/tag/teacher
│   ├── GetLecture/              # UC-71: single lecture + materials + discussion count
│   ├── GetMaterialList/         # UC-65
│   ├── GetTopicList/            # UC-70: hierarchical topic tree (TagTopic from qnb)
│   ├── GetDiscussions/          # List questions for a lecture + answers
│   └── GetModerationQueue/      # UC-75: Teacher/Admin pending reports list
├── Events/
│   ├── ActivityLoggedEvent.cs           # Logged on lecture view / material download → Gamification
│   ├── DiscussionQuestionPostedEvent.cs # → Notification module (notify Teacher)
│   └── DiscussionAnsweredEvent.cs       # → Notification module (notify Student)
├── Services/
│   └── CloudinaryService.cs    # Upload file → return file_url
├── Persistence/
│   ├── LearningDbContext.cs    # `lrn` schema
│   ├── Configurations/
│   │   ├── LectureConfiguration.cs
│   │   ├── MaterialConfiguration.cs
│   │   ├── LectureMaterialConfiguration.cs  # M:N junction
│   │   ├── DiscussionQuestionConfiguration.cs
│   │   ├── DiscussionAnswerConfiguration.cs
│   │   └── DiscussionReportConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── LecturesController.cs
│   ├── MaterialsController.cs
│   └── DiscussionsController.cs
└── LearningLectureModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Schema: `lrn`)

| Table | Key Indexes / Constraints |
|-------|--------------------------|
| `lrn.lectures` | `teacher_id` BTREE; `(status, tag_id)` composite; `status` enum |
| `lrn.materials` | `teacher_id` BTREE; `file_type` enum; `status` enum |
| `lrn.lecture_materials` | Composite PK `(lecture_id, material_id)` — M:N junction |
| `lrn.discussion_questions` | `lecture_id` BTREE; `student_id`; `status` enum |
| `lrn.discussion_answers` | `discussion_question_id` BTREE; `account_id` |
| `lrn.discussion_reports` | CHECK exactly one of `discussion_question_id` / `discussion_answer_id` non-null (DC-06) |

### Service & API Gateway — REST Endpoints

**Teacher (TeacherOnly + ownership check)**
```
GET    /api/v1/lectures                              # UC-60: own lecture list (paged + filter)
POST   /api/v1/lectures                              # UC-61: create DRAFT lecture
PUT    /api/v1/lectures/{id}                         # UC-62: update (own only)
PUT    /api/v1/lectures/{id}/publish                 # UC-63: → PUBLISHED
PUT    /api/v1/lectures/{id}/archive                 # UC-64: → ARCHIVED
GET    /api/v1/materials                             # UC-65: own material list
POST   /api/v1/materials/upload                      # UC-66: upload to Cloudinary
PUT    /api/v1/materials/{id}                        # UC-67: update metadata
PUT    /api/v1/materials/{id}/archive                # UC-68: → INACTIVE
POST   /api/v1/lectures/{id}/materials/{materialId}  # UC-69: attach material to lecture
POST   /api/v1/discussions/questions/{qId}/answers   # UC-74: Teacher answers
PUT    /api/v1/discussions/comments/{id}             # UC-77: update own comment
DELETE /api/v1/discussions/comments/{id}             # UC-78: delete own comment
GET    /api/v1/teacher/discussions/moderation        # UC-75: pending reports
POST   /api/v1/teacher/discussions/moderation/{reportId}/resolve  # UC-75: resolve report
PUT    /api/v1/discussions/comments/{id}/hide        # UC-79: hide comment/question
```

**Student (StudentOnly)**
```
GET    /api/v1/lectures                              # UC-70/71: PUBLISHED lectures, topic tree view
GET    /api/v1/lectures/{id}                         # UC-71: single lecture + materials
GET    /api/v1/materials/{id}/download               # UC-72: download material (logs ActivityLoggedEvent)
GET    /api/v1/discussions/{lectureId}               # List discussion questions
POST   /api/v1/discussions/{lectureId}/questions     # UC-73: ask question
POST   /api/v1/discussions/reports                   # UC-76: report question or answer (DC-06)
PUT    /api/v1/discussions/comments/{id}             # UC-77: update own comment/answer
DELETE /api/v1/discussions/comments/{id}             # UC-78: delete own comment
```

**Admin (AdminOnly)**
```
PUT    /api/v1/admin/lectures/{id}/archive            # UC-64: Admin can archive any lecture
GET    /api/v1/admin/discussions/moderation           # UC-75: all pending reports
POST   /api/v1/admin/discussions/moderation/{id}/resolve  # Resolve/dismiss report
PUT    /api/v1/admin/discussions/comments/{id}/hide   # UC-79: Admin hides
```

### Integration & Domain Events

| Event | Publisher | Consumer | Purpose |
|-------|-----------|----------|---------|
| `ActivityLoggedEvent` | Learning | Gamification (007) | Streak + badge tracking |
| `DiscussionQuestionPostedEvent` | Learning | Notification (008) | Notify Teacher |
| `DiscussionAnsweredEvent` | Learning | Notification (008) | Notify Student of answer |

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF migration: `lrn` schema tables created; M:N `lecture_materials` junction.
3. Integration tests:
   - UC-61: Create lecture → `status = DRAFT`.
   - UC-63: Publish → `status = PUBLISHED`.
   - UC-64: Archive → `status = ARCHIVED`; cannot revert.
   - UC-62: Teacher edits another's lecture → 403 (BR-31).
   - UC-66: Upload .exe → 415; upload 600MB → 413; valid PDF → Cloudinary URL saved.
   - UC-69: Attach material to lecture → M:N record created.
   - UC-73: Student posts question → `DiscussionQuestionPostedEvent` published.
   - UC-76: Report with both IDs set → 422 (DC-06).
   - UC-76: Report with both null → 422 (DC-06).
   - UC-79: Teacher hides question → `status = HIDDEN`.
   - UC-71: View lecture → `ActivityLoggedEvent` published.