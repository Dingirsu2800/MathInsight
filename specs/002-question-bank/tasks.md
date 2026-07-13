# Tasks Checklist: Question Bank Module

**Branch**: `002-question-bank` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [x] Create EF `IEntityTypeConfiguration` for all 8 Question Bank entities mapped to current DB script tables:
  - [x] `QuestionConfiguration` — composite index `(status, is_active)`, FK to `tag_difficulties`; map API question type enums to DB values (`SingleChoice`, `MultipleChoice`, `TrueFalse`, `ShortAnswer`, `Composite`)
  - [x] `AnswerConfiguration` — FK to `questions`, validate `is_correct`
  - [x] `QuestionPartConfiguration` — FK to `questions`, UNIQUE `(question_id, part_order)`, filtered UNIQUE `(question_id, part_label)` when label is not null; map `PartType` values (`TrueFalse`, `ShortAnswer`, `NumericAnswer`); keep `correct_*` fields out of student-facing DTOs
  - [x] `QuestionVersionConfiguration` — FK to `questions`, `experts`; `answers_snapshot` as TEXT/JSON
  - [x] `QuestionReportConfiguration` — `reporter_role` enum constraint
  - [x] `TagTopicConfiguration` — self-referencing FK `parent_tag_id`; UNIQUE `tag_name`
  - [x] `TagDifficultyConfiguration` — UNIQUE `difficulty_name`, UNIQUE `level_value`; keep `level_value` stable for Recommender/TestGen v2 `RecommendedDifficultyLevel` mapping
  - [x] `QuestionTopicConfiguration` — composite UNIQUE `(question_id, tag_id)`, `is_primary` flag
- [x] Create `QuestionBankDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [x] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [x] Schema alignment check: Admin re-review state is stored in `QuestionReport`; migration `003` adds review audit fields/statuses and `TagDifficulty.LevelValue` unique index; verify `Question.QuestionType` allows `Composite` and `QuestionPart` exists.
- [x] Seed: topic/difficulty levels and sample questions; Admin re-review state is represented by `QuestionReport.PendingFix` / `PendingReview`, never `Question.Status = Pending`

---

## Phase 2: Core Domain Logic

- [ ] **Question Commands**:
  - [x] `CreateQuestionCommand` — validate non-empty sanitized content, >=1 Topic + >=1 Difficulty tag, answer constraints per type, `Composite` part constraints, set `status = APPROVED` for Expert creator (BR-55)
  - [x] `UpdateQuestionCommand` — capture `QuestionVersion` snapshot before save if current `status = APPROVED` or `REPORTED` (BR-54); validate constraints
  - [x] `ToggleQuestionActiveCommand` — check existing `TestQuestion` records (DC-02)
  - [x] `DeleteQuestionCommand` — hard-delete only when there is no `TestQuestion` reference, pending report, or `REPORTED` status; otherwise return the matching HTTP 409 without mutation (DC-02, BR-69)
  - [x] `SubmitQuestionReportReviewCommand` — owning Expert submits an Admin `PendingFix` report as `PendingReview`
  - [x] `AdminApproveQuestionReportCommand` / `AdminRejectQuestionReportCommand` — only the original Admin reporter reviews `PendingReview`; approve restores `Approved` only when no Expert `Pending` report remains, reject requires review note and sets `Rejected`
- [x] `ExtractQuestionOcrDraftCommand` - validates one JPEG/PNG/WebP image and returns an unpersisted Mistral OCR draft with untrusted answer suggestions and up to three user-selectable visual candidates (BR-72)
- [x] **Report Commands**:
  - [x] `ReportQuestionCommand` — Student/Expert reports create `Pending`; Admin creates `PendingFix`; one active Admin workflow per Question is enforced under the SQL Server question lock (BR-58, BR-67, BR-71)
  - [x] `HandleQuestionReportCommand` — Question owner resolves or dismisses only Student/Expert `Pending` reports; Admin reports require submit/review flow; restoration accounts for active Admin workflow (BR-60, BR-68)
  - [x] Validate Teacher cannot report (BR-59) -> 403
- [x] **Tag Commands**:
  - [x] `CreateTagTopicCommand` — assign `parent_tag_id`, validate grade
  - [x] `CreateTagDifficultyCommand` — validate UNIQUE `level_value`; for MVP seed/accept normal levels `1..4` so Recommender `RecommendedDifficultyLevel` can resolve deterministically
  - [x] `UpdateTagCommand` — keep topic structure and difficulty `level_value` immutable after creation
  - [x] `DeleteTagCommand` — soft-delete taxonomy tags (`is_active = false`) to avoid FK conflicts; topic delete/deactivation is blocked when an active descendant exists (DC-02, BR-66)
- [ ] **Queries**:
  - [ ] `GetDashboardQuery` — count by `status`, `question_type`, `grade`; return last 5 reports
  - [x] `GetQuestionListQuery` — paged (pageSize, pageIndex), filter by `status`, `grade`, `tag_id`, `difficulty_id`, `question_type`, `expert_id`
  - [x] `GetQuestionVersionsQuery` — ordered by `created_time` DESC
  - [x] `GetOwnedReportedQuestionsQuery` / `GetQuestionReportsQuery` — owner-only reported-question list (grouped/paged) and report detail ordered by newest first, including `PendingFix`/`PendingReview`
  - [x] `GetAdminQuestionReportsQuery` — paged Admin-reporter-only workflow query for `PendingFix`, `PendingReview`, or `Resolved`
  - [x] `GetTagListQuery`/tag queries — return hierarchical topic tree + flat difficulty list; active-only by default and support `includeInactive=true` (BR-65)
- [ ] **Cross-module read contract**:
  - [ ] Provide/query by `QuestionTopic.TagID` + `Question.DifficultyID` + optional `Question.QuestionType` for TestGen.
  - [ ] For `Composite` questions, provide `QuestionPart` rows to Testing/Grading, but hide `correct_*` answer-key fields from student-facing test payloads before grading.
  - [ ] Ensure `TagDifficulty.LevelValue` can resolve Recommender v2 `RecommendedDifficultyLevel` to a concrete `DifficultyID`.
  - [ ] Do not couple QuestionBank to `TagsMastery`; QuestionBank only exposes question/topic/difficulty data.
- [x] **Excel Import MVP (UC-23)**:
  - [x] ClosedXML template download, `.xlsx` parser, file/type/size/version/formula validation
  - [x] Expert-only Preview -> Confirm API; Preview has no database write and Confirm revalidates then saves atomically
  - [x] Resolve active topic/difficulty taxonomy and validate BR-05, BR-50, BR-61, BR-62, BR-64
  - [x] Frontend API handoff document with stable errors and JSON contract
- [ ] **Post-MVP import backlog**: Word/OpenXml parser, persistent import batches, queue/notification events, idempotency across process restarts
- [ ] **Domain Events**: `QuestionReportedEvent`, `QuestionApprovedEvent`

---

## Phase 3: Controller and Routing

- [ ] `QuestionsController` — ExpertOnly + AdminOnly hybrid routes
- [x] `TagsController` — ExpertOnly routes for topic/difficulty CRUD
- [x] `ReportsController` — Student/Expert/Admin POST report; Expert owner GET report list/detail and PATCH resolve/dismiss
- [x] Admin report workflow APIs: Expert submit-review; original Admin reporter approve/reject; Admin-reporter-only dashboard query
- [x] Frontend handoff: `status=Pending` aggregates active report states for Expert inbox; document Admin submit/review API contracts and stable errors
- [x] Expert frontend report workflow: distinguish `Pending`, `PendingFix`, and `PendingReview` in reported-question list and editor; integrate submit-review; preserve regular resolve/dismiss for Student/Expert reports
- [ ] Admin frontend report dashboard and approve/reject UI
- [x] Image upload helper endpoint -> authenticated Cloudinary REST client using server-side HTTP Basic authentication, return `picture_url`; validate JPEG/PNG/WebP magic bytes and 5 MB limit; OCR/Pix2Text remains a separate backlog checkpoint
- [x] OCR draft endpoint -> Expert-only Mistral OCR client, one JPEG/PNG/WebP question image, 5 MB magic-byte validation, 10 requests/minute per Expert, no database write or automatic answer confirmation (BR-72)
- [x] Register QuestionBank import services inside `QuestionBankModuleExtensions.cs`:
  - ClosedXML parser, template service, validation service, MediatR handlers

---

## Phase 4: Verification

- [x] `dotnet build` — zero compile errors for `MathInsight.Modules.QuestionBank`
- [ ] Integration tests (xUnit):
  - [ ] UC-20/21: Create SINGLE_CHOICE -> `status = APPROVED`, 4 answers, 1 correct
  - [ ] UC-21: Create with 0 Topic tags -> 400 (BR-05)
  - [ ] UC-21: Create TRUE_FALSE with 3 answers -> 400 (BR-62)
  - [ ] UC-21: Create COMPOSITE with 4 TrueFalse parts (`a`-`d`) -> `QuestionPart` rows created; student-facing payload hides `CorrectBoolean`, `CorrectText`, `CorrectNumeric`, `NumericTolerance`
  - [ ] UC-21: Create COMPOSITE with a NumericAnswer part and tolerance -> grading can compare within tolerance
  - [ ] UC-21: API enum `TRUE_FALSE` persists as DB value `TrueFalse`; `MULTIPLE_SELECT` persists as `MultipleChoice`; `COMPOSITE` persists as `Composite`
  - [ ] UC-21: Create SHORT_ANSWER with rich-text/image content in correct field -> 400 (BR-61)
  - [x] UC-25: Update APPROVED or REPORTED question -> QuestionVersion created before update (BR-54)
  - [x] UC-27: Delete/deactivate question used in `TestQuestion` -> `QUESTION_IN_USE` / 409 with no data mutation (DC-02)
  - [x] UC-28: Student report -> QuestionReport created, question status unchanged (BR-58)
  - [x] UC-28: Teacher attempts to report -> 403 (BR-59)
  - [x] UC-29/30/33: Expert/Admin report, owner-only report queries, resolve/dismiss state transitions, and HTTP error mapping
  - [ ] Manual SQL Server smoke: verify `QuestionReportSqlServerLock` serializes report mutations for the same QuestionID in a disposable test database
  - [x] UC-31/32: Admin workflow submit, approve/reject, ownership, active-workflow and question-status transitions correct
  - [x] UC-23: Excel template, preview without writes, invalid formula/inactive taxonomy rejection, and atomic confirm are covered by automated tests
  - [ ] UC-38: Delete tag with linked questions -> soft-delete (`is_active = false`) (DC-02)
  - [x] UC-38: Disable/delete topic with active descendant -> `TAG_TOPIC_HAS_ACTIVE_DESCENDANTS` / 409 with no data mutation (BR-66)
  - [x] UC-34: Tag queries exclude inactive records by default and return them with `includeInactive=true` (BR-65)
  - [ ] Recommender/TestGen contract: `RecommendedDifficultyLevel = 2` resolves to `TagDifficulty.LevelValue = 2` and returns matching `Question.DifficultyID`
  - [ ] TestGen query contract: approved active questions can be filtered by `QuestionTopic.TagID`, `Question.DifficultyID`, and section `Question.QuestionType`
