# Feature Specification: Gamification Module

**Feature Branch**: `007-gamification`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-11, FT-12, FT-13), UCS UC-80–UC-88, TDS §3 (badges, study_streaks, target_scores, activity_logs)

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Trigger |
|-------|------|---------------|---------|
| UC-80 | Record Learning Activity | System | After test submit, lecture view, material download |
| UC-81 | View Study Streak | Student | Student checks dashboard streak indicator |
| UC-82 | View Badge List | Student | Browse badge catalogue |
| UC-83 | Auto-Award Badge | System | After activity → check badge conditions |
| UC-84 | Track Badge Progress | Student | View progress toward locked badges |
| UC-85 | Set Target Score | Student | Student sets target for a specific tag |
| UC-86 | Update Target Score | Student | Student updates existing target |
| UC-87 | View Target Progress | Student | View current competency vs target |
| UC-88 | Receive Score Suggestions | Student | After grading → auto-suggest target adjustments |

### Edge Cases

- **Duplicate activity log**: Same `(student_id, activity_type, activity_date)` — always insert new record (ActivityLog is append-only, no dedup).
- **Streak already broken**: Gap > 1 day → reset `current_streak = 0`; `longest_streak` unchanged if not exceeded.
- **Badge already earned**: `StudentBadge` composite PK prevents duplicate awards.
- **Target already achieved**: `competency_score >= target_point` → display congratulatory message; suggest new higher target.
- **Target out of range**: `target_point` outside 0–10 → HTTP 400 (DC-04).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-04**: Target scores must fall strictly within **0.0 to 10.0**. Out-of-range → HTTP 400.
- **BR-39**: Daily streak **active criteria**: student must complete **at least 1 test submission** OR **5 minutes of lecture viewing** per calendar day.
- **BR-40**: `ActivityLog` records are **permanent, insert-only**. No update or delete operations are permitted.
- **BR-41**: Streak is reset to 0 if no qualifying activity is detected for more than **1 calendar day** (gap detection).
- **BR-42**: `StudyStreak.longest_streak` is updated only when `current_streak > longest_streak`.
- **BR-43**: Badge auto-award runs after every `ActivityLoggedEvent`. Badge conditions checked:
  - `condition_type = TOTAL_CORRECT_ANSWERS` AND `condition_value <= total_correct_count` → award badge.
  - `condition_type = STREAK_DAYS` AND `condition_value <= current_streak` → award badge.
  - `condition_type = TESTS_COMPLETED` AND `condition_value <= test_session_count` → award badge.
- **BR-44**: Target score (`TargetScore`) is per `student_id` × `tag_id`. A student may have at most **one target per topic tag**.
- **BR-45**: Streak reminder notification is sent when the student has not completed a qualifying activity by **20:00 local time** (pushed via Notification module).

### Activity Types

| Activity Type | Qualifies for Streak? | Notes |
|---------------|----------------------|-------|
| `PRACTICE` | Yes (1 test submission) | `TestSession.test_format = PRACTICE` |
| `EXAM` | Yes (1 test submission) | `TestSession.test_format = EXAM` |
| `VIEW_LECTURE` | Yes if ≥ 5 min | `duration_seconds >= 300` |
| `DOWNLOAD_MATERIAL` | No | Logged but does not count for streak |

### Key Entities *(include if feature involves data)*

- **Badge**: `badge_id`, `badge_name` (UNIQUE), `description`, `icon_url`, `condition_type` (TOTAL_CORRECT_ANSWERS | STREAK_DAYS | TESTS_COMPLETED), `condition_value` (INT), `created_time`
- **StudentBadge**: `student_id` (FK), `badge_id` (FK), `earned_time` — composite PK (immutable)
- **StudyStreak**: `streak_id`, `student_id` (FK, UNIQUE 1:1), `current_streak`, `longest_streak`, `last_activity_date`
- **TargetScore**: `target_id`, `student_id` (FK), `tag_id` (FK → tag_topics), `target_point` (INT, 0–10), `created_time`, `updated_time` — UNIQUE `(student_id, tag_id)`
- **ActivityLog**: `activity_log_id`, `student_id` (FK), `activity_type` (PRACTICE | EXAM | VIEW_LECTURE | DOWNLOAD_MATERIAL), `test_session_id` (FK, nullable), `lecture_id` (FK, nullable), `material_id` (FK, nullable), `duration_seconds`, `activity_date`

### Enums

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| ActivityLog | activity_type | `PRACTICE`, `EXAM`, `VIEW_LECTURE`, `DOWNLOAD_MATERIAL` |
| Badge | condition_type | `TOTAL_CORRECT_ANSWERS`, `STREAK_DAYS`, `TESTS_COMPLETED` |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Activity logging completes within **1 second** of event received.
- Badge check and award completes within **2 seconds** of trigger.
- Streak calculation is accurate across timezone boundaries.
- Schema isolation enforced under `gam` namespace.
- `ActivityLog` is strictly insert-only — no update/delete SQL generated by ORM.

## Assumptions

- Target database is SQL Server; schema prefix is `gam`.
- Consumes `ActivityLoggedEvent` from Learning (006) and `TestSubmittedEvent` from Testing (003) via MediatR.
- Notification module (008) handles streak reminder push delivery.
- Hangfire can be configured for daily streak-check jobs (optional: event-driven via login).