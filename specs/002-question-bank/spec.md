# Feature Specification: Question Bank Module

**Feature Branch**: `002-question-bank`

**Created**: 2026-06-23 | **Updated**: 2026-07-10

**Status**: Approved

**Source Documents**: PRD §4 (FT-02), UCS UC-18–UC-38, TDS §3 (questions, answers, tags, reports, versions)

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor |
|-------|------|---------------|
| UC-18 | View Dashboard | Expert |
| UC-19 | View Question List | Expert |
| UC-20 | Input Single Question | Expert |
| UC-21 | Manual Input | Expert |
| UC-22 | Input by Image | Expert |
| UC-23 | Input by Excel/Word File | Expert |
| UC-24 | View Question Version Control | Expert |
| UC-25 | Update Question | Expert |
| UC-26 | Activate/Deactivate Question | Expert |
| UC-27 | Delete Question | Expert |
| UC-28 | Report Question (Student) | Student |
| UC-29 | Report Question (Expert) | Expert |
| UC-30 | View Reported Questions | Expert |
| UC-31 | Approve Question | Admin |
| UC-32 | Reject Question | Admin |
| UC-33 | Handle Question's Report | Expert |
| UC-34 | View Tag List | Expert |
| UC-35 | Create Tag for Topic | Expert |
| UC-36 | Create Tag for Difficulty | Expert |
| UC-37 | Update Tag | Expert |
| UC-38 | Delete Tag | Expert |

### Edge Cases

- **Duplicate question**: System detects near-match via content hash → HTTP 409 with link to existing question.
- **Empty answers list**: SingleChoice / MultipleSelect must have at least one correct answer → HTTP 400.
- **Tag not found**: Assigning a non-existent `tag_id` → HTTP 422.
- **Question in test history**: Cannot hard-delete or deactivate a question used in any existing `TestQuestion` record → HTTP 409 (`QUESTION_IN_USE`).
- **Unsupported content format**: If `question_content` contains unsafe HTML, unsupported embedded content, or malformed rich-text payload → reject with HTTP 422 (frontend pre-validates; backend sanitizes/logs).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02**: Referential history is protected. Taxonomy tags are soft-disabled through `IsActive = false` to preserve QuestionBank/TestGen/Recommender references. A Question with any existing `TestQuestion` reference cannot be hard-deleted or manually deactivated and returns HTTP 409 (`QUESTION_IN_USE`); an unreferenced Question may be hard-deleted. Report moderation may still move a referenced Question to `Reported` without changing `IsActive`.
- **BR-04**: Mathematical questions and solutions must be entered through a user-friendly rich-text/WYSIWYG editor. Experts are not required to know or type technical markup syntax. The editor may provide toolbar-based math symbols, superscript/subscript, fractions, tables, and optional image upload for diagrams or complex formulas.
- **BR-05**: A newly created question must be assigned at least one **Topic** tag and one **Difficulty** tag before saving.
- **BR-50**: Each `SINGLE_CHOICE` question must have **exactly one** correct answer. `MULTIPLE_SELECT` must have **at least one** correct answer.
- **BR-61**: The correct answer for a `SHORT_ANSWER` question must be plain text or numeric (max 100 chars), without rich-text formatting or images.
- **BR-62**: `TRUE_FALSE` questions must have **exactly 2** options (True / False) with **exactly 1** correct answer.
- **BR-52**: Topic tags are cascading: selecting a parent tag automatically filters child subtopics only.
- **BR-54**: Changes to `APPROVED` or `REPORTED` questions must capture a `QuestionVersion` snapshot **before** applying the update.
- **BR-55**: Expert-created questions are **published/approved by default** (`status = APPROVED`) and do not enter an Admin approval queue.
- **BR-56**: UC-31 (Approve) and UC-32 (Reject) apply only to the Admin-initiated question rejection/re-review flow. If Admin rejects, the original Expert must fix and re-submit (sets `status = PENDING`); Admin reviews again before it becomes `APPROVED`.
- **BR-57**: Question status semantics:
  - `APPROVED` — published and visible in test generation.
  - `REPORTED` — excluded from newly generated tests while retained for existing tests and history; `IsActive` remains unchanged.
  - `REJECTED` — Admin-rejected; hidden until Expert handles.
  - `PENDING` — Expert re-submitted after Admin rejection; awaiting Admin re-review. **Not used for normal Expert creation.**
- **BR-58**: When a Student reports a question (UC-28), system creates a `QuestionReport` record but **must not hide, deactivate, or change the `status` of the Question**.
- **BR-59**: Teacher accounts are **not allowed** to report questions. Attempts → HTTP 403.
- **BR-60**: When an Expert reports another Expert's question (UC-29), after the original Expert resolves it (UC-33), the question becomes visible again **automatically** without requiring additional Admin approval.
- **BR-67**: Student reports create a `QuestionReport` only and may be submitted for any existing Question, including inactive or historical Questions. Expert and Admin reports require an active Question in `APPROVED` or `REPORTED` status; an Expert cannot report their own Question. Expert/Admin reports change an `APPROVED` Question to `REPORTED`.
- **BR-68**: Resolving or dismissing the final pending Expert/Admin report restores a `REPORTED` Question to `APPROVED`. Pending Student reports do not prevent restoration.
- **BR-69**: A Question with a pending report, or a Question in `REPORTED` status, cannot be hard-deleted. The API returns HTTP 409 (`QUESTION_HAS_PENDING_REPORTS`) and preserves the report audit trail.
- **BR-53**: File import (Excel/Word) must validate: question stem present, at least one Topic tag assigned, and answer structure valid for the question type. Option-based types need answer rows; `COMPOSITE` needs valid `QuestionPart` rows instead of normal `Answer` rows.
- **BR-63**: `TagDifficulty.LevelValue` is the stable cross-module difficulty contract. Values should be unique and normally map to Recommender v2 `RecommendedDifficultyLevel` values `1..4`. Question Bank owns `Question.DifficultyID`; Recommender v2 owns only student-topic mastery and does not store Ptag per difficulty.
- **BR-64**: `COMPOSITE` questions must have at least one `QuestionPart`. For THPT statement-style questions, MVP should model the parent question as `COMPOSITE` and create child parts with labels such as `a`, `b`, `c`, `d`. Part answer keys are stored on `QuestionPart` but must not be exposed in student-facing test APIs before grading.
- **BR-65**: Tag list APIs return only active records by default. Expert tag management may request inactive records with `includeInactive=true`; each response retains its `IsActive` state.
- **BR-66**: A topic cannot be soft-disabled while it has an active descendant at any depth. The operation returns HTTP 409 (`TAG_TOPIC_HAS_ACTIVE_DESCENDANTS`) and leaves the topic unchanged.

### Accepted Question Types

| Type | API Enum Value | DB Value | Constraint |
|------|----------------|----------|------------|
| Single Choice | `SINGLE_CHOICE` | `SingleChoice` | Exactly 1 correct answer |
| Multiple Select | `MULTIPLE_SELECT` | `MultipleChoice` | >= 1 correct answer |
| True/False | `TRUE_FALSE` | `TrueFalse` | Exactly 2 options, 1 correct |
| Short Answer | `SHORT_ANSWER` | `ShortAnswer` | Plain text/numeric, max 100 chars |
| Composite / Multi-part | `COMPOSITE` | `Composite` | Parent question with one or more `QuestionPart` rows |

### Key Entities *(include if feature involves data)*

- **Question**: `question_id`, `question_content` (TEXT, sanitized rich-text/plain-text content), `picture_url`, `difficulty_id` (FK to `TagDifficulty`), `grade` (10/11/12), `status` (**PENDING** | **APPROVED** | **REJECTED**), `question_type` (**SINGLE_CHOICE** | **MULTIPLE_SELECT** | **TRUE_FALSE** | **SHORT_ANSWER** | **COMPOSITE**, mapped to DB values above), `expert_id` (FK to `Expert`), `default_point` (DECIMAL 3,2), `is_active` (BOOLEAN)
- **Answer**: `answer_id`, `question_id` (FK), `answer_content` (TEXT), `is_correct` (BOOLEAN)
- **QuestionPart**: `part_id`, `question_id` (FK), `part_order`, `part_label` (nullable, e.g. `a`, `b`, `c`, `d`), `part_content`, `part_type` (**TRUE_FALSE** | **SHORT_ANSWER** | **NUMERIC_ANSWER**, mapped to DB values `TrueFalse`, `ShortAnswer`, `NumericAnswer`), `correct_boolean`, `correct_text`, `correct_numeric`, `numeric_tolerance`, `explanation` (nullable), `default_point`. The `correct_*` fields are answer keys and must be hidden from student-facing APIs before grading.
- **QuestionVersion**: `version_id`, `question_id` (FK), `question_content`, `question_answer`, `answers_snapshot` (JSON), `picture_url`, `created_time`, `expert_id` (FK)
- **QuestionReport**: `report_id`, `question_id` (FK), `reporter_account_id` (FK), `reporter_role` (**STUDENT** | **EXPERT** | **ADMIN**), `report_reason`, `status` (**PENDING** | **RESOLVED** | **DISMISSED**), `created_time`, `resolved_time`, `resolved_by` (FK → experts)
- **TagTopic**: `tag_id`, `parent_tag_id` (self-FK, nullable), `tag_name` (UNIQUE), `description`, `grade`, `is_active`, `display_order`
- **TagDifficulty**: `difficulty_id`, `difficulty_name` (UNIQUE), `description`, `level_value` (UNIQUE, stable `1..4` mapping for Recommender/TestGen), `display_order`, `is_active`
- **QuestionTopic**: `question_topic_id`, `question_id` (FK), `tag_id` (FK), `is_primary` (BOOLEAN) — supports Many-to-Many

### Cross-Module Contract: Recommender/TestGen v2

Recommender v2 stores student mastery at the **student + topic** level:

```text
TagsMastery = StudentID + TagID
WeakTag = TagsMastery.OfficialPoint < 5.0
```

It does **not** store Ptag per difficulty. This does not remove difficulty from Question Bank. Difficulty remains a property of each question and is required for TestGen selection.

Adaptive question selection should use this contract:

```text
TagsMastery.TagID
TagsMastery.RecommendedDifficultyLevel
  -> TagDifficulty.LevelValue
  -> TagDifficulty.DifficultyID
  -> Question.DifficultyID
  -> QuestionTopic.TagID
```

Therefore:

- Do not remove `Question.DifficultyID` or `TagDifficulty`.
- `TagDifficulty.LevelValue` must be unique and stable because it maps recommender output to concrete questions.
- TestGen should filter generated questions by topic, difficulty, and section question type when a `BlueprintSection` is used:

```sql
QuestionTopic.TagID = @tagId
Question.DifficultyID = @difficultyId
Question.QuestionType = @sectionQuestionType
Question.Status = 'Approved'
Question.IsActive = 1
```

> Schema alignment note: the current DB script must allow `Pending` in `Question.Status` before implementing the Admin rejection/re-review flow in BR-56. If the DB keeps only `Approved/Rejected/Reported/Deactivated`, handlers must not persist `Pending` until the constraint is updated.

### File Import Rules (UC-23)

| Format | Accepted | Max Size | Notes |
|--------|----------|----------|-------|
| Excel (.xlsx) | Yes | 20 MB | Template with predefined columns |
| Word (.docx) | Yes | 20 MB | Structured format per spec |
| PDF | No | — | Not supported |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints return responses within **2 seconds** (NFR-P01).
- Backend maps Question Bank entities to the current SQL script tables; no separate `qnb` schema is created for MVP.
- Version snapshot captured on every update to an `APPROVED` question.
- File import pipeline processes 100-question batch within 30 seconds.
- Tag cascade filtering returns correct subtopics within 500ms.

## Assumptions

- Target database is SQL Server. Backend maps to current DB script tables (`Question`, `Answer`, `QuestionVersion`, `QuestionReport`, `TagTopic`, `TagDifficulty`, `QuestionTopic`) instead of schema-prefixed tables.
- Cloudinary is used for image upload (picture_url from UC-22).
- MediatR domain events handle async version snapshot creation.
- Question and solution authoring is handled by a rich-text/WYSIWYG editor so non-technical Experts can input math content without technical markup syntax. Backend stores sanitized content and associated media URLs; frontend renders the stored rich text directly.
