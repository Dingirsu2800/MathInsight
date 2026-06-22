# Quickstart Guide: Identity & Access Management

This guide provides steps for setting up, running, and testing the Identity & Access module locally.

## Prerequisite Configurations

1. **Local environment variables (`.env` file in root)**:
   ```env
   DB_CONNECTION_STRING="Server=localhost;Database=MathInsight;User Id=sa;Password=YourPassword123;"
   REDIS_CONNECTION_STRING="localhost:6379"
   JWT_SECRET_KEY="SuperSecretKeyThatMustBeAtLeast32BytesLong"
   GOOGLE_CLIENT_ID="google-client-id-here"
   GOOGLE_CLIENT_SECRET="google-client-secret-here"
   ```

2. **Database Migrations**:
   Run the following commands to create and apply Entity Framework migrations for the Identity module:
   ```bash
   dotnet ef migrations add InitialIdentitySetup --project src/MathInsight.WebAPI --startup-project src/MathInsight.WebAPI
   dotnet ef database update --project src/MathInsight.WebAPI
   ```

3. **Seeding Admin/Expert Accounts**:
   Ensure `src/Host/Db/Seed/data.sql` is executed on startup or manually to seed default users:
   - Admin: `admin` / `Test@1234`
   - Expert: `expert_01` / `Test@1234`
   - Student: `student_01` / `Test@1234`

## Local Test Verification

1. **Authentication API Check**:
   Use curl or Postman to request a token:
   ```bash
   curl -X POST http://localhost:5000/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username": "admin", "password": "Test@1234"}'
   ```
   **Expected Response**:
   ```json
   {
     "token": "eyJhbGciOi...",
     "role": "Admin",
     "username": "admin"
   }
   ```

2. **Accessing Protected Routes**:
   Verify JWT access on User List endpoint:
   ```bash
   curl -H "Authorization: Bearer <your_jwt_token>" \
     http://localhost:5000/api/v1/admin/accounts
   ```
