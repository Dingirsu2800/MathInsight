# Feature Specification: Gamification System

**Feature Branch**: `[specs/007-gamification]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Study Streak Tracking (Priority: P1)
As a Student, I want the system to track my consecutive daily learning activities (streaks) so that I stay motivated to study math every day.
**Why this priority**: Major driver of student engagement. Encourages daily practice.
**Independent Test**: Logging activity (completing test or viewing lecture) updates the streak. JOB-02 resets missed streaks at midnight.
**Acceptance Scenarios**:
1. **Given** a Student whose last activity was yesterday and current streak is 5, **When** they complete a study activity today, **Then** their CurrentStreak increments to 6 (UC-80).
2. **Given** a student has already completed an activity today, **When** they complete another activity, **Then** the streak remains at 6 (AF-01).
3. **Given** a student with streak 6 does not study today, **When** the clock hits midnight, **Then** JOB-02 runs and resets their CurrentStreak to 0.

---

### User Story 2 - Achievement Badges (Priority: P2)
As a Student, I want to earn badges automatically for completing milestones (e.g. 10 tests taken, 7-day streak) so that I feel rewarded for my achievements.
**Why this priority**: Increases gamification value. Gives students concrete milestones to target.
**Independent Test**: Completing the 10th test automatically awards the "Decathlon learner" badge and triggers a notification.
**Acceptance Scenarios**:
1. **Given** a Student has completed 9 tests, **When** they submit their 10th test, **Then** background job JOB-03 triggers, awards the "Decathlon" badge, and pushes a badge notification.
2. **Given** a Student views their profile, **When** they open the Badge Gallery, **Then** they see their earned badges highlighted and locked ones greyed out (UC-82).

---

### User Story 3 - Target Score Planning (Priority: P2)
As a Student, I want to set and monitor target scores for specific mathematical topics so that I can align my practice tests with my exam goals.
**Why this priority**: Helps students set learning goals. Connects targets with competency scores.
**Independent Test**: Student sets a target score of 8.0/10.0 for Integration, and system displays progress.
**Acceptance Scenarios**:
1. **Given** Student is on Target Score Planner, **When** they select 'Calculus' and enter a target score of 8.5, **Then** the target is saved, and progress is shown compared to their current Calculus Ptag.
2. **Given** a student has an active target score, **When** they submit a test, **Then** the system pushes auto-generated score suggestions to help them meet their goals (UC-88).

### Edge Cases

- **Streak Increment Once Daily**: Multiple activities in one day must not increment the streak count multiple times (BR-34, UC-80 AF-01).
- **Target score boundaries**: Target scores must fall strictly within the range of 0.0 to 10.0 (DC-04).
- **Single active target profile**: A student may maintain only one active target score profile per subject at any given time (GBR-12).
- **Badge Immutability**: Earned badges must be permanently locked to the student profile and must not be revoked (GBR-11).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST record student activities (viewing lecture for 5+ mins, submitting test) to trigger streak evaluations (UC-80).
- **FR-002**: System MUST increment streak if last activity was exactly 1 day ago, reset to 1 if older, and skip if same day (BR-34, BR-35).
- **FR-003**: System MUST run a daily background job (JOB-02) at 00:00 AM to reset missed study streaks to 0 and push reminders.
- **FR-004**: System MUST store a catalog of badges, conditions, and icons (UC-82).
- **FR-005**: System MUST run an event-driven background job (JOB-03) to evaluate badge criteria and auto-award rewards (UC-83).
- **FR-006**: Student MUST be able to browse their badges and track progress percentages for locked achievements (UC-82, UC-84).
- **FR-007**: Student MUST be able to set, update, and monitor target scores (0.0 to 10.0 range) for specific knowledge tags (UC-85 to UC-87).
- **FR-008**: System MUST enforce that students only have one active target profile per subject at a time (GBR-12).
- **FR-009**: System MUST push score suggestions and tips based on target goals after test submissions (UC-88).

### Key Entities
- **StudyStreak**: Current streak, longest streak, last activity date, and student ID.
- **Badge**: Badge details, rules, and icon URLs.
- **StudentBadge**: Records linking awarded badges to students.
- **TargetScore**: Planned target scores per topic tag.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Streak evaluations during study activity logging must complete in under 100ms.
- **SC-002**: Badge award evaluations (JOB-03) must complete within 2 seconds of the triggering activity.
- **SC-003**: Streak Sweeper (JOB-02) must process all inactive users at midnight in under 5 minutes.

## Assumptions

- System server time is synchronized with local timezone for accurate midnight resets.
