# Feature Specification: Lectures, Materials & Discussions

**Feature Branch**: `[specs/006-learning-lecture]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Publish Lectures & Materials (Priority: P1)
As a Teacher, I want to create theoretical lectures, upload video lessons, and attach PDF/Word study files so that students can study them.
**Why this priority**: Essential for content generation. Teachers must be able to publish learning resources to the platform.
**Independent Test**: Uploading a PDF and creating a lecture links them, and students can view and download the PDF.
**Acceptance Scenarios**:
1. **Given** a Teacher is logged in, **When** they fill in the lecture title, select a Topic Tag, and click 'Publish', **Then** the lecture becomes visible to students.
2. **Given** a Teacher uploads a PDF material, **When** they edit a lecture, **Then** they can link the material, making it downloadable on the lecture player page (UC-69).
3. **Given** a Teacher archives a lecture, **When** Student browses, **Then** the archived lecture is hidden (UC-64).

---

### User Story 2 - Study Lectures and Materials (Priority: P1)
As a Student or Guest, I want to browse topics, watch lectures, and download study PDFs so that I can learn mathematical theories.
**Why this priority**: Core student learning journey. Guests should also be allowed to preview content to encourage registration.
**Independent Test**: Student opens a lecture, starts the video, and triggers a learning log entry. Guest can only preview the first 5 minutes.
**Acceptance Scenarios**:
1. **Given** a Student selects a lecture, **When** they load the player, **Then** they can watch the full video, download attached files, and ask questions.
2. **Given** a Guest opens a lecture, **When** they watch, **Then** the video playback is stopped at exactly 5 minutes, prompting them to register (SCR-19).

---

### User Story 3 - Lecture Q&A Discussions (Priority: P2)
As a Student, I want to ask questions on a lecture page, and as a Teacher, I want to reply to them so that we can resolve learning doubts.
**Why this priority**: Enhances the interactive learning experience. Resolves confusing theoretical parts through direct teacher-student Q&A.
**Independent Test**: Student posts a question, which appears below the lecture. Teacher replies, which displays nested under the question.
**Acceptance Scenarios**:
1. **Given** Student is on a lecture page, **When** they submit a discussion question, **Then** it appears in real-time in the Q&A thread.
2. **Given** a Teacher is on their dashboard, **When** they view new lecture questions, **Then** they can write a reply, which resolves the student's doubt.
3. **Given** an inappropriate comment is posted, **When** Teacher or Admin clicks 'Hide', **Then** the comment is hidden from students immediately (UC-79).

### Edge Cases

- **File Size/Format Verification**: Materials uploads must reject files larger than 300MB or unsupported formats (only PDF, DOCX, MP4 allowed) (GB-05, TDS §9).
- **Soft Deleting Lectures**: Lectures cannot be hard-deleted if referenced in active recommendation pathways or study logs; they must be soft-deleted using status flags (DC-02, GB-04).
- **Report Target Check**: When reporting a discussion item, the report must point to either a Question or an Answer, but never both or neither (DC-06).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: Teachers MUST be able to create, update, publish, and archive lectures (UC-60 to UC-64).
- **FR-002**: Teachers MUST be able to upload study materials and link them to lectures (UC-65 to UC-69).
- **FR-003**: System MUST enforce upload size limits (up to 300MB) and formats (PDF, DOCX, MP4) (GB-05, TDS §9).
- **FR-004**: Students MUST be able to browse topics, view published lectures, and download attached materials (UC-70 to UC-72).
- **FR-005**: Guests MUST be restricted to a 5-minute preview of lectures and blocked from downloads (SCR-19).
- **FR-006**: Students MUST be able to post questions and answers in a nested Q&A discussion board below lectures (UC-73, UC-74, UC-77, UC-78).
- **FR-007**: Teachers and Admins MUST be able to moderate, hide, or delete inappropriate discussion posts (UC-75, UC-76, UC-79).
- **FR-008**: System MUST validate that discussion reports target either a question or an answer, but not both (DC-06).

### Key Entities
- **Lecture**: Title, video url, topic tag, status (Draft, Published, Archived), and teacher ID.
- **Material**: Title, file url, file type, file size, status, and linked lecture ID.
- **DiscussionQuestion & DiscussionAnswer**: Q&A comments posted under lectures.
- **DiscussionReport**: Flagged comments awaiting moderation.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Uploading a valid material file under 50MB must complete within 5 seconds.
- **SC-002**: Moderation actions (hiding a comment) must reflect on the screen in under 500ms.
- **SC-003**: Lecture player pages must load the video stream and attachment details in under 2 seconds.

## Assumptions

- Video streaming uses a standard HTML5 player compatible with MP4.
- High-bandwidth storage (e.g., S3 or local network storage) is available for hosting video files.
