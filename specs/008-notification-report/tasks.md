# Tasks Checklist: Notification & Report Module

**Branch**: `008-notification-report` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for `Notification` entity mapped to current DB script table:
  - [ ] `NotificationConfiguration` — FK to `accounts`; composite index `(account_id, is_read)`; index `created_time` (for prune)
- [ ] Create `NotificationDbContext.cs` with shared connection and explicit `ToTable(...)` mapping.
- [ ] Do not add EF migration unless the team explicitly switches from SQL script source-of-truth to EF migration source-of-truth.
- [ ] Seed: 3 test notifications per TDS §3.6

---

## Phase 2: Core Domain Logic

- [ ] **NotificationService**:
  - [ ] `SendAsync(accountId, title, content, link)`:
    - Insert `Notification` record into `Notification`
    - Push via SignalR `_hubContext.Clients.User(accountId).SendAsync("ReceiveNotification", payload)`
  - [ ] `MarkReadAsync(notificationId, accountId)` — validate ownership before update

- [ ] **NotificationHub** (`/hubs/notification`):
  - [ ] Inherit `Hub<INotificationClient>`
  - [ ] Require JWT authorization: `[Authorize]`
  - [ ] Map `account_id` from JWT claim to SignalR connection group on `OnConnectedAsync`

- [ ] **Domain Event Consumers** (MediatR `INotificationHandler`):
  - [ ] `GradeCalculatedConsumer` — title: "Test Graded", content: "{score}/10", link: `/sessions/{id}/solution`
  - [ ] `BadgeAwardedConsumer` — title: "Badge Earned! 🏆", content: "{badge_name}", link: `/badges`
  - [ ] `DiscussionQuestionPostedConsumer` — title: "New Question", content: "{student_name} asked...", link: `/lectures/{lectureId}`
  - [ ] `DiscussionAnsweredConsumer` — title: "Answer Received", content: "{teacher_name} replied", link: `/lectures/{lectureId}#discussion`
  - [ ] `ApplicationResolvedConsumer` — title: "Application {APPROVED|REJECTED}", content: review_comments, link: `/profile`
  - [ ] `AccountCreatedConsumer` — send welcome email via `EmailService.SendWelcomeAsync()`
  - [ ] `StreakReminderConsumer` — title: "🔥 Don't break your streak!", content: "Complete a lesson today"

- [ ] **Report Queries**:
  - [ ] `GetCompetencyReportQuery` (UC-55):
    - Cross-read `CompetencyPoint` for student grade scores
    - Cross-read `TagsMastery` aggregate
    - Return: `{ overallPoint, weakCount, learningCount, masteredCount, tagDetails[] }`
  - [ ] `GetPerformanceAnalysisQuery` (UC-56):
    - Cross-read `TestSession` WHERE `StudentID = current` AND `Status = Graded`
    - Last 30 days; group by date; return daily scores for chart
  - [ ] `GetCompetencyHeatmapQuery` (UC-57):
    - Cross-read `TagsMastery` by `(StudentID, TagID)` using `official_point` and `recommended_difficulty_level`
    - Return matrix: `{ tagName, mastery_status, official_point, recommended_difficulty_level }`
  - [ ] `GetLeaderboardQuery` (UC-58):
    - Read from Redis `ntf:leaderboard:{grade}`; fallback live query if cache miss
    - Return: `{ rank, studentName, grade, point }[]`
  - [ ] `GetExamHistoryQuery` (UC-59):
    - Cross-read `TestSession` WHERE `StudentID` AND `Status != InProgress`
    - ORDER BY `start_time DESC`; paged; include `test_name`, `score`, `start_time`, `test_format`

- [ ] **Mark Read Command**:
  - [ ] `MarkNotificationReadCommand` — validate `account_id = currentUserId`; set `is_read = true`

- [ ] **Hangfire Jobs**:
  - [ ] `LeaderboardRecalculationJob` — cron `0 0 * * *`:
    - Query all students' competency points from `CompetencyPoint`
    - Sort descending by `point`; assign rank
    - Cache in Redis `ntf:leaderboard:{grade}` (TTL 25 hours)
  - [ ] `NotificationPruneJob` — cron `0 1 * * *`:
    - DELETE FROM `Notification` WHERE `CreatedTime < DATEADD(day, -90, GETDATE())`

- [ ] **EmailService**:
  - [ ] `SendWelcomeAsync(email, firstName)`
  - [ ] `SendApplicationResultAsync(email, status, comments)` — for Teacher application result
  - [ ] Configure SMTP via `Email:Host`, `Email:Port`, `Email:User`, `Email:Password` env vars

---

## Phase 3: Controller and Routing

- [ ] `NotificationsController` — authenticated all roles:
  - [ ] `GET /api/v1/notifications` (paged, filter unread)
  - [ ] `PUT /api/v1/notifications/{id}/read`
- [ ] `ReportsController` — StudentOnly:
  - [ ] `GET /api/v1/reports/competency`
  - [ ] `GET /api/v1/reports/performance`
  - [ ] `GET /api/v1/reports/heatmap`
  - [ ] `GET /api/v1/reports/leaderboard`
  - [ ] `GET /api/v1/reports/exam-history`
- [ ] Register `NotificationHub` via `app.MapHub<NotificationHub>("/hubs/notification").RequireAuthorization()`
- [ ] Register inside `NotificationReportModuleExtensions.cs`:
  - DbContext, NotificationService, EmailService, SignalR, Hangfire jobs, all consumers

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] SignalR: connect with valid JWT → connection established; `ReceiveNotification` fires
  - [ ] `GradeCalculatedEvent` → Notification in DB + SignalR push
  - [ ] `BadgeAwardedEvent` → Notification with badge name and link
  - [ ] UC-55: Competency report aggregation returns accurate totals
  - [ ] UC-57: Heatmap returns correct matrix for student
  - [ ] UC-58: Leaderboard served from Redis after Hangfire job runs
  - [ ] UC-59: Exam history returns paginated sessions (no `InProgress`)
  - [ ] Prune job: Notifications 91 days old are deleted; 89-day-old retained
  - [ ] Mark read: `is_read = true`; attempt to mark another user's notification → 403
