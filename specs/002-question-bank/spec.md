# Feature Specification: Question Bank Module

**Feature Branch**: `002-question-bank`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

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
- **Question in active test**: Cannot hard-delete or deactivate a question used in existing `TestQuestion` records → HTTP 409.
- **LaTeX invalid**: If `question_content` contains invalid LaTeX → reject with HTTP 422 (frontend pre-validates; backend logs).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02**: Entities cannot be hard-deleted if referenced in test sessions or blueprints. Soft delete enforced via `is_active = false` or `status = REJECTED/ARCHIVED`.
- **BR-04**: All mathematical questions and solutions must be formatted using LaTeX. Static images for formulas are not permitted.
- **BR-05**: A newly created question must be assigned at least one **Topic** tag and one **Difficulty** tag before saving.
- **BR-50**: Each `SINGLE_CHOICE` question must have **exactly one** correct answer. `MULTIPLE_SELECT` must have **at least one** correct answer.
- **BR-61**: The correct answer for a `SHORT_ANSWER` question must be plain text or numeric (max 100 chars), must not contain LaTeX.
- **BR-62**: `TRUE_FALSE` questions must have **exactly 2** options (True / False) with **exactly 1** correct answer.
- **BR-52**: Topic tags are cascading: selecting a parent tag automatically filters child subtopics only.
- **BR-54**: Changes to `APPROVED` questions must capture a `QuestionVersion` snapshot **before** applying the update.
- **BR-55**: Expert-created questions are **published/approved by default** (`status = APPROVED`) and do not enter an Admin approval queue.
- **BR-56**: UC-31 (Approve) and UC-32 (Reject) apply only to the Admin-initiated question rejection/re-review flow. If Admin rejects, the original Expert must fix and re-submit (sets `status = PENDING`); Admin reviews again before it becomes `APPROVED`.
- **BR-57**: Question status semantics:
  - `APPROVED` — published and visible in test generation.
  - `REJECTED` — Admin-rejected; hidden until Expert handles.
  - `PENDING` — Expert re-submitted after Admin rejection; awaiting Admin re-review. **Not used for normal Expert creation.**
- **BR-58**: When a Student reports a question (UC-28), system creates a `QuestionReport` record but **must not hide, deactivate, or change the `status` of the Question**.
- **BR-59**: Teacher accounts are **not allowed** to report questions. Attempts → HTTP 403.
- **BR-60**: When an Expert reports another Expert's question (UC-29), after the original Expert resolves it (UC-33), the question becomes visible again **automatically** without requiring additional Admin approval.
- **BR-53**: File import (Excel/Word) must validate: question stem present, at least 2 answers, one marked correct, at least one Topic tag assigned.

### Accepted Question Types

| Type | Enum Value | Constraint |
|------|-----------|-----------|
| Single Choice | `SINGLE_CHOICE` | Exactly 1 correct answer |
| Multiple Select | `MULTIPLE_SELECT` | ≥ 1 correct answer |
| True/False | `TRUE_FALSE` | Exactly 2 options, 1 correct |
| Short Answer | `SHORT_ANSWER` | Plain text/numeric, max 100 chars |

### Key Entities *(include if feature involves data)*

- **Question**: `question_id`, `question_content` (TEXT, LaTeX), `picture_url`, `difficulty_id` (FK → tag_difficulties), `grade` (10/11/12), `status` (**PENDING** | **APPROVED** | **REJECTED**), `question_type` (**SINGLE_CHOICE** | **MULTIPLE_SELECT** | **TRUE_FALSE** | **SHORT_ANSWER**), `expert_id` (FK → experts), `default_point` (DECIMAL 3,2), `is_active` (BOOLEAN)
- **Answer**: `answer_id`, `question_id` (FK), `answer_content` (TEXT), `is_correct` (BOOLEAN)
- **QuestionVersion**: `version_id`, `question_id` (FK), `question_content`, `question_answer`, `answers_snapshot` (JSON), `picture_url`, `created_time`, `expert_id` (FK)
- **QuestionReport**: `report_id`, `question_id` (FK), `reporter_account_id` (FK), `reporter_role` (**STUDENT** | **EXPERT** | **ADMIN**), `report_reason`, `status` (**PENDING** | **RESOLVED** | **DISMISSED**), `created_time`, `resolved_time`, `resolved_by` (FK → experts)
- **TagTopic**: `tag_id`, `parent_tag_id` (self-FK, nullable), `tag_name` (UNIQUE), `description`, `grade`, `is_active`, `display_order`
- **TagDifficulty**: `difficulty_id`, `difficulty_name` (UNIQUE), `description`, `level_value`, `display_order`, `is_active`
- **QuestionTopic**: `question_topic_id`, `question_id` (FK), `tag_id` (FK), `is_primary` (BOOLEAN) — supports Many-to-Many

### File Import Rules (UC-23)

| Format | Accepted | Max Size | Notes |
|--------|----------|----------|-------|
| Excel (.xlsx) | Yes | 20 MB | Template with predefined columns |
| Word (.docx) | Yes | 20 MB | Structured format per spec |
| PDF | No | — | Not supported |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints return responses within **2 seconds** (NFR-P01).
- Schema isolation enforced under `qnb` namespace.
- Version snapshot captured on every update to an `APPROVED` question.
- File import pipeline processes 100-question batch within 30 seconds.
- Tag cascade filtering returns correct subtopics within 500ms.

## Assumptions

- Target database is SQL Server; schema prefix is `qnb`.
- Cloudinary is used for image upload (picture_url from UC-22).
- MediatR domain events handle async version snapshot creation.
- LaTeX rendering is handled client-side (KaTeX/MathJax); backend stores raw LaTeX strings.
