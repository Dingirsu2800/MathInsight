# Feature Specification: Recommender Module

**Feature Branch**: `005-recommender`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Calculates student competency scores, performs weakness analysis based on tag mastery rules, and maps weak points to recommended lectures/materials."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-52: View WeakTags**
- **UC-53: View Recommended Lectures**
- **UC-54: View Recommended Materials**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-04: Competency points and mastery status must fall strictly within the range 0.0 to 10.0 (or 0% to 100%).**
- **Competency updates are triggered immediately after test grading. Weak topics falling below the mastery threshold are flagged as WeakTags.**
- **Collaborative Filtering Recommendation**: The recommender uses the SAR (Smart Adaptive Recommendation) algorithm from the `recommenders-team/recommenders` library:
  - **Inputs**: User interaction matrices where rows represent `student_id`, columns represent `topic_tag_id`, and rating values are derived from correctness rates and attempt counts.
  - **Model Training**: A background scheduler trains the SAR model on co-occurrence similarities of user tags.
  - **Predictions & Recommendations**: Predicts affinity scores for untaken or low-mastery tags.
  - **WeakTag Diagnosis**: Tags with calculated mastery index under 5.0 (P_tag < 5.0) are classified as `WeakTags`.
- **Dynamic Test Generator Loop**: Exposes internal endpoints/interfaces for the `TestGen` module. When generating a test, `TestGen` calls this loop to retrieve the user's `WeakTags` and adapt the question candidate pool by biasing question selection weights toward those diagnosed tags.

### Key Entities *(include if feature involves data)*

- **CompetencyPoint**:  competency_id, student_id (FK), topic_id, points (P_tag), grade, updated_time
- **TagsMastery**:  mastery_id, student_id (FK), topic_id, mastery_status (NOT_LEARNED, LEARNING, MASTERED), updated_time

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `rcm` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.