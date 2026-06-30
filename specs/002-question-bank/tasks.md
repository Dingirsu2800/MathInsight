# Tasks Checklist: Question Bank Module

**Branch**: `002-question-bank` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for all 7 entities mapped to current DB script tables:
  - [ ] `QuestionConfiguration` — composite index `(status, grade)`, FK to `experts`, `tag_difficulties`
  - [ ] `AnswerConfiguration` — FK to `questions`, validate `is_correct`
  - [ ] `QuestionVersionConfiguration` — FK to `questions`, `experts`; `answers_snapshot` as TEXT/JSON
  - [ ] `QuestionReportConfiguration` — `reporter_role` enum constraint
  - [ ] `TagTopicConfiguration` — self-referencing FK `parent_tag_id`; UNIQUE `tag_name`
  - [ ] `TagDifficultyConfiguration` — UNIQUE `difficulty_name`, UNIQUE `level_value`
  - [ ] `QuestionTopicConfiguration` — composite UNIQUE `(question_id, tag_id)`, `is_primary` flag
- [ ] Create `QuestionBankDbContext.cs` with shared connection and explicit `ToTable(...)` mappings.
- [ ] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 3 topic tags, 3 difficulty levels, 5 questions (3 APPROVED, 2 PENDING) per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **Question Commands**:
  - [ ] `CreateQuestionCommand` — validate non-empty sanitized content, ≥1 Topic + ≥1 Difficulty tag, answer constraints per type, set `status = APPROVED` for Expert creator (BR-55)
  - [ ] `UpdateQuestionCommand` — capture `QuestionVersion` snapshot before save if current `status = APPROVED` (BR-54); validate constraints
  - [ ] `ToggleQuestionActiveCommand` — check no active `TestQuestion` references (DC-02)
  - [ ] `DeleteQuestionCommand` — hard-delete if no test references; soft-delete otherwise (DC-02)
  - [ ] `AdminApproveQuestionCommand` — set `status = APPROVED`
  - [ ] `AdminRejectQuestionCommand` — set `status = REJECTED`; requires non-empty reject reason
- [ ] **Report Commands**:
  - [ ] `ReportQuestionCommand` — Student: create `QuestionReport`, do NOT change question status (BR-58); Expert: create report, temporarily hide question from generation
  - [ ] `ResolveReportCommand` — Expert resolves own question report; restore visibility (BR-60)
  - [ ] Validate Teacher cannot report (BR-59) → 403
- [ ] **Tag Commands**:
  - [ ] `CreateTagTopicCommand` — assign `parent_tag_id`, validate grade
  - [ ] `CreateTagDifficultyCommand` — validate UNIQUE `level_value`
  - [ ] `UpdateTagCommand`
  - [ ] `DeleteTagCommand` — check no linked `question_topics` records (DC-02)
- [ ] **Queries**:
  - [ ] `GetDashboardQuery` — count by `status`, `question_type`, `grade`; return last 5 reports
  - [ ] `GetQuestionListQuery` — paged (pageSize, pageIndex), filter by `status`, `grade`, `tag_id`, `question_type`, `expert_id`
  - [ ] `GetQuestionVersionsQuery` — ordered by `created_time` DESC
  - [ ] `GetReportedQuestionsQuery` — filter by `reporter_account_id` = current Expert's questions
  - [ ] `GetTagListQuery` — return hierarchical topic tree + flat difficulty list
- [ ] **File Parsers**:
  - [ ] `ExcelQuestionParser` — parse .xlsx using EPPlus; return list of `QuestionCreationDTO`
  - [ ] `WordQuestionParser` — parse .docx using OpenXml SDK; return list of `QuestionCreationDTO`
  - [ ] Validate each parsed question against BR-05, BR-50, BR-61, BR-62
- [ ] **MassTransit Consumer**: `BulkImportConsumer` — reads from `excel_import_queue`, calls `CreateQuestionCommand` per item, publishes `BulkImportCompletedEvent`
- [ ] **Domain Events**: `QuestionReportedEvent`, `QuestionApprovedEvent`

---

## Phase 3: Controller and Routing

- [ ] `QuestionsController` — ExpertOnly + AdminOnly hybrid routes
- [ ] `TagsController` — ExpertOnly routes for topic/difficulty CRUD
- [ ] `ReportsController` — Mixed: Student (POST report), Expert (GET + resolve), Admin (GET all + approve/reject)
- [ ] Image upload helper endpoint → Cloudinary REST client, return `picture_url`
- [ ] Register all inside `QuestionBankModuleExtensions.cs`:
  - DbContext, MediatR handlers, Parsers, MassTransit consumer

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] UC-20/21: Create SINGLE_CHOICE → `status = APPROVED`, 4 answers, 1 correct
  - [ ] UC-21: Create with 0 Topic tags → 400 (BR-05)
  - [ ] UC-21: Create TRUE_FALSE with 3 answers → 400 (BR-62)
  - [ ] UC-21: Create SHORT_ANSWER with rich-text/image content in correct field → 400 (BR-61)
  - [ ] UC-25: Update APPROVED question → QuestionVersion created before update (BR-54)
  - [ ] UC-27: Delete question used in test_questions → 409 (DC-02)
  - [ ] UC-28: Student report → QuestionReport created, question status unchanged (BR-58)
  - [ ] UC-28: Teacher attempts to report → 403 (BR-59)
  - [ ] UC-31/32: Admin approve/reject → status transitions correct
  - [ ] UC-23: Import 10 questions from Excel → all created, invalid rows rejected
  - [ ] UC-38: Delete tag with linked questions → 409 (DC-02)
