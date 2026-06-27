# Implementation Plan: Notification & Report Module

**Branch**: `008-notification-report` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Notification_Report` managing real-time SignalR push notifications, analytics reports (competency, heatmap, leaderboard, exam history), and email dispatch. Consumes multiple domain events from other modules.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, SignalR, Hangfire |
| Storage | SQL Server; map to current DB script tables and cross-read Recommender/Testing/Gamification tables |
| Real-time | SignalR Hub at `/hubs/notification` |
| Cache | Redis (heatmap, leaderboard fallback) |
| Email | SMTP / SendGrid |
| Scheduler | Hangfire (daily leaderboard, daily 90-day prune) |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Notification_Report/
├── Consumers/
│   ├── GradeCalculatedConsumer.cs         # → send test result notification
│   ├── BadgeAwardedConsumer.cs            # → send badge earned notification
│   ├── DiscussionQuestionPostedConsumer.cs # → notify Teacher
│   ├── DiscussionAnsweredConsumer.cs       # → notify Student
│   ├── ApplicationResolvedConsumer.cs     # → notify Teacher
│   ├── AccountCreatedConsumer.cs          # → send welcome email
│   └── StreakReminderConsumer.cs          # → push streak reminder
├── Services/
│   ├── INotificationService.cs
│   ├── NotificationService.cs             # Create DB record + SignalR push
│   ├── IEmailService.cs
│   └── EmailService.cs                    # SMTP/SendGrid integration
├── Hubs/
│   └── NotificationHub.cs                 # SignalR: /hubs/notification
├── Queries/
│   ├── GetNotifications/                  # List unread/all notifications for account
│   ├── GetCompetencyReport/               # UC-55: competency_points + tags_mastery summary
│   ├── GetPerformanceAnalysis/            # UC-56: score history (30-day) from test_sessions
│   ├── GetCompetencyHeatmap/              # UC-57: tags_mastery per topic × difficulty
│   ├── GetLeaderboard/                    # UC-58: cached daily leaderboard
│   └── GetExamHistory/                   # UC-59: paginated test_sessions (SUBMITTED+)
├── Commands/
│   └── MarkNotificationRead/             # PUT /api/v1/notifications/{id}/read
├── Jobs/
│   ├── LeaderboardRecalculationJob.cs    # Hangfire: daily 00:00 (BR-19)
│   └── NotificationPruneJob.cs           # Hangfire: daily 01:00 (prune > 90 days)
├── Persistence/
│   ├── NotificationDbContext.cs          # maps to current DB script table names
│   ├── Configurations/
│   │   └── NotificationConfiguration.cs
│   └── Migrations/
├── Controllers/
│   ├── NotificationsController.cs
│   └── ReportsController.cs
└── NotificationReportModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Key Indexes |
|-------|-------------|
| `Notification` | `(UserID, IsRead)` query pattern; `CreatedTime` for pruning |

### Service & API Gateway — REST Endpoints

**Authenticated (all roles)**
```
GET    /api/v1/notifications                 # List notifications (paged, unread filter)
PUT    /api/v1/notifications/{id}/read       # Mark as read
```

**Student (StudentOnly policy)**
```
GET    /api/v1/reports/competency            # UC-55: competency points + tags mastery
GET    /api/v1/reports/performance           # UC-56: score trend (30-day)
GET    /api/v1/reports/heatmap              # UC-57: topic × difficulty heatmap
GET    /api/v1/reports/leaderboard          # UC-58: daily cached leaderboard
GET    /api/v1/reports/exam-history         # UC-59: past test sessions (paged)
```

**SignalR Hub**
```
Hub:   /hubs/notification                    # JWT authenticated; real-time push
Method: ReceiveNotification(payload)         # Client-side event handler name
```

### Integration & Domain Events (Consumed)

| Event Source | Consumer | Notification Created |
|-------------|----------|---------------------|
| Grading (004) `GradeCalculatedEvent` | `GradeCalculatedConsumer` | "Your test has been graded" → Student |
| Gamification (007) `BadgeAwardedEvent` | `BadgeAwardedConsumer` | "You earned a badge!" → Student |
| Learning (006) `DiscussionQuestionPostedEvent` | `DiscussionQuestionPostedConsumer` | "Student asked a question" → Teacher |
| Learning (006) `DiscussionAnsweredEvent` | `DiscussionAnsweredConsumer` | "Teacher replied to your question" → Student |
| Identity (001) `ApplicationResolvedEvent` | `ApplicationResolvedConsumer` | "Your application was approved/rejected" → Teacher |
| Identity (001) `AccountCreatedEvent` | `AccountCreatedConsumer` | Welcome email (SMTP) |
| Gamification (007) `StreakReminderEvent` | `StreakReminderConsumer` | "Don't break your streak!" → Student |

### NotificationService Flow

```csharp
// NotificationService.SendAsync(accountId, title, content, link):
// 1. Insert Notification record into Notification
// 2. Get SignalR connectionId for accountId from Redis/Hub context
// 3. If connected: _hubContext.Clients.User(accountId).SendAsync("ReceiveNotification", payload)
// 4. If offline: payload stored in DB; client fetches on reconnect/page load
```

### Hangfire Scheduled Jobs

| Job | Cron | Action |
|-----|------|--------|
| `LeaderboardRecalculationJob` | `0 0 * * *` (00:00 daily) | Query `CompetencyPoint` for all students → sort → cache in Redis `ntf:leaderboard:{grade}` |
| `NotificationPruneJob` | `0 1 * * *` (01:00 daily) | DELETE FROM `Notification` WHERE `CreatedTime` older than 90 days |

### Report Query Data Sources

```
GET /api/v1/reports/competency:
  → JOIN CompetencyPoint ON StudentID
  → JOIN TagsMastery ON StudentID GROUP BY Grade
  → Return: overall point, weak count, mastered count

GET /api/v1/reports/heatmap:
  → TagsMastery WHERE StudentID = current
  → Return matrix: tag_name × difficulty_name → mastery_status + accuracy_rate

GET /api/v1/reports/leaderboard:
  → Redis key ntf:leaderboard:{grade} (set by Hangfire job)
  → Fallback: live query if Redis miss

GET /api/v1/reports/exam-history:
  → TestSession WHERE StudentID + Status IN submitted/graded states
  → ORDER BY start_time DESC; PAGE
```

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests:
   - SignalR hub connects with valid JWT → `ReceiveNotification` fires on event.
   - `GradeCalculatedEvent` → Notification record created + SignalR push.
   - Offline student → Notification persisted; delivered on GET /notifications.
   - UC-55: Competency report aggregation returns correct data.
   - UC-57: Heatmap returns correct matrix for 5 tags × 3 difficulties.
   - UC-58: Leaderboard returns cached data after Hangfire job.
   - Prune job: Notifications > 90 days deleted.
   - Mark read: `is_read = true` after PUT.
