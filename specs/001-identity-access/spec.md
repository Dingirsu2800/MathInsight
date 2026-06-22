# Feature Specification: Identity & Access Management

**Feature Branch**: `[specs/001-identity-access]`  
**Created**: 2026-06-22  
**Status**: Draft  
**Input**: User description: "From reports in C:\Users\Admin\Documents\ĐỒ ÁN\Report"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure User Authentication (Priority: P1)
As a Student, Teacher, Expert, or Admin, I want to securely log in and log out of the MathInsight platform so that I can access my personalized dashboard and data.
**Why this priority**: Core entry point of the platform. Authentication is necessary to protect user data, track study progress, and enforce permissions.
**Independent Test**: Can be verified by running the Login flow with valid credentials, verifying that a session token is created, and logging out to verify token invalidation.
**Acceptance Scenarios**:
1. **Given** a registered user exists with active status, **When** they enter correct credentials and submit, **Then** they are authenticated and redirected to their home dashboard.
2. **Given** an inactive user, **When** they attempt to login with valid credentials, **Then** the login is blocked and they see a message: "Your account has been deactivated. Please contact Admin."
3. **Given** an active session, **When** the user clicks logout, **Then** the session is destroyed on the server and they are redirected to the login page.

---

### User Story 2 - Self-Service Registration (Priority: P1)
As a Guest, I want to register as a Student or submit an application for a Teacher account so that I can participate in the system.
**Why this priority**: Enables growth of the user base. Allows students to start learning immediately and teachers to apply to contribute.
**Independent Test**: Registering a student sends a confirmation email. Registering a teacher creates a PENDING application visible to the Admin.
**Acceptance Scenarios**:
1. **Given** a Guest registers as a Student, **When** they submit a unique email and valid passwords, **Then** a verification email is sent, and clicking the link activates the Student account.
2. **Given** a Guest registers as a Teacher, **When** they submit personal details and upload certificate files (PDF/JPG/PNG <= 10MB), **Then** their account is created in PENDING status, and Admin is notified.

---

### User Story 3 - User Administration and Access Control (Priority: P2)
As an Admin, I want to view, search, import, activate/deactivate user accounts, and adjust roles/permissions so that I can keep the platform secure and organized.
**Why this priority**: Vital for system operations, managing teacher approvals, auditing accounts, and bulk-importing students from high school class lists.
**Independent Test**: Admin can filter user list, toggle active status, and import batch student records from an Excel file (.xlsx).
**Acceptance Scenarios**:
1. **Given** Admin is on User Management list, **When** they upload a valid student Excel template, **Then** accounts are created, and an import summary is shown (imported vs skipped).
2. **Given** a PENDING teacher application, **When** Admin clicks 'Approve', **Then** the teacher's status changes to ACTIVE, and they receive an approval email.
3. **Given** Admin is on Role settings, **When** they toggle a permission for a role, **Then** it takes effect immediately for all active sessions of that role.

### Edge Cases

- **Duplicate Credentials**: System must reject duplicate emails or usernames with HTTP 409 Conflict (DC-01).
- **Brute Force Protection**: Account must be locked out temporarily after 5 consecutive failed login attempts (JOB-01, TDS §4.4).
- **Self-Deactivation**: Admins must be prevented from deactivating their own account (UC-14).
- **Invalid Certificate Formats**: Teacher registration must reject files other than PDF, JPG, PNG or files larger than 10MB (UC-08).

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST support local credential login (Username/Password) and 1-click Google OAuth 2.0 Single Sign-On (UC-01, UC-07).
- **FR-002**: Password verification MUST use secure hashed comparisons (bcrypt/PBKDF2); plain-text passwords must never be stored (BR-02).
- **FR-003**: System MUST support Student self-registration with email verification and Teacher self-registration with mandatory credential upload (UC-39, UC-08).
- **FR-004**: Admin MUST be able to view, search, filter, and manually create/update user accounts (UC-09, UC-11, UC-13).
- **FR-005**: Admin MUST be able to import multiple accounts from an Excel (.xlsx) file, generating a summary report upon completion (UC-12).
- **FR-006**: Admin MUST be able to approve/reject teacher applications and activate/deactivate user accounts (UC-10, UC-15, UC-14).
- **FR-007**: Admin MUST be able to manage roles and toggle permissions on roles, taking immediate effect (UC-16, UC-17).
- **FR-008**: User profile management MUST validate email format and telephone number format before updating (UC-05).
- **FR-009**: Student sessions MUST be unique per student; a new login must terminate any existing active session for that student (BR-02 in UC-07).

### Key Entities
- **Account**: Base user record containing AccountID, Username, PasswordHash, Email, RoleID, Status, and timestamps.
- **TeacherApplication**: Holds certificate files and review logs for teacher self-registrations.
- **Student**: Profile extension holding student-specific learning stats.
- **Teacher**: Profile extension holding teacher-specific material details.
- **Role & Permission**: Configures access permissions mapped to system functions.

## Success Criteria *(mandatory)*

### Measurable Outcomes
- **SC-001**: Verification emails and reset links must be delivered to the SMTP server within 5 seconds of the user request.
- **SC-002**: Login validation and session token generation must complete in under 1 second.
- **SC-003**: Accounts locked due to failed attempts must be automatically unlocked by JOB-01 exactly 10 minutes after lockout.

## Assumptions

- Existing SMTP server is accessible for email delivery.
- Google OAuth developer credentials will be provided.
- High school class list exports follow the provided Excel schema template.
