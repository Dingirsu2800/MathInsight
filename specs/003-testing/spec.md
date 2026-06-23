# Feature Specification: Testing Module

**Feature Branch**: `003-testing`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Handles blueprint creation, exam auto-generation selection, student practice/mock tests execution sessions, countdown server sync, auto-saves and anti-cheat tab monitoring."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-47: Doing Test/Question**
- **UC-48: Report Error**
- **UC-49: Submit Test/question**
- **UC-50: View Detailed Solution**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-03: Once a TestSession is 'Submitted' or 'Force-Submitted', all associated answers become read-only to guarantee audit integrity.**
- **BR-10: Exam security: detects browser tab focus loss, logs security incident. Switches of 5 times suspends session and force-submits.**
- **BR-11: Progress is auto-saved in the background every 5 minutes or upon answer selection.**
- **Timer expiration: when countdown timer reaches 0, system locks interface, saves selected answers, and force-submits.**

### Key Entities *(include if feature involves data)*

- **Test**:  test_id, blueprint_id (FK), test_name, duration, test_status, created_time
- **TestQuestion**:  test_id (FK), question_id (FK), question_order
- **TestSession**:  session_id, test_id (FK), student_id, test_format (PRACTICE, EXAM), status (IN_PROGRESS, COMPLETED, ABANDONED), start_time, end_time, total_question, num_correct, num_incorrect, num_abandoned, score
- **TestAnswer**:  test_answer_id, session_id (FK), question_id (FK), is_answered, submitted_time
- **TestAnswerOption**:  test_answer_id (FK), answer_id (FK) (Composite PK for unique selections)
- **TestIncidents**:  incident_id, session_id (FK), type, time

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `tst` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.