# Actionable Tasks: Identity & Access Management

This task list guides the incremental implementation and test-driven verification of the Identity & Access module.

- [ ] **Infrastructure Setup**
  - [ ] Configure database connection settings and JWT token validation configurations in `Program.cs`
  - [ ] Add Redis connection strings and initialize JWT Blacklist cache middleware

- [ ] **Database & Models (Phase 1)**
  - [ ] Create EF Core entities for `Account`, `Role`, `Permission`, `Student`, `Teacher`, `TeacherApplication` in `MathInsight.Modules.Identity_Access/Models/`
  - [ ] Define DB relationships and seed data script `data.sql`
  - [ ] Generate and apply Entity Framework migrations for the database schema

- [ ] **Authentication Services (Phase 2)**
  - [ ] Implement password hashing logic (BCrypt strength 12)
  - [ ] Implement `TokenService` to sign JWT tokens with configured secret, expiration, and user role claims
  - [ ] Implement local credential verification and Google OAuth token validation
  - [ ] Implement account lockout state logic (failed login count increments, 15m expiration lockout timestamp check)
  - [ ] Implement Student token concurrency blacklist lookup in Redis

- [ ] **Controllers & Routing (Phase 2)**
  - [ ] Create `AuthController.cs` exposing routes: `/login`, `/logout`, `/register`, `/google`, `/reset-password`, `/change-password`
  - [ ] Create `AdminController.cs` exposing routes: `/admin/accounts`, `/admin/accounts/{id}/status`, `/admin/applications/{id}`
  - [ ] Configure role policies (`AdminOnly`, `ExpertOnly`, `TeacherOnly`, `StudentOnly`) using `[Authorize(Policy = "...")]`

- [ ] **Frontend Screens (Phase 2)**
  - [ ] Develop student sign-in/registration forms with inline validation triggers (email format, password strength)
  - [ ] Develop Teacher Registration form with certificates attachment upload inputs (only PDF, JPG, PNG <= 10MB)
  - [ ] Develop Admin dashboard panels for account listing, lock/unlock toggle switch controls, and teacher applications review list

- [ ] **Verification Checks**
  - [ ] Run xUnit integration test suite verifying user login, password lockout, and role adjustment permissions
  - [ ] Validate file upload validations reject files > 10MB or not in PDF/PNG format
