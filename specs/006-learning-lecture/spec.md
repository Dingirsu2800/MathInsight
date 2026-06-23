# Feature Specification: Learning & Lecture Module

**Feature Branch**: `006-learning-lecture`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Manages theoretical video lectures, downloadable slide/PDF files, student topic directories and discussion comment boards (moderate, flag, report)."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-60: View Lecture List**
- **UC-61: Create Lecture**
- **UC-62: Update Lecture**
- **UC-63: Publish Lecture**
- **UC-64: Archive Lecture**
- **UC-65: View Material List**
- **UC-66: Upload Material**
- **UC-67: Update Material**
- **UC-68: Archive Material**
- **UC-69: Attach Material to Lecture**
- **UC-70: View Topic List**
- **UC-71: View Lecture**
- **UC-72: View Learning Material**
- **UC-73: Ask Question on Lecture**
- **UC-74: Answer Question on Lecture**
- **UC-75: Moderate Discussion**
- **UC-76: Report Discussion/Reply**
- **UC-77: Update Comment/Reply**
- **UC-78: Delete Comment/Reply**
- **UC-79: Hide Discussion/Reply**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02: Lectures and Materials are soft-deleted (status toggle to ARCHIVED/INACTIVE) if referenced elsewhere.**
- **DC-06: Discussion report targets must have exactly one of DiscussionQuestionID or DiscussionAnswerID set as non-null.**
- **BR-25: Maximum uploaded file size is 500 MB for media materials.**
- **BR-26: Accepted learning material formats: PDF, MP4, DOCX. Blocked formats: executables (.exe, .bat).**
- **Teachers can only manage (edit, publish, archive) lectures or materials that they created.**

### Key Entities *(include if feature involves data)*

- **Lecture**:  lecture_id, title, content, video_url, thumbnail_url, teacher_id (FK), tag_id (FK), status (DRAFT, PUBLISHED, ARCHIVED), created_time, updated_time
- **Material**:  material_id, material_name, file_url, file_type, teacher_id (FK), status (ACTIVE, INACTIVE), uploaded_time
- **DiscussionQuestion**:  discussion_question_id, lecture_id (FK), student_id (FK), title, content, status (ACTIVE, REPORTED, HIDDEN, SOLVED), created_time, updated_time
- **DiscussionAnswer**:  discussion_answer_id, discussion_question_id (FK), account_id (FK), content, created_time, status (ACTIVE, REPORTED, HIDDEN), updated_time
- **DiscussionReport**:  report_id, discussion_question_id (FK, nullable), discussion_answer_id (FK, nullable), reporter_account_id (FK), report_reason, status (PENDING, RESOLVED, DISMISSED), created_time, resolved_time, resolver_account_id (FK, nullable)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `lrn` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.