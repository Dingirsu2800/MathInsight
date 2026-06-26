# Tasks Checklist: Gamification Module

**Branch**: `007-gamification` | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

---

## Phase 1: Persistence Setup

- [ ] Create EF `IEntityTypeConfiguration` for 5 entities under `gam` schema:
  - [ ] `BadgeConfiguration` — UNIQUE `badge_name`; `condition_type` enum
  - [ ] `StudentBadgeConfiguration` — composite PK `(student_id, badge_id)`; no Update/Delete operations configured
  - [ ] `StudyStreakConfiguration` — UNIQUE `student_id` (1:1 per student); default `current_streak = 0`
  - [ ] `TargetScoreConfiguration` — UNIQUE `(student_id, tag_id)`; CHECK `target_point` in [0, 10] (DC-04)
  - [ ] `ActivityLogConfiguration` — no soft-delete, no update (BR-40); index `(student_id, activity_date)`
- [ ] Create `GamificationDbContext.cs` with shared connection, `gam` schema default
- [ ] Add EF migration: `dotnet ef migrations add Init_Gamification --project MathInsight.WebAPI`
- [ ] Seed badges: at minimum 3 badges (TOTAL_CORRECT_ANSWERS: 10, STREAK_DAYS: 7, TESTS_COMPLETED: 5)

---

## Phase 2: Core Domain Logic

- [ ] **ActivityLoggedConsumer** (handles `ActivityLoggedEvent` from Learning module):
  - [ ] Insert `ActivityLog` record (append-only — no WHERE, no UPDATE, no DELETE) (BR-40)
  - [ ] Call `StreakService.UpdateStreakAsync()` with activity qualifying criteria (BR-39)
  - [ ] Call `BadgeService.CheckAndAwardBadgesAsync()`

- [ ] **TestSubmittedConsumer** (handles `TestSubmittedEvent` from Testing module):
  - [ ] Insert `ActivityLog` with `activity_type = PRACTICE` or `EXAM`
  - [ ] `duration_seconds = TestSession.duration`
  - [ ] Call `StreakService.UpdateStreakAsync()` — PRACTICE/EXAM always qualifies
  - [ ] Call `BadgeService.CheckAndAwardBadgesAsync()`

- [ ] **StreakService**:
  - [ ] `UpdateStreakAsync(studentId, activityDate, activityType, durationSeconds)`:
    - `VIEW_LECTURE` qualifies only if `durationSeconds >= 300` (BR-39)
    - `DOWNLOAD_MATERIAL` → insert log but do NOT update streak
    - Get or create `StudyStreak` for student
    - Date comparison logic: yesterday → `current_streak++`; gap > 1 day → `current_streak = 1` (reset, BR-41)
    - Update `longest_streak` if exceeded (BR-42)
    - Update `last_activity_date = activityDate`

- [ ] **BadgeService**:
  - [ ] `CheckAndAwardBadgesAsync(studentId)`:
    - Fetch all unearned badges
    - For `TOTAL_CORRECT_ANSWERS`: cross-read count from `tst.test_answers` where `is_correct = true`
    - For `STREAK_DAYS`: read `StudyStreak.current_streak`
    - For `TESTS_COMPLETED`: cross-read count from `tst.test_sessions` where `status = GRADED`
    - If condition met AND `StudentBadge` not exists → insert (composite PK prevents duplicates)
    - Publish `BadgeAwardedEvent` → Notification module

- [ ] **Target Score Commands**:
  - [ ] `SetTargetScoreCommand` (UC-85): validate `target_point` in [0, 10]; UNIQUE `(student_id, tag_id)` → 409 if duplicate
  - [ ] `UpdateTargetScoreCommand` (UC-86): validate `target_point` in [0, 10]; validate ownership

- [ ] **Queries**:
  - [ ] `GetStreakQuery` (UC-81): return `current_streak`, `longest_streak`, `last_activity_date`
  - [ ] `GetBadgeListQuery` (UC-82): all badges + `earned = true/false` per student
  - [ ] `GetBadgeProgressQuery` (UC-84): for each unearned badge, calculate progress % vs condition_value
  - [ ] `GetTargetProgressQuery` (UC-87): `TargetScore` list + cross-read `rcm.competency_points` for current vs target

- [ ] **Hangfire Scheduled Job** (streak reminder):
  - [ ] Register job: `0 13 * * *` (UTC, = 20:00 VN ICT)
  - [ ] Query students with no `ActivityLog` for today → publish `StreakReminderEvent` → Notification module

---

## Phase 3: Controller and Routing

- [ ] `GamificationController` — StudentOnly:
  - [ ] `GET /api/v1/gamification/streak`
  - [ ] `GET /api/v1/gamification/badges`
  - [ ] `GET /api/v1/gamification/badges/progress`
  - [ ] `POST /api/v1/gamification/targets`
  - [ ] `PUT /api/v1/gamification/targets/{id}`
  - [ ] `GET /api/v1/gamification/targets`
- [ ] Register inside `GamificationModuleExtensions.cs`:
  - DbContext, StreakService, BadgeService, MediatR consumers, Hangfire job

---

## Phase 4: Verification

- [ ] `dotnet build` — zero compile errors
- [ ] Integration tests (xUnit):
  - [ ] `ActivityLoggedEvent`: VIEW_LECTURE 6 min → streak updated; VIEW_LECTURE 3 min → streak NOT updated
  - [ ] `TestSubmittedEvent`: PRACTICE → ActivityLog with type=PRACTICE, streak updated
  - [ ] Streak: 3 consecutive days → `current_streak = 3`
  - [ ] Streak broken: gap 2 days → `current_streak = 1`
  - [ ] UC-83: 100 correct answers → badge awarded, `StudentBadge` created
  - [ ] UC-83: Second trigger for same badge → no duplicate insert (composite PK)
  - [ ] UC-85: Set target `target_point = 7` for Algebra tag → created
  - [ ] UC-85: Set second target for same tag → 409 (UNIQUE constraint)
  - [ ] UC-86: Update `target_point = 11` → 400 (DC-04)
  - [ ] BR-40: Attempt UPDATE on ActivityLog → not allowed (ORM config check)