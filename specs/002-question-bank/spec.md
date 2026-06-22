# Feature Specification: Question Bank Management

**Feature Branch**: `[specs/002-question-bank]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and Maintain Mathematics Questions (Priority: P1)
As an Expert, I want to write, edit, and search math questions containing LaTeX equations and multi-choice options so that I can populate the active question pool.
**Why this priority**: The core value proposition of MathInsight is its rich mathematical questions. Experts need a powerful editor to manage this.
**Independent Test**: Creating a question parses LaTeX formulas, saves options, and makes it searchable in the Question Bank.
**Acceptance Scenarios**:
1. **Given** Expert is creating a question, **When** they type LaTeX code (e.g., `\int x dx`), **Then** it renders correctly in the preview, and saves with 4 MCQ options.
2. **Given** a search keyword 'Tích phân', **When** Expert searches, **Then** only questions containing 'Tích phân' are listed in the grid.
3. **Given** a question is modified, **When** Expert saves, **Then** a new version is created in QuestionVersion history to support audit tracking.

---

### User Story 2 - Knowledge Taxonomy Tagging (Priority: P1)
As an Expert, I want to manage Topic and Difficulty Tags and map them to questions so that the test generator and recommendation engines can classify questions.
**Why this priority**: Adaptive recommendation and test blueprint generation depend entirely on structured metadata (Topic + Difficulty).
**Independent Test**: Expert creates subtopics, which are displayed in a cascading selector when filtering questions.
**Acceptance Scenarios**:
1. **Given** a topic tree, **When** Expert selects a parent topic, **Then** the cascading selector dynamically displays only its subtopics (BR-52).
2. **Given** a Tag is in use by active questions, **When** Expert tries to delete it, **Then** the action is blocked to preserve referential integrity (BR-57).

---

### User Story 3 - Moderation and Error Resolution (Priority: P2)
As a Student, Teacher, or Expert, I want to report errors in questions so that incorrect content is flagged and revised by content creators.
**Why this priority**: Ensures the quality and correctness of the mathematical content through crowd-sourced flagging and Expert review.
**Independent Test**: Student flags a question, sending it to the moderation dashboard and hiding it from future test generations.
**Acceptance Scenarios**:
1. **Given** a student is reviewing a test, **When** they click 'Report Error' and submit feedback, **Then** the question is temporarily suspended from mock test pools, and Expert is notified.
2. **Given** an Expert reviews a report, **When** they correct the question and re-publish, **Then** status becomes APPROVED, and it is restored to active visibility.

### Edge Cases

- **Duplicate Questions**: System must check for content duplicates during upload to avoid cluttering.
- **Cascading Deletions**: Deleting a topic or difficulty tag is strictly blocked if any questions are actively linked (DC-02, BR-57).
- **Active Question Editing**: Editing a question that has historical test answers must write a new row to `question_versions` instead of overwriting, ensuring past test results remain accurate (DC-03).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST support a rich math editor rendering LaTeX formulas in real-time (UC-21, UC-25).
- **FR-002**: Experts MUST be able to import questions in bulk from Excel/Word files with format validation (UC-23).
- **FR-003**: System MUST support question version control, logging creator, timestamp, and field differences (UC-24).
- **FR-004**: System MUST restrict soft-deletes of questions or tags if referenced in test sessions or student history (DC-02, UC-27).
- **FR-005**: Experts MUST be able to manage Topic and Difficulty tags, supporting hierarchical parent-child relationships (UC-34 to UC-38).
- **FR-006**: Students, Teachers, and Experts MUST be able to report issues on questions, which flags them on the Expert dashboard (UC-28, UC-29, UC-30).
- **FR-007**: Experts MUST be able to resolve reports, moving questions back to APPROVED or REJECTED statuses (UC-31, UC-32, UC-33).
- **FR-008**: Search and filter criteria in the question bank list MUST update results dynamically without full page reload (BR-63).

### Key Entities
- **Question**: Question content (LaTeX stem), grade, status, active flag, and timestamps.
- **Answer**: Multiple-choice options linked to questions, with correctness flags.
- **QuestionVersion**: Audited snapshots of question content changes.
- **QuestionReport**: Error reports submitted by students or reviewed by experts.
- **TagTopic & TagDifficulty**: System taxonomy tags for organizing content.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Dynamic search and filtering in the Question Bank list must return matches in under 1 second.
- **SC-002**: Question import parsing must process up to 100 questions within 5 seconds of file upload.
- **SC-003**: LaTeX rendering on the editor screen must complete in under 200ms after user stops typing.

## Assumptions

- MathJax or KaTeX will be used in the client browser for rendering LaTeX.
- Experts will upload Excel files conforming to the standardized structure.
