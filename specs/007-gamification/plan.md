# Implementation Plan: Gamification Module

**Branch**: `007-gamification` | **Date**: 2026-06-23 | **Updated**: 2026-06-26
**Spec**: [spec.md](spec.md)

## Summary

Builds `MathInsight.Modules.Gamification` managing study streaks, badge awards, target scores, and insert-only activity logs. Consumes `ActivityLoggedEvent` (from Learning) and `TestSubmittedEvent` (from Testing) via MediatR.

## Technical Context

| Property | Value |
|----------|-------|
| Language | C# / .NET 10.0 |
| Primary Dependencies | MediatR, EF Core, MassTransit |
| Storage | SQL Server; map to current DB script tables |
| Scheduler | Hangfire (optional: daily streak reminder check) |
| Testing | xUnit / Integration tests |
| Project Type | Modular Monolith Web API |

## Project Structure

```text
src/MathInsight.Modules.Gamification/
├── Consumers/
│   ├── ActivityLoggedConsumer.cs    # MediatR: handle ActivityLoggedEvent → log + streak + badges
│   └── TestSubmittedConsumer.cs     # MediatR: handle TestSubmittedEvent → log PRACTICE/EXAM activity
├── Services/
│   ├── IStreakService.cs
│   ├── StreakService.cs             # Calculate, update, reset streak
│   ├── IBadgeService.cs
│   └── BadgeService.cs             # Check badge conditions, award StudentBadge
├── Commands/
│   ├── SetTargetScore/             # UC-85: create TargetScore record
│   └── UpdateTargetScore/          # UC-86: update existing TargetScore
├── Queries/
│   ├── GetStreak/                  # UC-81: current_streak, longest_streak, last_activity_date
│   ├── GetBadgeList/               # UC-82: all badges + StudentBadge earned status
│   ├── GetBadgeProgress/           # UC-84: progress toward each unearned badge
│   └── GetTargetProgress/          # UC-87: target_point vs current competency per tag
├── Persistence/
│   ├── GamificationDbContext.cs    # maps to current DB script table names
│   ├── Configurations/
│   │   ├── BadgeConfiguration.cs
│   │   ├── StudentBadgeConfiguration.cs   # Composite PK; immutable (no update/delete)
│   │   ├── StudyStreakConfiguration.cs     # UNIQUE student_id (1:1)
│   │   ├── TargetScoreConfiguration.cs    # UNIQUE (student_id, tag_id)
│   │   └── ActivityLogConfiguration.cs    # Insert-only; no PK for update/delete
│   └── Migrations/
├── Controllers/
│   └── GamificationController.cs
└── GamificationModuleExtensions.cs
```

## Proposed Changes

### Database Layer (Current DB Script Tables)

| Table | Key Constraints |
|-------|----------------|
| `Badge` | Unique badge name; condition type check from DB script |
| `StudentBadge` | Composite PK `(StudentID, BadgeID)`; insert-only |
| `StudyStreak` | Student streak state |
| `TargetScore` | Target per `(StudentID, TagID)`; target point in [0, 10] |
| `ActivityLog` | Insert-only activity history |

### Service & API Gateway — REST Endpoints

**Student (StudentOnly policy)**
```
GET    /api/v1/gamification/streak           # UC-81: current + longest streak
GET    /api/v1/gamification/badges           # UC-82: all badges (earned/locked)
GET    /api/v1/gamification/badges/progress  # UC-84: % progress per locked badge
POST   /api/v1/gamification/targets          # UC-85: set target (UNIQUE per tag)
PUT    /api/v1/gamification/targets/{id}     # UC-86: update target_point
GET    /api/v1/gamification/targets          # UC-87: target vs competency (cross-read CompetencyPoint)
```

### Integration & Domain Events (Consumed)

| Event | Source | Action |
|-------|--------|--------|
| `ActivityLoggedEvent` (VIEW_LECTURE, DOWNLOAD_MATERIAL) | Learning (006) | Insert `ActivityLog`; update streak; check badges |
| `TestSubmittedEvent` (PRACTICE/EXAM) | Testing (003) | Insert `ActivityLog`; update streak; check badges |

### Streak Update Logic

```csharp
// StreakService.UpdateStreakAsync(studentId, activityDate, activityType, durationSeconds):
// 1. Get or create StudyStreak for student
// 2. Check if activity qualifies (BR-39):
//    - PRACTICE or EXAM → qualifies
//    - VIEW_LECTURE → qualifies only if duration_seconds >= 300 (5 min)
//    - DOWNLOAD_MATERIAL → does NOT qualify
// 3. If qualifies AND last_activity_date < today:
//    - If last_activity_date == yesterday → current_streak++
//    - If last_activity_date < yesterday → current_streak = 1 (reset, BR-41)
//    - Update last_activity_date = today
//    - If current_streak > longest_streak → update longest_streak (BR-42)
// 4. If already logged today → skip (no double-count)
```

### Badge Check Logic

```csharp
// BadgeService.CheckAndAwardBadgesAsync(studentId):
// Fetch all badges not yet earned by student
// For each badge:
//   if condition_type == TOTAL_CORRECT_ANSWERS:
//     count = SELECT COUNT(*) FROM TestAnswer WHERE student answers + IsCorrect=true
//     if count >= condition_value → award
//   if condition_type == STREAK_DAYS:
//     if StudyStreak.current_streak >= condition_value → award
//   if condition_type == TESTS_COMPLETED:
//     count = SELECT COUNT(*) FROM TestSession WHERE student + Status=Graded
//     if count >= condition_value → award
// Insert StudentBadge if not exists; publish BadgeAwardedEvent → Notification
```

### Scheduled Jobs (Hangfire)

| Job | Schedule | Purpose |
|-----|----------|---------|
| Streak Reminder Check | Daily `0 13 * * *` (20:00 VN time) | Send reminder to students without activity today (BR-45) |

## Verification Plan

1. `dotnet build` — zero compile errors.
2. EF mappings point to current DB script tables. Do not add EF migration unless the team switches source-of-truth from SQL script to EF migrations.
3. Integration tests (xUnit):
   - `ActivityLoggedEvent` → `ActivityLog` record inserted (insert-only, no update possible).
   - UC-81: View streak → `current_streak = 3` after 3 consecutive days.
   - Streak broken (gap 2 days) → `current_streak = 0`, `longest_streak` unchanged.
   - UC-83: 100 correct answers → "Algebra Warrior" badge awarded; duplicate award blocked.
   - UC-85: Set target for `tag_id` = Algebra → created.
   - UC-85: Set second target for same `tag_id` → 409 (UNIQUE constraint).
   - UC-86: Update target_point = 11 → 400 (DC-04).
   - UC-87: Target progress shows correct vs `CompetencyPoint`.
