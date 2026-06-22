# Feature Specification: Adaptive Recommender System

**Feature Branch**: `[specs/005-recommender]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - WeakTag Diagnosis (Priority: P1)
As a Student, I want the system to identify my weak areas (topics with mastery scores below 0.5) so that I know what to study next.
**Why this priority**: Helps students target their study time. Serves as the filter for recommendations.
**Independent Test**: System lists tags with Ptag < 0.5 in the Weak Areas section of the student dashboard.
**Acceptance Scenarios**:
1. **Given** a Student has a Calculus tag mastery score of 0.35, **When** they open their Weak Areas dashboard, **Then** 'Calculus' is listed in red as a WeakTag.
2. **Given** a student has no test data, **When** they view Weak Areas, **Then** the system displays a default message asking them to take a test first.

---

### User Story 2 - Recommended Lectures and Materials (Priority: P1)
As a Student, I want the system to suggest video lectures and study PDFs related to my WeakTags so that I can review theory before practicing again.
**Why this priority**: Closes the learning loop. Diagnostic analytics are only useful if the student is given material to fix their weaknesses.
**Independent Test**: Recommended lecture list updates to show lectures tagged with the diagnosed WeakTags.
**Acceptance Scenarios**:
1. **Given** a Student has 'Integration' as a WeakTag, **When** they check the dashboard, **Then** lectures and PDFs tagged with 'Integration' are prominently displayed.
2. **Given** a student completes a test and raises their Integration score to 0.7, **When** they visit the dashboard, **Then** 'Integration' lectures are replaced by recommendations for their new lowest topics.

### Edge Cases

- **No Weak Tags**: If a student scores > 0.5 across all topics, the recommender must display a congratulations message and suggest hard-difficulty practice exams to maintain their scores (UC-52 AF-01).
- **Out-of-Date Scores**: Recommendations must update immediately after a practice test is auto-graded (GBR-08).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST diagnose WeakTags where the mastery score Ptag is less than 0.5 (or the bottom three tags if all are below 0.5) (UC-52, BR-15).
- **FR-002**: System MUST automatically suggest published video lectures matching the student's current WeakTags (UC-53, GBR-07).
- **FR-003**: System MUST automatically suggest learning materials (PDF/DOCX) matching the student's current WeakTags (UC-54, GBR-07).
- **FR-004**: Recommendation list MUST recalculate automatically as soon as a new test submission updates the competency scores (GBR-08).
- **FR-005**: Recommendations MUST respect content visibility rules, suggesting only published lectures and active materials (GBR-09).

### Key Entities
- **CompetencyPoint**: Student mastery data.
- **TagsMastery**: Student mastery history.
- **Lecture**: Available video lessons.
- **Material**: Supplementary reading files.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Recommendation engine must resolve and fetch matching lectures/materials in under 1 second.
- **SC-002**: Recommendations must update on the dashboard within 1 second after test grading completes.

## Assumptions

- The lecture library has sufficient coverage of topics so that every tag has at least one associated lecture.
