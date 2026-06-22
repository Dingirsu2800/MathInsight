# Data Model: Identity & Access Management

Physical database design for tables related to accounts, roles, permissions, and teacher applications.

## Entity Definitions

### Table: `roles`
Stores role classifications.
- `role_id` (VARCHAR(36), PK): String identifier (e.g., `'r-admin'`, `'r-student'`).
- `role_name` (VARCHAR(50), NOT NULL, UNIQUE): Name of the role.
- `description` (VARCHAR(255), NULLABLE): Description of role privileges.

### Table: `permissions`
Granular system permissions.
- `permission_id` (VARCHAR(36), PK): Unique permission identifier.
- `permission_name` (VARCHAR(100), NOT NULL, UNIQUE): Name of the permission.
- `description` (VARCHAR(255), NULLABLE): Description of the permission.

### Table: `role_permissions`
Join table mapping permissions to roles.
- `role_id` (VARCHAR(36), PK, FK -> `roles.role_id`)
- `permission_id` (VARCHAR(36), PK, FK -> `permissions.permission_id`)

### Table: `accounts`
Core user credentials.
- `account_id` (VARCHAR(36), PK): Generated GUID.
- `username` (VARCHAR(50), NOT NULL, UNIQUE): Login username.
- `password_hash` (VARCHAR(255), NOT NULL): BCrypt hash.
- `email` (VARCHAR(100), NOT NULL, UNIQUE): Email address.
- `first_name` (VARCHAR(50), NOT NULL)
- `last_name` (VARCHAR(50), NOT NULL)
- `phone_number` (VARCHAR(20), NULLABLE)
- `date_of_birth` (DATE, NULLABLE)
- `avatar_url` (VARCHAR(255), NULLABLE)
- `role_id` (VARCHAR(36), NOT NULL, FK -> `roles.role_id`)
- `is_active` (BOOLEAN, NOT NULL, DEFAULT TRUE)
- `created_time` (TIMESTAMP, NOT NULL, DEFAULT CURRENT_TIMESTAMP)
- `lockout_end` (TIMESTAMP, NULLABLE): Lockout expiry time.
- `failed_attempts` (INT, NOT NULL, DEFAULT 0): Failed sign-in count.

### Table: `students`
Student-specific profile extensions.
- `student_id` (VARCHAR(36), PK, FK -> `accounts.account_id`)
- `gender` (VARCHAR(20), NULLABLE)
- `school` (VARCHAR(100), NULLABLE)
- `current_grade` (INT, NOT NULL): 10, 11, or 12.

### Table: `teachers`
Teacher-specific profile extensions.
- `teacher_id` (VARCHAR(36), PK, FK -> `accounts.account_id`)
- `department` (VARCHAR(100), NULLABLE)

### Table: `teacher_applications`
Holds teacher verification records.
- `application_id` (VARCHAR(36), PK)
- `teacher_id` (VARCHAR(36), NOT NULL, FK -> `teachers.teacher_id`)
- `certificate_url` (VARCHAR(255), NOT NULL): URL path to uploaded certificate PDF/Image.
- `status` (VARCHAR(20), NOT NULL, DEFAULT 'PENDING'): Allowed: `PENDING`, `APPROVED`, `REJECTED`.
- `rejection_reason` (VARCHAR(255), NULLABLE)
- `created_time` (TIMESTAMP, NOT NULL, DEFAULT CURRENT_TIMESTAMP)

## Indexing Strategy
- **Unique Indexes**:
  - `accounts(username)` (UNIQUE, BTREE) - Quick login checks.
  - `accounts(email)` (UNIQUE, BTREE) - Uniqueness verification on registration.
- **Foreign Key Indexes**:
  - `accounts(role_id)` (BTREE) - User role join.
  - `teacher_applications(teacher_id)` (BTREE) - Admin pending verification lists.
