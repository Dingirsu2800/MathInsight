# Feature Specification: Grading & Analytics

**Feature Branch**: `[specs/004-grading-analytics]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Asynchronous Auto-Grading (Priority: P1)
As a Student, I want the system to grade my test automatically and immediately after submission so that I can see my score and review the detailed solutions.
**Why this priority**: Immediate feedback is crucial for student learning. Asynchronous grading prevents web request timeouts.
**Independent Test**: Submitting a test starts background job JOB-01, which grades the test, updates competency scores, and redirects the student to the result page.
**Acceptance Scenarios**:
1. **Given** a submitted test, **When** JOB-01 triggers, **Then** it grades the answers against the key, calculates the final score, and sets session status to 'Graded'.
2. **Given** a graded test, **When** Student views the solution, **Then** they see step-by-step LaTeX explanations for correct and incorrect answers (UC-50).

---

### User Story 2 - Competency Index Tracking (Priority: P1)
As a Student, I want to track my general mathematical mastery score (from 0.0 to 10.0) across tags and view my progress over time so that I know which areas to improve.
**Why this priority**: Helps students measure their skill growth. Drives the recommendation system.
**Independent Test**: Completing tests recalculates the student's mastery value for the tags tested, keeping scores in the 0.0-10.0 range.
**Acceptance Scenarios**:
1. **Given** a student competency profile, **When** they complete a practice test with 100% correct answers in Calculus, **Then** their Calculus mastery score (Ptag) increases towards 10.0.
2. **Given** a Student views their Competency Report, **When** they look at the performance analysis, **Then** they see line/bar charts showing their historical scores (UC-56).

---

### User Story 3 - Competency Heatmap Viewing (Priority: P2)
As a Student, I want to view a visual heatmap of my competencies categorized by topic and difficulty level so that I can easily spot my weak areas.
**Why this priority**: Visual representation of mastery. A heatmap is much easier to scan than raw numbers, enhancing usability.
**Independent Test**: Heatmap cells display colors corresponding to competency ranges (e.g. green for mastery, red for weak).
**Acceptance Scenarios**:
1. **Given** a student with low scores in Advanced Algebra, **When** they view the Competency Heatmap, **Then** the cell for 'Algebra - Hard' is highlighted in red (representing Ptag < 0.5).

### Edge Cases

- **Calculations Boundary**: Derived mastery indices must fall strictly within the range of 0.0 to 10.0 (or 0% to 100%). Any out-of-bound result must trigger alerts (DC-04).
- **Transactional Atomicity**: Grading, logging learning activity, and updating mastery scores must execute as a single atomic transaction. Any error rolls back all states (DC-05).
- **Row-Level Security**: Students must be blocked from modifying URL parameters to view another student's report (UC-55 AF-01).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST run an event-driven background task (JOB-01) to auto-grade test sessions asynchronously (UC-49, JOB-01).
- **FR-002**: System MUST calculate competency scores (0.0 to 10.0 range) for each tag based on correctness (DC-04).
- **FR-003**: System MUST update student competency values atomically; failure rolls back the session state (DC-05).
- **FR-004**: System MUST render detailed, step-by-step LaTeX solutions for all exam questions on the review screen (UC-50).
- **FR-005**: System MUST provide charts (line/bar) showing score trends and exam history (UC-56, UC-59).
- **FR-006**: System MUST show a competency heatmap mapping topics and difficulties, with color-coded mastery alerts (UC-57).
- **FR-007**: System MUST enforce row-level access controls, blocking students from accessing other students' report pages (UC-55, BR-21).

### Key Entities
- **CompetencyPoint**: Student's current mastery score for specific topic/difficulty tags.
- **TagsMastery**: Aggregated history of topic-level competency points.
- **ActivityLog**: Raw event log mapping user actions to timestamped learning events.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: The grading and competency recalculation background job (JOB-01) must complete within 5 seconds of test submission.
- **SC-002**: Analytical charts and heatmap rendering must load within 2 seconds.
- **SC-003**: Row-level access violations must immediately return HTTP 403 Forbidden.

## Assumptions

- Charting libraries (e.g. Chart.js, Recharts) are compatible with the frontend client.
- Database supports ACID transactions across multiple tables for atomic grading.
