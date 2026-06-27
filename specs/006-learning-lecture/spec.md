# Feature Specification: Learning & Lecture Module

**Feature Branch**: `006-learning-lecture`

**Created**: 2026-06-23 | **Updated**: 2026-06-27

**Status**: Approved

**Source Documents**: PRD §4 (FT-08, FT-09, FT-10), UCS UC-60–UC-80, TDS §3 (lectures, materials, discussions), current SQL database script

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor |
|-------|------|---------------|
| UC-60 | View Lecture List | Teacher |
| UC-61 | Create Lecture | Teacher |
| UC-62 | Update Lecture | Teacher |
| UC-63 | Publish Lecture | Teacher, Admin |
| UC-64 | Deactivate Lecture | Teacher, Admin |
| UC-65 | View Material List | Teacher |
| UC-66 | Upload Material | Teacher |
| UC-67 | Update Material | Teacher |
| UC-68 | Deactivate Material | Teacher |
| UC-69 | Attach Material to Lecture | Teacher |
| UC-70 | View Topic List | Student |
| UC-71 | View Lecture | Student |
| UC-72 | View Learning Material | Student |
| UC-73 | Ask Question on Lecture | Student |
| UC-74 | Answer Question on Lecture | Teacher |
| UC-75 | Moderate Discussion Report | Teacher, Admin |
| UC-76 | Report Discussion/Reply | Student |
| UC-77 | Update Comment/Reply | Student, Teacher |
| UC-78 | Delete Comment/Reply | Student, Teacher |
| UC-79 | Hide Discussion/Reply | Teacher, Admin |
| UC-80 | Like / Unlike Lecture | Student |

### Edge Cases

- **Expired lecture link**: Broken video URL returns a clear error and suggests alternative materials.
- **File size over limit**: Upload > 500 MB returns HTTP 413 with a limit explanation.
- **Blocked file format**: Upload `.exe`, `.bat`, `.sh`, or `.ps1` returns HTTP 415.
- **Ownership violation**: Teacher attempts to edit another Teacher's lecture returns HTTP 403.
- **Discussion report both null**: `DiscussionQuestionID` and `DiscussionAnswerID` are both null returns HTTP 422.
- **Discussion report both set**: both fields are non-null returns HTTP 422.
- **Duplicate lecture like**: Student likes the same lecture more than once creates no duplicate `LectureLike` row; API returns HTTP 409 or idempotent 200 based on final controller design.

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02**: Lectures and Materials are soft-deleted by setting `Status = 'Deactivated'` if referenced elsewhere. No hard-delete on published/used entities.
- **DC-06**: A `DiscussionReport` must have exactly one of `DiscussionQuestionID` or `DiscussionAnswerID` set as non-null. Both null or both non-null returns HTTP 422.
- **BR-25**: Maximum uploaded file size is **500 MB** for media materials.
- **BR-26**: Accepted file formats: **PDF**, **MP4**, **DOCX**. Blocked: executables (`.exe`, `.bat`, `.sh`, `.ps1`).
- **BR-31**: Teachers can only manage lectures or materials they created (`TeacherID = currentUserId`).
- **BR-32**: Lecture status lifecycle follows the current DB script: `Draft → Published → Deactivated`. `Deactivated` is terminal for lecture publishing.
- **BR-33**: Material status lifecycle follows the current DB script: `Active ↔ Deactivated`.
- **BR-34**: Lecture-Material relationship is **Many-to-Many**. A material can be attached to multiple lectures; a lecture can have multiple materials.
- **BR-35**: `DiscussionQuestion.Status` follows the current DB script: `Active`, `Hidden`, `Deleted`. Reporting a discussion creates a `DiscussionReport`; it does not set a `Reported` status because the DB does not support that value.
- **BR-36**: `DiscussionAnswer.Status` follows the current DB script: `Active`, `Hidden`, `Deleted`. Reporting an answer creates a `DiscussionReport`; it does not set a `Reported` status.
- **BR-37**: Student activity (UC-71: viewing a lecture, UC-72: downloading a material) must be logged to Gamification via `ActivityLoggedEvent`.
- **BR-38**: Teacher receives notification when a student posts a discussion question on their lecture (UC-73).
- **BR-39**: Students can like a published lecture at most once. `Lecture.Likes` stores the aggregate like count and must never be negative. `LectureLike` stores the unique `(LectureID, StudentID)` relationship used to prevent duplicate likes and support unlike.

### File Upload Rules

| Format | Max Size | Cloudinary | Notes |
|--------|----------|------------|-------|
| PDF | 500 MB | Yes | Downloadable by students |
| MP4 | 500 MB | Yes | Video streaming |
| DOCX | 500 MB | Yes | Document preview |
| `.exe`, `.bat`, `.sh`, `.ps1` | Blocked | No | HTTP 415 |

### Key Entities *(current DB script naming)*

- **Lecture**: `LectureID`, `Title`, `Content`, `VideoUrl`, `ThumbnailUrl`, `Likes`, `TeacherID`, `TagID`, `Status` (`Draft` | `Published` | `Deactivated`), `CreatedTime`, `UpdatedTime`
- **Material**: `MaterialID`, `MaterialName`, `FileUrl`, `FileType`, `TeacherID`, `Status` (`Active` | `Deactivated`), `UploadedTime`
- **LectureMaterial** *(junction, Many-to-Many)*: `LectureID`, `MaterialID`; composite PK `(LectureID, MaterialID)`
- **LectureLike** *(junction)*: `LectureID`, `StudentID`, `CreatedTime`; composite PK `(LectureID, StudentID)` prevents duplicate likes by the same Student.
- **DiscussionQuestion**: `DiscussionQuestionID`, `LectureID`, `StudentID`, `Title`, `Content`, `Status` (`Active` | `Hidden` | `Deleted`), `CreatedTime`, `UpdatedTime`
- **DiscussionAnswer**: `DiscussionAnswerID`, `DiscussionQuestionID`, `AccountID`, `Content`, `CreatedTime`, `Status` (`Active` | `Hidden` | `Deleted`), `UpdatedTime`
- **DiscussionReport**: `ReportID`, `DiscussionQuestionID` nullable, `DiscussionAnswerID` nullable, `ReporterAccountID`, `ReportReason`, `Status` (`Pending` | `Resolved` | `Dismissed`), `CreatedTime`, `ResolvedTime`, `ResolvedByAccountID` nullable

### State Machines

```text
Lecture:             Draft → Published → Deactivated
Material:            Active ↔ Deactivated

DiscussionQuestion:  Active → Hidden | Deleted
DiscussionAnswer:    Active → Hidden | Deleted

DiscussionReport:    Pending → Resolved | Dismissed
LectureLike:         absent → present → absent
```

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All API endpoints respond within **2 seconds**.
- File upload to Cloudinary completes within **10 seconds** for files up to 50 MB.
- Lecture list (paginated) loads within **1 second** for 100 lectures.
- Backend mapping follows the current DB script table and column names instead of creating a separate `lrn` schema.
- Activity log is recorded within 2 seconds of lecture view/material download.
- Duplicate lecture likes are prevented by database constraint and API validation.

## Assumptions

- Target database is SQL Server. Backend maps to the current DB script tables (`Lecture`, `Material`, `LectureMaterial`, `LectureLike`, etc.) instead of creating schema-prefixed tables.
- SQL script is the source of truth for MVP persistence. Do not add EF migrations unless the team explicitly changes this decision.
- Cloudinary REST API is used for file/media upload; credentials are injected via environment variables.
- Gamification module (007) listens to `ActivityLoggedEvent` for streak tracking.
- Notification module (008) listens to `DiscussionQuestionPostedEvent` and `DiscussionAnsweredEvent`.
- MediatR domain events are used for in-process cross-module communication.
