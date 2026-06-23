# Feature Specification: Question Bank Module

**Feature Branch**: `002-question-bank`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Manages math questions bank (LaTeX supported), choices, tag taxonomies (topics, difficulties), peer/student feedback reporting, version histories and bulk import parsers."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-18: View dashboard**
- **UC-19: View question list**
- **UC-20: Input single question**
- **UC-21: Manual input**
- **UC-22: Input by image**
- **UC-23: Input by excel/word file**
- **UC-24: View question version control**
- **UC-25: Update question**
- **UC-26: Activate/Deactivate question**
- **UC-27: Delete question**
- **UC-28: Report Question (Student)**
- **UC-29: Report question (Expert)**
- **UC-30: Views reported questions**
- **UC-31: Approve question**
- **UC-32: Reject question**
- **UC-33: Handle question's report**
- **UC-34: View tag list**
- **UC-35: Create Tag for Topic**
- **UC-36: Create Tag for difficulty**
- **UC-37: Update Tag**
- **UC-38: Delete Tag**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-02: Entities cannot be hard-deleted if referenced in test sessions or blueprints. Soft delete is enforced via Status/isActive.**
- **BR-04: All mathematical questions and solutions must be formatted using LaTeX; static images for formulas are not permitted.**
- **BR-05: A newly created question must be assigned at least one Topic tag and one Difficulty tag before saving.**
- **BR-50: Each single-choice question must have exactly one correct answer. Multiple-choice questions must have at least one correct answer.**
- **BR-61: The correct answer for a ShortAnswer question must be a plain text or numeric value (maximum 100 characters) and must not contain LaTeX equations.**
- **BR-62: True/False questions must have exactly 2 options (True and False) with exactly 1 correct answer selected.**
- **BR-52: Topic tags are cascading: selecting a parent tag automatically filters and displays only its subtopics.**
- **BR-54: Changes to published questions must capture snapshots in QuestionVersion table for audit trails.**

### Key Entities *(include if feature involves data)*

- **Question**:  question_id, question_content, picture_url, difficulty_id (FK), grade, status (PENDING, APPROVED, REJECTED), question_type, expert_id (FK), default_point, is_active
- **Answer**:  answer_id, question_id (FK), answer_content, is_correct
- **QuestionVersion**:  version_id, question_id (FK), question_content, question_answer, answers_snapshot, picture_url, created_time, expert_id (FK)
- **QuestionReport**:  report_id, question_id (FK), reporter_account_id (FK), reporter_role, report_reason, status (PENDING, RESOLVED, DISMISSED), created_time, resolved_time, resolved_by (FK)
- **TagTopic**:  tag_id, parent_tag_id (FK), tag_name, description, grade, is_active, display_order
- **TagDifficulty**:  difficulty_id, difficulty_name, description, level_value, display_order, is_active
- **QuestionTopic**:  question_topic_id, question_id (FK), tag_id (FK), is_primary

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `qnb` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.