# Tasks Checklist: Learning & Lecture Module

**Branch**: `006-learning-lecture` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] Create EF `IEntityTypeConfiguration` for all Learning entities mapped to the current DB script table and column names:
  - [x] `LectureConfiguration` — map to `Lecture`; include `Likes`; `TagID` FK, `TeacherID` FK; check DB values `Draft`, `Published`, `Deactivated`; index `(Status, TagID)`.
  - [x] `LectureLikeConfiguration` — map to `LectureLike`; composite PK `(LectureID, StudentID)`; FK to `Lecture` + `Student`.
  - [x] `MaterialConfiguration` — map to `Material`; `TeacherID` FK; `FileType`; check DB values `Active`, `Deactivated`.
  - [x] `LectureMaterialConfiguration` — map to `LectureMaterial`; composite PK `(LectureID, MaterialID)`.
  - [x] `DiscussionQuestionConfiguration` — map to `DiscussionQuestion`; `LectureID` FK; `StudentID` FK; check DB values `Active`, `Hidden`, `Deleted`.
  - [x] `DiscussionAnswerConfiguration` — map to `DiscussionAnswer`; `DiscussionQuestionID` FK; `AccountID` FK; check DB values `Active`, `Hidden`, `Deleted`.
  - [x] `DiscussionReportConfiguration` — map to `DiscussionReport`; exactly one of `DiscussionQuestionID`/`DiscussionAnswerID` non-null; check DB values `Pending`, `Resolved`, `Dismissed`.
- [x] Create `LearningDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [x] Seed only through SQL script or an approved seed strategy; do not create conflicting EF seed migrations.

---

## Phase 2: Core Domain Logic

- [x] **Lecture Commands**:
  - [x] `CreateLectureCommand` — validate `TagID` exists, `TeacherID = currentUserId`, default `Status = 'Draft'`.
  - [x] `UpdateLectureCommand` — validate ownership; reject if `Status = 'Deactivated'`.
  - [x] `PublishLectureCommand` — `Draft → Published`; validate `VideoUrl` or `Content` is set.
  - [x] `DeactivateLectureCommand` — `Published → Deactivated`; validate ownership.
  - [x] `LikeLectureCommand` — Student only; validate lecture is `Published`; insert `LectureLike`; increment `Lecture.Likes`; prevent duplicate `(LectureID, StudentID)`.
  - [x] `UnlikeLectureCommand` — Student only; remove own `LectureLike`; decrement `Lecture.Likes` but never below 0.

- [x] **Material Commands**:
  - [x] `UploadMaterialCommand`:
    - Validate file format: PDF, MP4, DOCX only.
    - Validate file size ≤ 500 MB.
    - Upload to Cloudinary via `CloudinaryService.UploadAsync()`.
    - Save `Material` with returned `FileUrl`.
  - [x] `UpdateMaterialCommand` — validate ownership.
  - [x] `DeactivateMaterialCommand` — set `Status = 'Deactivated'`.
  - [x] `AttachMaterialToLectureCommand` — validate ownership of both; insert `LectureMaterial`.

- [x] **Discussion Commands**:
  - [x] `AskDiscussionQuestionCommand` — validate lecture is `Published`; insert `DiscussionQuestion` with `Status = 'Active'`; publish `DiscussionQuestionPostedEvent`.
  - [x] `AnswerDiscussionQuestionCommand` — insert `DiscussionAnswer` with `Status = 'Active'`; publish `DiscussionAnsweredEvent`.
  - [x] `UpdateDiscussionCommentCommand` — validate `AccountID = currentUserId` or Teacher/Admin permission.
  - [x] `DeleteDiscussionCommentCommand` — set target question/answer `Status = 'Deleted'`.
  - [x] `HideDiscussionCommentCommand` — Teacher/Admin only; set target question/answer `Status = 'Hidden'`.
  - [x] `ReportDiscussionCommand`:
    - Validate DC-06: exactly one of `DiscussionQuestionID` or `DiscussionAnswerID` non-null.
    - Create `DiscussionReport` with `Status = 'Pending'`.
    - Do not update target question/answer status because DB has no `Reported` value.
  - [x] `ResolveModerationCommand` — Teacher/Admin: `Pending → Resolved` or `Pending → Dismissed`.

- [x] **Queries**:
  - [x] `GetLectureListQuery` — paged; Teacher sees own lectures; Students see `Published` only.
  - [x] `GetLectureQuery` — includes materials via `LectureMaterial`, like count, discussion count; logs `ActivityLoggedEvent`.
  - [x] `GetMaterialListQuery` — paged; Teacher sees own materials; Students see active materials only through published lectures.
  - [x] `GetTopicListQuery` — read `TagTopic` hierarchical tree.
  - [x] `GetDiscussionsQuery` — paginated questions + answers for a lecture; exclude `Deleted`, hide `Hidden` from Students.
  - [x] `GetModerationQueueQuery` — pending `DiscussionReport` records for Teacher's lectures or all for Admin.

- [x] **CloudinaryService**: upload file bytes to Cloudinary and return HTTPS `secure_url`.
- [x] **Activity Events**: publish `ActivityLoggedEvent` on lecture view and material download.

---

## Phase 3: Controller and Routing

- [x] `LecturesController` — Teacher CRUD; Student read-only for `Published` lectures.
  - [x] `POST /api/v1/lectures/{id}/like`
  - [x] `DELETE /api/v1/lectures/{id}/like`
- [x] `MaterialsController` — Teacher CRUD; Student download.
- [x] `DiscussionsController` — mixed roles per endpoint.
- [x] Apply `[Authorize]` and ownership checks.
- [x] Register inside `LearningModuleExtensions.cs`:
  - DbContext, Cloudinary service, MediatR handlers, domain events.

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors.
- [ ] Integration tests or manual API checks:
  - [ ] UC-61: Create lecture → `Status = 'Draft'`.
  - [ ] UC-63: Publish → `Status = 'Published'`.
  - [ ] UC-64: Deactivate → `Status = 'Deactivated'`.
  - [ ] UC-62: Teacher edits another Teacher's lecture → 403.
  - [ ] UC-66: Upload `.exe` → 415; upload > 500MB → 413; valid PDF → URL stored.
  - [ ] UC-69: Attach material to lecture → `LectureMaterial` row created.
  - [ ] UC-80: Student likes a `Published` lecture → `LectureLike` row created and `Lecture.Likes` increments by 1.
  - [ ] UC-80: Same Student likes same lecture again → no duplicate `LectureLike`.
  - [ ] UC-80: Student unlikes lecture → own `LectureLike` row removed and `Lecture.Likes` decrements by 1.
  - [ ] UC-73: Post question on non-`Published` lecture → 422.
  - [ ] UC-73: Valid question post → `DiscussionQuestionPostedEvent` published.
  - [ ] UC-76: Report both IDs set or both null → 422.
  - [ ] UC-76: Valid report → `DiscussionReport.Status = 'Pending'`; target question/answer status unchanged.
  - [ ] UC-79: Teacher hides → target `Status = 'Hidden'`.
  - [ ] UC-78: Delete → target `Status = 'Deleted'`.
  - [ ] UC-71: View lecture → `ActivityLoggedEvent` with `ActivityType = 'VIEW_LECTURE'`.
  - [ ] UC-72: Download material → `ActivityLoggedEvent` with `ActivityType = 'DOWNLOAD_MATERIAL'`.
