# Feature Specification: Notification & Report Module

**Feature Branch**: `008-notification-report`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §4 (FT-07, FT-14), UCS UC-55–UC-59, UC-88–UC-90, UC-92, TDS §3 (notifications), §5.7

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor | Notes |
|-------|------|---------------|-------|
| UC-55 | View Competency Report | Student | Aggregated performance data from Recommender tables |
| UC-56 | View Individual Performance Analysis | Student | Score trend charts (30-day history) |
| UC-57 | View Competency Heatmap | Student | Topic × Difficulty mastery heatmap |
| UC-58 | View Leaderboard | Student | Daily recalculated class ranking |
| UC-59 | View Exam History | Student | Chronological past test sessions |
| UC-88 | Receive Score Suggestions | Student | Auto-triggered after grading (BR-45) |
| UC-89 | Push System Notification (Test Result) | System | After `GradeCalculatedEvent` |
| UC-90 | Receive Discussion/Streak Notification | Student | From Learning/Gamification events |
| UC-92 | Receive System Event Notification | User (all) | Account events (approval, activation) |

### Edge Cases

- **No test history**: Competency report shows empty state with "No data yet" message.
- **Leaderboard not recalculated**: Served from last cached daily calculation; note shown if stale.
- **Notification pruning**: Notifications older than 90 days are auto-deleted (BR-46).
- **SignalR disconnected client**: Notification stored in DB; delivered on next connection or page load.

## Requirements *(mandatory)*

### Functional Requirements

- **BR-19**: Leaderboards are recalculated **daily at 00:00** using a Hangfire scheduled task. Avoids real-time performance overhead.
- **BR-20**: Real-time notifications are pushed over **SignalR WebSocket** hubs at route `/hubs/notification`.
- **BR-21 (formerly BR-46)**: Notification log retention policy: records older than **90 days** are pruned via daily Hangfire job.
- **BR-22**: Notifications are delivered in two channels: (1) persisted `Notification` record in DB (for history), (2) real-time SignalR push (for instant badge/alert).
- **BR-23**: The Notification module listens to the following events via MediatR:
  - `GradeCalculatedEvent` → push test result notification
  - `BadgeAwardedEvent` → push badge earned notification
  - `DiscussionQuestionPostedEvent` → notify Teacher of new student question
  - `DiscussionAnsweredEvent` → notify Student of Teacher's answer
  - `ApplicationResolvedEvent` → notify Teacher of approval/rejection
  - `AccountCreatedEvent` → send welcome email
  - `StreakReminderEvent` → notify Student to maintain streak

### Report Data Sources

| Report | Data Source | Update Frequency |
|--------|-------------|-----------------|
| Competency Report (UC-55) | `CompetencyPoint`, `TagsMastery` | Real-time (after each grade) |
| Performance Analysis (UC-56) | `TestSession` (score history) | Real-time |
| Competency Heatmap (UC-57) | `TagsMastery` (per topic/Ptag) | Real-time |
| Leaderboard (UC-58) | Hangfire daily cache (Redis or DB snapshot) | Daily 00:00 |
| Exam History (UC-59) | `TestSession` (`Graded`/historical sessions) | Real-time |

### Notification Template Types

| Template | Trigger Event | Recipient |
|----------|---------------|-----------|
| Test result ready | `GradeCalculatedEvent` | Student |
| Badge earned | `BadgeAwardedEvent` | Student |
| New discussion question | `DiscussionQuestionPostedEvent` | Teacher |
| Discussion answered | `DiscussionAnsweredEvent` | Student |
| Teacher application resolved | `ApplicationResolvedEvent` | Teacher |
| Streak reminder | `StreakReminderEvent` | Student |
| Account created/activated | `AccountCreatedEvent` | New user |

### Key Entities *(include if feature involves data)*

- **Notification**: `notification_id`, `account_id` (FK → accounts), `title` (VARCHAR 100), `content` (VARCHAR 255), `link` (VARCHAR 255, nullable — redirect URL), `is_read` (BOOLEAN, DEFAULT false), `created_time`

## Success Criteria *(mandatory)*

### Measurable Outcomes

- Real-time SignalR push delivered within **500ms** of event trigger.
- Report APIs respond within **2 seconds** (NFR-P01).
- Heatmap query responds within **2 seconds** (served from Redis cache fallback per TDS §9.2).
- Leaderboard recalculation completes within **60 seconds** at 00:00 daily.
- Backend maps Notification/Report entities to the current SQL script tables; no separate `ntf` schema is created for MVP.
- 90-day notification pruning runs daily without performance impact.

## Assumptions

- Target database is SQL Server. Backend maps to current DB script tables (`Notification`, `CompetencyPoint`, `TagsMastery`, `TestSession`) instead of schema-prefixed tables.
- SignalR Hub registered at `/hubs/notification`; JWT authentication required.
- Redis used as fallback cache for heatmap and leaderboard data.
- Report aggregation queries cross-read current DB script tables owned by Recommender, Testing, and Gamification.
- Email delivery (account activation, application result) via SMTP/SendGrid — injected via environment variables.
