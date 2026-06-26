# Tasks Checklist: Learning & Lecture Module

**Branch**: `006-learning-lecture` | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for all 6 entities under `lrn` schema:
  - [ ] `LectureConfiguration` — `tag_id` FK (from qnb), `teacher_id` FK; status enum; index `(status, tag_id)`
  - [ ] `MaterialConfiguration` — `teacher_id` FK; `file_type` enum (PDF/MP4/DOCX); status enum
  - [ ] `LectureMaterialConfiguration` — composite PK `(lecture_id, material_id)`; FK to lectures + materials
  - [ ] `DiscussionQuestionConfiguration` — `lecture_id` FK; `student_id` FK; status enum
  - [ ] `DiscussionAnswerConfiguration` — `discussion_question_id` FK; `account_id` FK (any role); status enum
  - [ ] `DiscussionReportConfiguration` — exactly one of `question_id`/`answer_id` non-null (CHECK constraint, DC-06); status enum
- [ ] Create `LearningDbContext.cs` with shared connection, `lrn` schema default
- [ ] Add EF migration: `dotnet ef migrations add Init_Learning --project MathInsight.WebAPI`
- [ ] Seed: 2 lectures (1 Draft, 1 Published), materials, per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **Lecture Commands**:
  - [ ] `CreateLectureCommand` — validate `tag_id` exists, `teacher_id` = current user, default `status = DRAFT`
  - [ ] `UpdateLectureCommand` — validate ownership (BR-31); reject if `status = ARCHIVED`
  - [ ] `PublishLectureCommand` — DRAFT → PUBLISHED; validate `video_url` or `content` is set
  - [ ] `ArchiveLectureCommand` — PUBLISHED → ARCHIVED (no revert); validate ownership (BR-31)

- [ ] **Material Commands**:
  - [ ] `UploadMaterialCommand`:
    - Validate file format: PDF, MP4, DOCX only (BR-26) → 415 otherwise
    - Validate file size ≤ 500 MB (BR-25) → 413 otherwise
    - Upload to Cloudinary via `CloudinaryService.UploadAsync()`
    - Save `Material` record with returned `file_url`
  - [ ] `UpdateMaterialCommand` — validate ownership (BR-31)
  - [ ] `ArchiveMaterialCommand` — ACTIVE → INACTIVE
  - [ ] `AttachMaterialToLectureCommand` — UC-69: validate ownership of both; insert `LectureMaterial` (M:N)

- [ ] **Discussion Commands**:
  - [ ] `AskDiscussionQuestionCommand` (UC-73) — validate `lecture_id` is PUBLISHED; publish `DiscussionQuestionPostedEvent`
  - [ ] `AnswerDiscussionQuestionCommand` (UC-74) — publish `DiscussionAnsweredEvent`
  - [ ] `UpdateDiscussionCommentCommand` (UC-77) — validate `account_id = currentUserId` OR Teacher role
  - [ ] `DeleteDiscussionCommentCommand` (UC-78) — same ownership check
  - [ ] `HideDiscussionCommentCommand` (UC-79) — Teacher/Admin only; set `status = HIDDEN`
  - [ ] `ReportDiscussionCommand` (UC-76):
    - Validate DC-06: exactly one of `discussion_question_id` or `discussion_answer_id` non-null → 422
    - Create `DiscussionReport` with `status = PENDING`
    - Update target status to `REPORTED`
  - [ ] `ResolveModerationCommand` (UC-75) — Teacher/Admin: PENDING → RESOLVED or DISMISSED

- [ ] **Queries**:
  - [ ] `GetLectureListQuery` — paged; Teacher sees own lectures; Students see PUBLISHED only
  - [ ] `GetLectureQuery` — includes `materials` (via junction), `discussion_count`; logs `ActivityLoggedEvent`
  - [ ] `GetMaterialListQuery` — paged; Teacher sees own; Students see ACTIVE only via joined lectures
  - [ ] `GetTopicListQuery` (UC-70) — cross-read `qnb.tag_topics` hierarchical tree
  - [ ] `GetDiscussionsQuery` — paginated questions + answers for a lecture
  - [ ] `GetModerationQueueQuery` — pending reports for Teacher's lectures or all for Admin

- [ ] **CloudinaryService**: upload file bytes → Cloudinary HTTPS REST → return `secure_url`
- [ ] **Activity Events**: publish `ActivityLoggedEvent` on lecture view (UC-71) and material download (UC-72)

---

## Phase 3: Controller and Routing

- [ ] `LecturesController` — Teacher CRUD; Student read-only (PUBLISHED only)
- [ ] `MaterialsController` — Teacher CRUD; Student download
- [ ] `DiscussionsController` — Mixed roles per endpoint
- [ ] Apply `[Authorize]` and ownership check middleware/service
- [ ] Register inside `LearningLectureModuleExtensions.cs`:
  - DbContext, Cloudinary, MediatR handlers, all domain events

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-61: Create lecture → `status = DRAFT`
  - [ ] UC-63: Publish → `status = PUBLISHED`
  - [ ] UC-64: Archive PUBLISHED → `status = ARCHIVED`
  - [ ] UC-62: Teacher edits another Teacher's lecture → 403 (BR-31)
  - [ ] UC-66: Upload .exe → 415; upload 600MB → 413; valid PDF → URL stored
  - [ ] UC-69: Attach material to lecture → `lecture_materials` row created (M:N)
  - [ ] UC-73: Post question on DRAFT lecture → 422 (must be PUBLISHED)
  - [ ] UC-73: Valid question post → `DiscussionQuestionPostedEvent` published
  - [ ] UC-76: Report both IDs set → 422 (DC-06)
  - [ ] UC-76: Valid report → `DiscussionQuestion.status = REPORTED`
  - [ ] UC-79: Teacher hides → `status = HIDDEN` (cannot revert)
  - [ ] UC-71: View lecture → `ActivityLoggedEvent` with `activity_type = VIEW_LECTURE`
  - [ ] UC-72: Download material → `ActivityLoggedEvent` with `activity_type = DOWNLOAD_MATERIAL`