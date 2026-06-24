# Feature Specification: Identity & Access Module

**Feature Branch**: `001-identity-access`

**Created**: 2026-06-23

**Status**: Approved

**Input**: User description: "Handles authentication, Google OAuth integration, account lifecycle management (CRUD & bulk excel import), role and permission controls, and teacher applications verification."

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

- **UC-01: Login**
- **UC-02: Logout**
- **UC-03: Change Password**
- **UC-04: View Profile**
- **UC-05: Update Profile**
- **UC-06: Reset Password**
- **UC-07: Login by Google**
- **UC-08: Register Teacher Account**
- **UC-09: View Account List**
- **UC-10: View Teacher Application**
- **UC-11: Create Account Manually**
- **UC-12: Import from Excel**
- **UC-13: Update Account**
- **UC-14: Activate / Deactivate Account**
- **UC-15: Approve / Reject Application**
- **UC-16: Adjust Permission**
- **UC-17: Update Role**
- **UC-39: Student Register Account**

### Edge Cases

- Database integrity: Handled via soft deletes and transactional consistency checks.
- Empty fields: Handled via DTO validation constraints on APIs (e.g. Model validation).

## Requirements *(mandatory)*

### Functional Requirements

- **DC-01: Username and Email must be unique across the entire system. Duplicate registrations must return HTTP 409 Conflict.**
- **BR-01: Both Username and Password fields are required before login submission.**
- **BR-02: A Student account is restricted from logging in on multiple devices simultaneously; a new login terminates the previous session.**
- **BR-03: Account is locked for 15 minutes after 5 consecutive failed login attempts within 10 minutes.**
- **Teacher self-registration is locked (PENDING status) until Admin explicitly approves/verifies their application with certificate credentials.**
- **BR-04: Student and Expert accounts created through self-registration must confirm their email before they can log in or use the system. Unconfirmed self-registered accounts remain inactive until email confirmation succeeds.**
- **BR-05: Email confirmation may be implemented using a confirmation link/token flow and does not require a dedicated OTP table in the database schema.**

### Key Entities *(include if feature involves data)*

- **Account**:  account_id, username, password_hash, email, first_name, last_name, phone_number, date_of_birth, avatar_url, role_id, is_active, created_time
- **Expert**:  expert_id (FK -> Account), specialty
- **Student**:  student_id (FK -> Account), gender, school, current_grade
- **Teacher**:  teacher_id (FK -> Account), biography, is_verified
- **TeacherApplication**:  application_id, teacher_id (FK), documents_url, status (PENDING, APPROVED, REJECTED), review_comments, applied_time, reviewed_time, reviewed_by (FK)
- **Role**:  role_id, role_name, description
- **Permission**:  permission_id, permission_key, description
- **RolePermission**:  role_id (FK), permission_id (FK)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All core API endpoints must return successful response within 2 seconds (NFR-P01).
- Schema isolation per module is enforced under the `usr` namespace.

## Assumptions

- Target database is SQL Server.
- MediatR event handling provides decoupled async integration.
