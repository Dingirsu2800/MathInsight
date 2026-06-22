# Feature Specification: Notification, Report & Leaderboard

**Feature Branch**: `[specs/008-notification-report]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Real-time System Notifications (Priority: P1)
As a Student, Teacher, or Expert, I want to receive real-time notifications for critical events (test graded, badge earned, comment replied) so that I stay informed.
**Why this priority**: Essential communication layer. Keeps users engaged by informing them of system updates and interactions.
**Independent Test**: Posting a reply to a student's question pushes a notification visible in their notification center.
**Acceptance Scenarios**:
1. **Given** a Student completes a test, **When** grading finishes, **Then** a test result notification is pushed to their dashboard (UC-89).
2. **Given** a user has unread notifications, **When** they click 'Mark as Read', **Then** the notification status changes, and the badge count decreases.

---

### User Story 2 - Global Competency Leaderboard (Priority: P2)
As a Student, I want to view a global ranking leaderboard of all students based on streak counts and overall competency scores so that I can gauge my standing.
**Why this priority**: Drives friendly competition. Encourages students to practice more to rank higher on the leaderboard.
**Independent Test**: Opening the leaderboard renders the top student listings sorted by Ptag and current streaks.
**Acceptance Scenarios**:
1. **Given** students have active competency points, **When** Student views the Leaderboard page, **Then** they see the list of top students sorted by overall mastery index.

### Edge Cases

- **Notification Spam**: System must prevent sending duplicate notifications for the same event trigger (e.g. double clicking submit).
- **Inactive Accounts in Rankings**: Inactive or deactivated accounts must be automatically excluded from the leaderboard calculations (GBR-02).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST create notifications automatically on critical business events (test graded, badge earned, replies posted, reminders) (GBR-13).
- **FR-002**: Users MUST be able to view their notifications, mark them as read, and see unread counts (UC-90).
- **FR-003**: System MUST push notifications via the dashboard and update counts in real-time (UC-89).
- **FR-004**: System MUST display a global leaderboard ranking students based on competency scores (Ptag) and active streaks (UC-58).
- **FR-005**: Leaderboard MUST automatically exclude inactive or locked user accounts (GBR-02).

### Key Entities
- **Notification**: Alert message, target user ID, status (Read/Unread), type, and link URL.
- **ActivityLog**: History of logged user actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Notifications must be created and available on the user dashboard in under 1 second of the event.
- **SC-002**: The Leaderboard query must resolve and display the top 50 students in under 1 second.

## Assumptions

- Browser clients support polling or WebSockets to display notifications in real-time.
