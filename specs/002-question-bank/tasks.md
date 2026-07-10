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
- [ ] Schema alignment check: verify SQL script allows `Question.Status = Pending` before implementing Admin re-review flow; verify `TagDifficulty.LevelValue` has a unique constraint; verify `Question.QuestionType` allows `Composite` and `QuestionPart` exists.
- [x] Seed: topic/difficulty levels and sample questions; `PENDING` sample rows are applied only when the DB allows `Question.Status = Pending`

---

## Phase 2: Core Domain Logic

- [ ] **Question Commands**:
  - [x] `CreateQuestionCommand` — validate non-empty sanitized content, >=1 Topic + >=1 Difficulty tag, answer constraints per type, `Composite` part constraints, set `status = APPROVED` for Expert creator (BR-55)
  - [x] `UpdateQuestionCommand` — capture `QuestionVersion` snapshot before save if current `status = APPROVED` (BR-54); validate constraints
  - [x] `ToggleQuestionActiveCommand` — check existing `TestQuestion` records (DC-02)
  - [x] `DeleteQuestionCommand` — hard-delete if no `TestQuestion` reference; otherwise return `QUESTION_IN_USE` / HTTP 409 without mutation (DC-02)
  - [ ] `AdminApproveQuestionCommand` — set `status = APPROVED`
  - [ ] `AdminRejectQuestionCommand` — set `status = REJECTED`; requires non-empty reject reason
- [ ] **Report Commands**:
  - [ ] `ReportQuestionCommand` — Student: create `QuestionReport`, do NOT change question status (BR-58); Expert: create report, temporarily hide question from generation
  - [ ] `ResolveReportCommand` — Expert resolves own question report; restore visibility (BR-60)
  - [ ] Validate Teacher cannot report (BR-59) -> 403
- [x] **Tag Commands**:
  - [x] `CreateTagTopicCommand` — assign `parent_tag_id`, validate grade
  - [x] `CreateTagDifficultyCommand` — validate UNIQUE `level_value`; for MVP seed/accept normal levels `1..4` so Recommender `RecommendedDifficultyLevel` can resolve deterministically
  - [x] `UpdateTagCommand` — keep topic structure and difficulty `level_value` immutable after creation
  - [x] `DeleteTagCommand` — soft-delete taxonomy tags (`is_active = false`) to avoid FK conflicts; topic delete/deactivation is blocked when an active descendant exists (DC-02, BR-66)
- [ ] **Queries**:
  - [ ] `GetDashboardQuery` — count by `status`, `question_type`, `grade`; return last 5 reports
  - [x] `GetQuestionListQuery` — paged (pageSize, pageIndex), filter by `status`, `grade`, `tag_id`, `difficulty_id`, `question_type`, `expert_id`
  - [x] `GetQuestionVersionsQuery` — ordered by `created_time` DESC
  - [ ] `GetReportedQuestionsQuery` — filter by `reporter_account_id` = current Expert's questions
  - [x] `GetTagListQuery`/tag queries — return hierarchical topic tree + flat difficulty list; active-only by default and support `includeInactive=true` (BR-65)
- [ ] **Cross-module read contract**:
  - [ ] Provide/query by `QuestionTopic.TagID` + `Question.DifficultyID` + optional `Question.QuestionType` for TestGen.
  - [ ] For `Composite` questions, provide `QuestionPart` rows to Testing/Grading, but hide `correct_*` answer-key fields from student-facing test payloads before grading.
  - [ ] Ensure `TagDifficulty.LevelValue` can resolve Recommender v2 `RecommendedDifficultyLevel` to a concrete `DifficultyID`.
  - [ ] Do not couple QuestionBank to `TagsMastery`; QuestionBank only exposes question/topic/difficulty data.
- [ ] **File Parsers**:
  - [ ] `ExcelQuestionParser` — parse .xlsx using EPPlus; return list of `QuestionCreationDTO`
  - [ ] `WordQuestionParser` — parse .docx using OpenXml SDK; return list of `QuestionCreationDTO`
  - [ ] Validate each parsed question against BR-05, BR-50, BR-61, BR-62, BR-64
- [ ] **MassTransit Consumer**: `BulkImportConsumer` — reads from `excel_import_queue`, calls `CreateQuestionCommand` per item, publishes `BulkImportCompletedEvent`
- [ ] **Domain Events**: `QuestionReportedEvent`, `QuestionApprovedEvent`

---

## Phase 3: Controller and Routing

- [ ] `QuestionsController` — ExpertOnly + AdminOnly hybrid routes
- [x] `TagsController` — ExpertOnly routes for topic/difficulty CRUD
- [ ] `ReportsController` — Mixed: Student (POST report), Expert (GET + resolve), Admin (GET all + approve/reject)
- [ ] Image upload helper endpoint -> Cloudinary REST client, return `picture_url`
- [ ] Register all inside `QuestionBankModuleExtensions.cs`:
  - DbContext, MediatR handlers, Parsers, MassTransit consumer

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
  - [ ] UC-25: Update APPROVED question -> QuestionVersion created before update (BR-54)
  - [x] UC-27: Delete/deactivate question used in `TestQuestion` -> `QUESTION_IN_USE` / 409 with no data mutation (DC-02)
  - [ ] UC-28: Student report -> QuestionReport created, question status unchanged (BR-58)
  - [ ] UC-28: Teacher attempts to report -> 403 (BR-59)
  - [ ] UC-31/32: Admin approve/reject -> status transitions correct
  - [ ] UC-23: Import 10 questions from Excel -> all created, invalid rows rejected
  - [ ] UC-38: Delete tag with linked questions -> soft-delete (`is_active = false`) (DC-02)
  - [x] UC-38: Disable/delete topic with active descendant -> `TAG_TOPIC_HAS_ACTIVE_DESCENDANTS` / 409 with no data mutation (BR-66)
  - [x] UC-34: Tag queries exclude inactive records by default and return them with `includeInactive=true` (BR-65)
  - [ ] Recommender/TestGen contract: `RecommendedDifficultyLevel = 2` resolves to `TagDifficulty.LevelValue = 2` and returns matching `Question.DifficultyID`
  - [ ] TestGen query contract: approved active questions can be filtered by `QuestionTopic.TagID`, `Question.DifficultyID`, and section `Question.QuestionType`
