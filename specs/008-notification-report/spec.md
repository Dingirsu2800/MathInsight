# Feature Specification: Notification & Report Module

**Feature Branch**: `008-notification-report`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Pushes real-time alerts via WebSockets, parses notification templates, generates detailed performance graphs, competency heatmaps and leaderboard rankings."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-55: View competency report**
- **UC-56: View Individual Performance Analysis**
- **UC-57: View Competency Heatmap**
- **UC-58: View Leader board**
- **UC-59: View exam history**
- **UC-85: Receive Score Suggestions**
- **UC-89: Receive test result notification**
- **UC-90: Receive progress notification**
- **UC-91: Receive Streak Notification**
- **UC-92: Receive system event notification**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **BR-19: Leaderboards are recalculated daily at 00:00 using a scheduled Hangfire background task to avoid real-time performance overhead.**
- **Real-time notifications are pushed over SignalR WebSocket hubs routes on /hubs/notification.**
- **Notifications log retention policy: pruned after 90 days.**

### Key Entities *(include if feature involves data)*

- **Notification**:  notification_id, account_id (FK), title, content, type, status (UNREAD, READ), created_time

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `ntf` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.