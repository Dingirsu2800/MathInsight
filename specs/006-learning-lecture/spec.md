# Feature Specification: Learning & Lecture Module

**Feature Branch**: `006-learning-lecture`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-08, FT-09, FT-10), UCS UC-60–UC-79, TDS §3 (lectures, materials, discussions)

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor |
|-------|------|---------------|
| UC-60 | View Lecture List | Teacher |
| UC-61 | Create Lecture | Teacher |
| UC-62 | Update Lecture | Teacher |
| UC-63 | Publish Lecture | Teacher, Admin |
| UC-64 | Archive Lecture | Teacher, Admin |
| UC-65 | View Material List | Teacher |
| UC-66 | Upload Material | Teacher |
| UC-67 | Update Material | Teacher |
| UC-68 | Archive Material | Teacher |
| UC-69 | Attach Material to Lecture | Teacher |
| UC-70 | View Topic List | Student |
| UC-71 | View Lecture | Student |
| UC-72 | View Learning Material | Student |
| UC-73 | Ask Question on Lecture | Student |
| UC-74 | Answer Question on Lecture | Teacher |
| UC-75 | Moderate Discussion | Teacher, Admin |
| UC-76 | Report Discussion/Reply | Student |
| UC-77 | Update Comment/Reply | Student, Teacher |
| UC-78 | Delete Comment/Reply | Student, Teacher |
| UC-79 | Hide Discussion/Reply | Teacher, Admin |

### Edge Cases

- **Expired lecture link**: Video URL broken → display error, suggest alternative materials.
- **File size over limit**: Upload > 500 MB → HTTP 413 with limit explanation.
- **Blocked file format**: Upload .exe, .bat → HTTP 415 Unsupported Media Type.
- **Ownership violation**: Teacher attempts to edit another Teacher's lecture → HTTP 403.
- **Discussion report both null**: `discussion_question_id` AND `discussion_answer_id` both null → HTTP 422 (DC-06).
- **Discussion report both set**: Both non-null → HTTP 422 (DC-06).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02**: Lectures and Materials are soft-deleted (`status = ARCHIVED` or `status = INACTIVE`) if referenced elsewhere. No hard-delete on published/used entities.
- **DC-06**: A `DiscussionReport` must have **exactly one** of `discussion_question_id` or `discussion_answer_id` set as non-null. Both null or both non-null → HTTP 422.
- **BR-25**: Maximum uploaded file size is **500 MB** for media materials.
- **BR-26**: Accepted file formats: **PDF**, **MP4**, **DOCX**. Blocked: executables (.exe, .bat, .sh, .ps1).
- **BR-31**: Teachers can only manage (edit, publish, archive) lectures or materials **they created** (`teacher_id = currentUserId`).
- **BR-32**: Lecture status lifecycle: `DRAFT → PUBLISHED → ARCHIVED`. Cannot revert from ARCHIVED.
- **BR-33**: Material status lifecycle: `ACTIVE ↔ INACTIVE`. Teachers can toggle freely until archived.
- **BR-34**: Lecture-Material relationship is **Many-to-Many** (per ERD). A material can be attached to multiple lectures; a lecture can have multiple materials.
- **BR-35**: `DiscussionQuestion.status` lifecycle: `ACTIVE → REPORTED → HIDDEN` (terminal) or `ACTIVE → SOLVED`.
- **BR-36**: `DiscussionAnswer.status` lifecycle: `ACTIVE → REPORTED → HIDDEN` (terminal).
- **BR-37**: Student activity (UC-71: viewing a lecture, UC-72: downloading a material) must be logged to Gamification module via `ActivityLoggedEvent` (for streak tracking).
- **BR-38**: Teacher receives notification when a student posts a discussion question on their lecture (UC-73).

### File Upload Rules

| Format | Max Size | Cloudinary | Notes |
|--------|----------|------------|-------|
| PDF | 500 MB | Yes | Downloadable by students |
| MP4 | 500 MB | Yes | Video streaming |
| DOCX | 500 MB | Yes | Document preview |
| .exe, .bat | Blocked | — | HTTP 415 |

### Key Entities *(include if feature involves data)*

- **Lecture**: `lecture_id`, `title` (VARCHAR 100), `content` (TEXT, rich-text+LaTeX), `video_url`, `thumbnail_url`, `teacher_id` (FK → teachers), `tag_id` (FK → tag_topics), `status` (**DRAFT** | **PUBLISHED** | **ARCHIVED**), `created_time`, `updated_time`
- **Material**: `material_id`, `material_name`, `file_url` (Cloudinary URL), `file_type` (PDF/MP4/DOCX), `teacher_id` (FK), `status` (**ACTIVE** | **INACTIVE**), `uploaded_time`
- **LectureMaterial** *(junction, Many-to-Many)*: `lecture_id` (FK), `material_id` (FK) — composite PK
- **DiscussionQuestion**: `discussion_question_id`, `lecture_id` (FK), `student_id` (FK), `title`, `content`, `status` (**ACTIVE** | **REPORTED** | **HIDDEN** | **SOLVED**), `created_time`, `updated_time`
- **DiscussionAnswer**: `discussion_answer_id`, `discussion_question_id` (FK), `account_id` (FK — any authenticated role), `content`, `created_time`, `status` (**ACTIVE** | **REPORTED** | **HIDDEN**), `updated_time`
- **DiscussionReport**: `report_id`, `discussion_question_id` (FK, nullable), `discussion_answer_id` (FK, nullable), `reporter_account_id` (FK), `report_reason`, `status` (**PENDING** | **RESOLVED** | **DISMISSED**), `created_time`, `resolved_time`, `resolver_account_id` (FK, nullable)

### Discussion State Machines

```
DiscussionQuestion:    ACTIVE → REPORTED (by Student)
                       ACTIVE → SOLVED (by Teacher)
                       REPORTED → HIDDEN (by Teacher/Admin) [terminal]

DiscussionAnswer:      ACTIVE → REPORTED (by Student)
                       REPORTED → HIDDEN (by Teacher/Admin) [terminal]

DiscussionReport:      PENDING → RESOLVED | DISMISSED [terminal]
```

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All API endpoints respond within **2 seconds** (NFR-P01).
- File upload to Cloudinary completes within **10 seconds** for files up to 50 MB.
- Lecture list (paginated) loads within **1 second** for 100 lectures.
- Schema isolation enforced under `lrn` namespace.
- Activity log recorded within 2 seconds of lecture view/material download.

## Assumptions

- Target database is SQL Server; schema prefix is `lrn`.
- Cloudinary REST API used for file/media upload; credentials injected via environment variables.
- Gamification module (007) listens to `ActivityLoggedEvent` for streak tracking.
- Notification module (008) listens to `DiscussionQuestionPostedEvent` to notify Teachers.
- MediatR domain events used for cross-module communication.