# Feature Specification: Identity & Access Module

**Feature Branch**: `001-identity-access`

**Created**: 2026-06-23 | **Updated**: 2026-06-26

**Status**: Approved

**Source Documents**: PRD §3, §4 (FT-01), UCS UC-01–UC-17, UC-39, TDS §3 (accounts, roles, permissions), §4

## User Scenarios & Testing *(mandatory)*

### Core Use Cases (Priority: P1)

| UC-ID | Name | Primary Actor |
|-------|------|---------------|
| UC-01 | Login | User (all roles) |
| UC-02 | Logout | User (all roles) |
| UC-03 | Change Password | User (all roles) |
| UC-04 | View Profile | User (all roles) |
| UC-05 | Update Profile | User (all roles) |
| UC-06 | Reset Password | User (all roles) |
| UC-07 | Login by Google | User (all roles) |
| UC-08 | Register Teacher Account | Guest |
| UC-09 | View Account List | Admin |
| UC-10 | View Teacher Application | Admin |
| UC-11 | Create Account Manually | Admin |
| UC-12 | Import from Excel | Admin |
| UC-13 | Update Account | Admin |
| UC-14 | Activate / Deactivate Account | Admin |
| UC-15 | Approve / Reject Application | Admin |
| UC-16 | Adjust Permission | Admin |
| UC-17 | Update Role | Admin |
| UC-39 | Student Register Account | Guest |

### Edge Cases

- **Duplicate registration**: Username or email already exists → return HTTP 409 Conflict.
- **Empty fields**: DTO validation via `[Required]`, `[MaxLength]` annotations — returns HTTP 400.
- **Expired email token**: Confirmation link expired → return HTTP 410 Gone; user must request new link.
- **Google OAuth conflict**: Google email matches existing local account → link accounts, do not create duplicate.
- **Admin unlocks locked account**: Resets failed login counter immediately.

## Requirements *(mandatory)*

### Functional Requirements

- **DC-01**: Username and Email must be unique across the entire system. Duplicate registrations must return HTTP 409 Conflict.
- **BR-01**: Both Username and Password fields are required before login submission.
- **BR-02**: A Student account is restricted from logging in on multiple devices simultaneously; a new login terminates the previous session via JWT blacklist in Redis.
- **BR-03**: Account is locked for **15 minutes** after **5 consecutive failed login attempts** within a **10-minute** window. Failure message is generic — does not reveal which field is wrong.
- **BR-04**: Student and Expert accounts created through self-registration must confirm their email before they can log in. Unconfirmed accounts remain `is_active = false` until email confirmation succeeds.
- **BR-05**: Email confirmation is implemented using a time-limited token (GUID-based link). No dedicated OTP table required — token stored temporarily in Redis or embedded in JWT claim.
- **BR-06**: Teacher self-registration sets the `TeacherApplication.status = PENDING`. Account remains locked (`is_active = false`) until Admin explicitly approves.
- **BR-07**: Google OAuth callback must complete and redirect within **3 seconds** (NFR-AC-FT01-02).
- **BR-08**: Password must be minimum **8 characters**, maximum **128 characters**. Stored as BCrypt hash (strength 12). Plaintext storage is strictly prohibited.
- **BR-09**: JWT token contains: `account_id`, `role`, `email`. Token validated by YARP Gateway on every request.
- **BR-10**: On logout, the JWT is immediately blacklisted in Redis to prevent reuse.

### Key Entities *(include if feature involves data)*

- **Account**: `account_id` (VARCHAR 36, PK), `username` (VARCHAR 50, UNIQUE), `password_hash` (VARCHAR 255), `email` (VARCHAR 100, UNIQUE), `first_name`, `last_name`, `phone_number`, `date_of_birth`, `avatar_url`, `role_id` (FK → roles), `is_active` (BOOLEAN, DEFAULT TRUE), `created_time`
- **Expert**: `expert_id` (PK, FK → accounts), `specialty`
- **Student**: `student_id` (PK, FK → accounts), `gender`, `school`, `current_grade`
- **Teacher**: `teacher_id` (PK, FK → accounts), `biography`, `is_verified` (BOOLEAN)
- **TeacherApplication**: `application_id`, `teacher_id` (FK), `documents_url`, `status` (**PENDING** → **APPROVED** | **REJECTED**), `review_comments`, `applied_time`, `reviewed_time`, `reviewed_by` (FK → accounts)
- **Role**: `role_id`, `role_name` (UNIQUE), `description`
- **Permission**: `permission_id`, `permission_key` (UNIQUE), `description`
- **RolePermission**: `role_id` (FK), `permission_id` (FK) — composite PK

### Enums & Lookup Values

| Entity | Field | Allowed Values |
|--------|-------|----------------|
| TeacherApplication | status | `PENDING`, `APPROVED`, `REJECTED` |
| Account | role_name | `Admin`, `Expert`, `Teacher`, `Student` |

### Permission Matrix (from PRD §3.2)

| Action | Admin | Expert | Teacher | Student | Guest |
|--------|-------|--------|---------|---------|-------|
| Login / Logout | Full | Full | Full | Full | No |
| Register Account | No | No | Full | Full | Full |
| Verify Teacher Credentials | Full | No | No | No | No |
| Deactivate Account | Full | No | No | No | No |
| Import Batch Accounts | Full | No | No | No | No |
| Adjust Permissions | Full | No | No | No | No |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- All auth API endpoints return successful responses within **2 seconds** (NFR-P01).
- Google OAuth callback completes within **3 seconds** (NFR-AC-FT01-02).
- JWT validation at YARP layer adds < 10ms overhead.
- Schema isolation enforced under `usr` namespace.
- All seed accounts (`admin`, `expert_01`, `teacher_01`, `student_01`, `student_02`) are testable end-to-end.

## Assumptions

- Target database is SQL Server; schema prefix is `usr`.
- Redis is available for JWT blacklist and email confirmation token storage.
- MediatR event handling provides decoupled async integration (e.g., `TeacherApplicationSubmittedEvent`).
- Google OAuth 2.0 credentials are injected via environment variables (`GoogleOAuth:ClientId`, `GoogleOAuth:ClientSecret`).
- BCrypt strength 12 is used for password hashing.
