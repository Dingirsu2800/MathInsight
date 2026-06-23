# Feature Specification: Gamification Module

**Feature Branch**: `007-gamification`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Calculates study streaks, validates daily activities, rewards achievement badges, and supports student target score planning dashboards."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-80: Record learning activity**
- **UC-81: View Study Streak**
- **UC-82: View Badge List**
- **UC-83: Auto-Award Badge**
- **UC-84: Track Badge Progress**
- **UC-85: Set Target Score**
- **UC-86: Update Target Score**
- **UC-87: View Target Progress**
- **UC-88: Receive Score Suggestions**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **Daily streak active criteria: requires at least 1 test submission or 5 minutes of lecture viewing per calendar day.**
- **DC-04: Target scores must fall strictly within the range of 0.0 to 10.0.**
- **ActivityLog records are permanent read-only insert-only records (no update or delete permitted).**

### Key Entities *(include if feature involves data)*

- **Badge**:  badge_id, badge_name, description, icon_url, condition_type, condition_value, created_time
- **StudentBadge**:  student_id (FK), badge_id (FK), earned_time
- **StudyStreak**:  streak_id, student_id (FK), current_streak, longest_streak, last_activity_date
- **TargetScore**:  target_id, student_id (FK), tag_id (FK), target_point, created_time, updated_time
- **ActivityLog**:  activity_log_id, student_id (FK), activity_type, test_session_id (FK, nullable), lecture_id (FK, nullable), material_id (FK, nullable), duration_seconds, activity_date

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `gam` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.