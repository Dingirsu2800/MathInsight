# Lecture Difficulty Recommendation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add difficulty-aware, explainable lecture recommendations for all mastery levels, with a safe lower-level fallback and grade-based cold start.

**Architecture:** `Learning_Lecture` owns lecture difficulty assignment and validation. `Recommender` reads the shared SQL tables through migration-excluded read models, calculates deterministic recommendations on demand, and keeps the existing student route. Frontend work is isolated in a separate Antigravity handoff after backend contracts are stable.

**Tech Stack:** .NET 10, ASP.NET Core, MediatR, EF Core, SQL Server, xUnit, React JavaScript, Vite, Tailwind CSS.

## Global Constraints

- Database source of truth is `Implementation/Database/database/001_Create_MathInsight_Azure.sql`, not the stale `Implementation/MathInsight/database` copy.
- Do not create Recommender-owned copies of `Lecture`, `TagTopic`, or `TagDifficulty` tables.
- Cross-module read models must use `ExcludeFromMigrations()`.
- Existing databases add nullable `Lecture.DifficultyID`; fresh databases use `NOT NULL`.
- Do not assign an arbitrary difficulty to existing lectures.
- Exact difficulty is preferred, then the nearest lower level; never recommend above the target level.
- Personalized evidence requires `TagsMastery.NumberDone >= 3`.
- Return at most two lectures per topic and six lectures overall.
- No Redis, Python, ML service, or persisted recommendation table.
- Keep `GET /api/v1/recommender/lectures` unchanged.
- Stable error codes come from backend; Vietnamese user messages stay in frontend.
- Frontend implementation is delegated to Antigravity and must not modify backend, SQL, or specs.

---

## File Map

### Specification and Database

- Modify: `specs/005-recommender/spec.md`
- Modify: `specs/005-recommender/plan.md`
- Modify: `specs/005-recommender/tasks.md`
- Modify: `specs/006-learning-lecture/spec.md`
- Modify: `specs/006-learning-lecture/plan.md`
- Modify: `specs/006-learning-lecture/tasks.md`
- Create: `../Database/Migrations/006_Lecture_Difficulty_Recommendation.sql`
- Modify: `../Database/database/001_Create_MathInsight_Azure.sql`
- Modify: `../Database/database/002_Seed_MathInsight_Demo.sql`

`../Database` is outside the `Implementation/MathInsight` Git root. Commit its SQL changes in the repository or tracking process used by the team for Database artifacts. Do not silently copy the older `MathInsight/database` script over the canonical script.

### Learning & Lecture Backend

- Modify: `src/MathInsight.Modules.Learning_Lecture/Entities/Lecture.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Entities/TagTopicReadOnly.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Entities/TagDifficultyReadOnly.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Persistence/LearningDbContext.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Persistence/Configurations/LectureConfiguration.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Persistence/Configurations/TagTopicReadOnlyConfiguration.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Persistence/Configurations/TagDifficultyReadOnlyConfiguration.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Contracts/LectureDto.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Contracts/DifficultyDto.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Errors/LearningErrors.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/CreateLectureCommand.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/CreateLectureCommandHandler.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/UpdateLectureCommand.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/UpdateLectureCommandHandler.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/PublishLectureCommand.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Commands/Lectures/PublishLectureCommandHandler.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Queries/Difficulties/GetDifficultyListQuery.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Queries/Difficulties/GetDifficultyListQueryHandler.cs`
- Create: `src/MathInsight.Modules.Learning_Lecture/Controllers/DifficultiesController.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Controllers/LecturesController.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Queries/Lectures/GetLectureListQueryHandler.cs`
- Modify: `src/MathInsight.Modules.Learning_Lecture/Queries/Lectures/GetLectureQueryHandler.cs`
- Test: `tests/MathInsight.Modules.Learning_Lecture.Tests/LectureDifficultyTests.cs`
- Test: `tests/MathInsight.Modules.Learning_Lecture.Tests/LearningModelMetadataTests.cs`

### Recommender Backend

- Modify: `src/MathInsight.Modules.Recommender/Persistence/Entities/LectureReadOnly.cs`
- Modify: `src/MathInsight.Modules.Recommender/Persistence/Configurations/LectureReadOnlyConfiguration.cs`
- Modify: `src/MathInsight.Modules.Recommender/Persistence/Configurations/TagTopicReadOnlyConfiguration.cs`
- Modify: `src/MathInsight.Modules.Recommender/Persistence/Configurations/TagDifficultyReadOnlyConfiguration.cs`
- Modify: `src/MathInsight.Modules.Recommender/Persistence/Configurations/StudentReadOnlyConfiguration.cs`
- Modify: `src/MathInsight.Modules.Recommender/Queries/GetRecommendedLectures/RecommendedLectureResponse.cs`
- Modify: `src/MathInsight.Modules.Recommender/Queries/GetRecommendedLectures/GetRecommendedLecturesQueryHandler.cs`
- Modify: `src/MathInsight.Modules.Recommender/Controllers/RecommenderController.cs`
- Test: `tests/MathInsight.Modules.Recommender.Tests/Unit/RecommendedLectureQueryTests.cs`
- Test: `tests/MathInsight.Modules.Recommender.Tests/Integration/LectureRecommendationSqlServerSmokeTests.cs`

### Frontend Handoff

- Create: `specs/005-recommender/frontend-lecture-difficulty-recommendation-handoff.md`
- Antigravity modifies only:
  - `frontend/src/services/learningApi.js`
  - `frontend/src/services/recommenderApi.js`
  - `frontend/src/pages/teacher/LectureEditorPage.jsx`
  - `frontend/src/pages/teacher/LectureListPage.jsx`
  - `frontend/src/pages/teacher/LectureDetailPage.jsx`
  - `frontend/src/pages/student/dashboard/RecommendedLecturesCard.jsx`

---

### Task 1: Align SpecKit Contracts

**Files:**
- Modify: `specs/005-recommender/spec.md`
- Modify: `specs/005-recommender/plan.md`
- Modify: `specs/005-recommender/tasks.md`
- Modify: `specs/006-learning-lecture/spec.md`
- Modify: `specs/006-learning-lecture/plan.md`
- Modify: `specs/006-learning-lecture/tasks.md`

**Interfaces:**
- Consumes: `specs/005-recommender/lecture-difficulty-recommendation-design.md`
- Produces: accepted `RCM` and `BR` requirements traceable by every later task.

- [ ] **Step 1: Add Recommender requirements**

Add requirements covering all mastery levels, `NumberDone >= 3`, exact/lower difficulty matching, the two-per-topic and six-overall limits, cold start, deterministic ordering, response audit reasons, and nullable `OfficialPoint` for cold start.

- [ ] **Step 2: Replace the old weak-only lecture wording**

Change RCM-10 from weak-tag-only matching to the approved full recommendation behavior. Keep material recommendation unchanged and explicitly state that material difficulty ranking is out of scope.

- [ ] **Step 3: Add Learning requirements**

Add the `Lecture.DifficultyID` ownership, create/update validation, legacy nullable transition, publish guard, active difficulty endpoint, and response metadata requirements.

- [ ] **Step 4: Add executable checklist items**

Create a new unchecked phase in both `tasks.md` files. Do not tick an item before its associated test/build gate passes.

- [ ] **Step 5: Verify documentation consistency**

Run:

```powershell
rg -n "weak tag.*lecture|DifficultyID|ColdStartGradeFoundation|NumberDone >= 3|two lectures|six lectures" specs/005-recommender specs/006-learning-lecture
```

Expected: no statement still claims that lecture recommendation only supports weak topics.

- [ ] **Step 6: Commit the SpecKit update**

```powershell
git add specs/005-recommender specs/006-learning-lecture
git commit -m "docs(recommender): specify difficulty-aware lecture recommendations"
```

---

### Task 2: Update the Canonical SQL Contract and Demo Seed

**Files:**
- Create: `../Database/Migrations/006_Lecture_Difficulty_Recommendation.sql`
- Modify: `../Database/database/001_Create_MathInsight_Azure.sql`
- Modify: `../Database/database/002_Seed_MathInsight_Demo.sql`

**Interfaces:**
- Produces: `Lecture.DifficultyID`, FK `FK_Lecture_TagDifficulty_DifficultyID`, and index `IX_Lecture_Status_TagID_DifficultyID`.

- [ ] **Step 1: Write migration preflight checks**

The migration must verify that `Lecture` and `TagDifficulty` exist. It must be idempotent by checking `COL_LENGTH`, `sys.foreign_keys`, and `sys.indexes` before each change.

- [ ] **Step 2: Add the transition column**

Use this existing-database contract:

```sql
IF COL_LENGTH(N'dbo.Lecture', N'DifficultyID') IS NULL
BEGIN
    ALTER TABLE dbo.Lecture ADD DifficultyID VARCHAR(36) NULL;
END;
```

- [ ] **Step 3: Add FK and replace the redundant index**

Create `FK_Lecture_TagDifficulty_DifficultyID`. Drop `IX_Lecture_Status_TagID` only after the new three-column index exists or can be created in the same transaction. Create:

```sql
CREATE INDEX IX_Lecture_Status_TagID_DifficultyID
ON dbo.Lecture(Status, TagID, DifficultyID);
```

- [ ] **Step 4: Update fresh schema**

In the canonical create script, define:

```sql
[DifficultyID] VARCHAR(36) NOT NULL
```

Add the FK in the script's FK section and use only the three-column recommendation index.

- [ ] **Step 5: Add idempotent demo lectures**

Use the seeded Teacher `cccccccc-cccc-cccc-cccc-cccccccccccc` and these exact rows with `MERGE` or guarded inserts:

| LectureID | Topic | Difficulty | Title |
|---|---|---|---|
| `LECTURE-DEMO-DERIV-L1` | `TOPIC-G12-DERIVAPP` | `DIFF-LEVEL-1` | `Nền tảng ứng dụng đạo hàm` |
| `LECTURE-DEMO-DERIV-L2` | `TOPIC-G12-DERIVAPP` | `DIFF-LEVEL-2` | `Hiểu và áp dụng đạo hàm` |
| `LECTURE-DEMO-DERIV-L3` | `TOPIC-G12-DERIVAPP` | `DIFF-LEVEL-3` | `Vận dụng đạo hàm` |
| `LECTURE-DEMO-DERIV-L4` | `TOPIC-G12-DERIVAPP` | `DIFF-LEVEL-4` | `Bài toán đạo hàm nâng cao` |
| `LECTURE-DEMO-INTEGRAL-L1` | `TOPIC-G12-INTEGRAL` | `DIFF-LEVEL-1` | `Nền tảng nguyên hàm tích phân` |
| `LECTURE-DEMO-INTEGRAL-L2` | `TOPIC-G12-INTEGRAL` | `DIFF-LEVEL-2` | `Hiểu và áp dụng tích phân` |
| `LECTURE-DEMO-INTEGRAL-L3` | `TOPIC-G12-INTEGRAL` | `DIFF-LEVEL-3` | `Vận dụng tích phân` |
| `LECTURE-DEMO-INTEGRAL-L4` | `TOPIC-G12-INTEGRAL` | `DIFF-LEVEL-4` | `Bài toán tích phân nâng cao` |

Set all eight rows to `Published`, use short deterministic Content text, null media URLs, and non-negative deterministic Likes. The seed must throw a stable SQL error if the Teacher, topics, or difficulty rows are missing.

- [ ] **Step 6: Add SQL assertions at the end of the migration**

Verify that the column, FK, and index exist. Do not assert `NOT NULL` for the transition migration.

- [ ] **Step 7: Run disposable SQL verification**

Create a disposable database, run canonical create, seed, and migration twice. Verify:

```sql
SELECT LectureID, TagID, DifficultyID, Status
FROM dbo.Lecture
WHERE Status = 'Published';
```

Expected: every seeded lecture has a valid difficulty and the second migration run succeeds without duplicate-object errors.

- [ ] **Step 8: Commit Database artifacts separately**

Use the team's Database repository/tracking location:

```text
feat(database): add lecture difficulty recommendation contract
```

Do not include unrelated application files in this commit.

---

### Task 3: Map Learning Difficulty Metadata

**Files:**
- Modify/Create the Learning persistence and DTO files listed in the File Map.
- Test: `tests/MathInsight.Modules.Learning_Lecture.Tests/LearningModelMetadataTests.cs`

**Interfaces:**
- Produces: nullable `Lecture.DifficultyId`, read-only `TagDifficultyReadOnly`, and `DifficultyDto`.

- [ ] **Step 1: Write failing EF metadata tests**

Assert:

```csharp
Assert.Equal("DifficultyID", lectureType.FindProperty(nameof(Lecture.DifficultyId))!.GetColumnName());
Assert.Equal(36, lectureType.FindProperty(nameof(Lecture.DifficultyId))!.GetMaxLength());
Assert.False(lectureType.FindProperty(nameof(Lecture.DifficultyId))!.IsUnicode());
Assert.True(model.FindEntityType(typeof(TagDifficultyReadOnly))!.IsTableExcludedFromMigrations());
Assert.True(model.FindEntityType(typeof(TagTopicReadOnly))!.IsTableExcludedFromMigrations());
```

- [ ] **Step 2: Run metadata tests and confirm failure**

```powershell
dotnet test tests/MathInsight.Modules.Learning_Lecture.Tests/MathInsight.Modules.Learning_Lecture.Tests.csproj --no-restore --filter LearningModelMetadataTests
```

Expected: FAIL because `DifficultyId` and `TagDifficultyReadOnly` do not exist.

- [ ] **Step 3: Add entities and mappings**

Use:

```csharp
public string? DifficultyId { get; set; }
```

and:

```csharp
public sealed class TagDifficultyReadOnly
{
    public string DifficultyId { get; set; } = string.Empty;
    public string DifficultyName { get; set; } = string.Empty;
    public int LevelValue { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
```

Map `VARCHAR(36)`, PascalCase SQL columns, active/display fields, and `ExcludeFromMigrations()` for both taxonomy read models.

- [ ] **Step 4: Add DTO contracts**

`DifficultyDto` is:

```csharp
public sealed record DifficultyDto(
    string DifficultyId,
    string DifficultyName,
    int LevelValue,
    int DisplayOrder);
```

Add nullable transitional `DifficultyId`, `DifficultyName`, and `DifficultyLevel` fields to `LectureDto`.

- [ ] **Step 5: Run metadata tests**

Expected: PASS.

- [ ] **Step 6: Commit persistence contract**

```powershell
git add src/MathInsight.Modules.Learning_Lecture/Persistence src/MathInsight.Modules.Learning_Lecture/Entities src/MathInsight.Modules.Learning_Lecture/Contracts tests/MathInsight.Modules.Learning_Lecture.Tests/LearningModelMetadataTests.cs
git commit -m "feat(learning): map lecture difficulty metadata"
```

---

### Task 4: Enforce Learning Create, Update, Publish, and Difficulty API

**Files:**
- Modify/Create Learning command, handler, controller, query, error, and test files listed in the File Map.

**Interfaces:**
- Consumes: `DifficultyDto`, `Lecture.DifficultyId`, active taxonomy read models.
- Produces: `GET /api/v1/difficulties` and difficulty-aware lecture write/read contracts.

- [ ] **Step 1: Write failing command tests**

Cover these exact cases:

```text
Create: blank difficulty -> LECTURE_DIFFICULTY_REQUIRED
Create: unknown difficulty -> LECTURE_DIFFICULTY_NOT_FOUND
Create: inactive difficulty -> LECTURE_DIFFICULTY_INACTIVE
Create: inactive topic -> LECTURE_TOPIC_INACTIVE
Update: valid difficulty changes Lecture.DifficultyId
Publish: null DifficultyId -> LECTURE_DIFFICULTY_REQUIRED
Difficulty list: only active rows ordered by DisplayOrder then DifficultyId
```

- [ ] **Step 2: Run tests and confirm failure**

```powershell
dotnet test tests/MathInsight.Modules.Learning_Lecture.Tests/MathInsight.Modules.Learning_Lecture.Tests.csproj --no-restore --filter LectureDifficultyTests
```

- [ ] **Step 3: Add stable errors**

Define `LearningErrors` using `MathInsight.Shared.Results.Error` with the approved codes. Convert the touched Create, Update, and Publish commands to `Result<LectureDto>` or `Result<bool>`; do not catch broad exceptions and expose exception messages.

- [ ] **Step 4: Extend command signatures**

Add `string? DifficultyId` after `TagId` in Create and Update commands. Keep nullable at the transport boundary so the handler can return `LECTURE_DIFFICULTY_REQUIRED` instead of relying on model-binding failure.

- [ ] **Step 5: Implement shared validation inside each touched handler**

Validate `TagTopic.IsActive` and `TagDifficulty.IsActive` with cancellation-aware EF queries before assigning the lecture. Keep validation inside the owning Learning module.

- [ ] **Step 6: Guard publication**

Before `Draft -> Published`, reject null, missing, or inactive difficulty. Preserve existing ownership, state, and content/video requirements.

- [ ] **Step 7: Implement active difficulty query and controller**

Add:

```text
GET /api/v1/difficulties
```

Require authentication, return only active rows, and order by `DisplayOrder`, then `DifficultyID`.

- [ ] **Step 8: Update lecture controller contracts**

Use:

```csharp
public record CreateLectureRequest(
    string Title,
    string? Content,
    string? VideoUrl,
    string? ThumbnailUrl,
    string TagId,
    string? DifficultyId,
    List<string>? MaterialIds,
    string? NextLectureId);
```

Apply the same addition to Update. Map `Result` failures to stable HTTP status codes through `ApiErrorResponse`.

- [ ] **Step 9: Return difficulty metadata from list/detail queries**

Project `DifficultyId`, `DifficultyName`, and `DifficultyLevel`. A legacy null-difficulty lecture returns null metadata instead of throwing.

- [ ] **Step 10: Run Learning tests**

```powershell
dotnet test tests/MathInsight.Modules.Learning_Lecture.Tests/MathInsight.Modules.Learning_Lecture.Tests.csproj --no-restore
```

Expected: all Learning tests pass.

- [ ] **Step 11: Commit Learning behavior**

```powershell
git add src/MathInsight.Modules.Learning_Lecture tests/MathInsight.Modules.Learning_Lecture.Tests
git commit -m "feat(learning): require difficulty for published lectures"
```

---

### Task 5: Implement Deterministic Lecture Recommendation

**Files:**
- Modify Recommender files listed in the File Map.
- Test: `tests/MathInsight.Modules.Recommender.Tests/Unit/RecommendedLectureQueryTests.cs`

**Interfaces:**
- Consumes: canonical `Lecture.DifficultyID`, `TagDifficulty.LevelValue`, `Student.CurrentGrade`, and `TagsMastery.RecommendedDifficultyLevel`.
- Produces: the unchanged route with the new response contract.

- [ ] **Step 1: Write failing recommendation tests**

Create tests for exact match, nearest-lower fallback, never-harder filtering, inactive taxonomy filtering, Published-only filtering, evidence threshold, topic diversity, global limit, classification priority, deterministic ties, cold start, null grade, and semantic IDs.

- [ ] **Step 2: Run the focused tests and confirm failure**

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore --filter RecommendedLectureQueryTests
```

- [ ] **Step 3: Extend the read model**

`LectureReadOnly` must contain:

```csharp
public string LectureId { get; set; } = string.Empty;
public string Title { get; set; } = string.Empty;
public string? ThumbnailUrl { get; set; }
public int Likes { get; set; }
public string TagId { get; set; } = string.Empty;
public string? DifficultyId { get; set; }
public string Status { get; set; } = string.Empty;
public DateTime UpdatedTime { get; set; }
```

Map exact PascalCase columns, non-Unicode IDs, and `ExcludeFromMigrations()`.

- [ ] **Step 4: Replace the response DTO**

Use:

```csharp
public sealed record RecommendedLectureResponse(
    string LectureId,
    string Title,
    string? ThumbnailUrl,
    string TagId,
    string TagName,
    string DifficultyId,
    string DifficultyName,
    int DifficultyLevel,
    byte TargetDifficultyLevel,
    decimal? OfficialPoint,
    int EvidenceCount,
    int Likes,
    bool IsDifficultyFallback,
    string Reason);
```

- [ ] **Step 5: Load qualified personalized contexts**

Join mastery to active topics and filter `NumberDone >= 3`. Classify priority with:

```csharp
var priority = officialPoint < 5.00m ? 0 : officialPoint < 7.50m ? 1 : 2;
```

- [ ] **Step 6: Rank candidates per topic**

Filter active difficulties and `LevelValue <= TargetDifficultyLevel`. Sort exact first, then level descending, likes descending, updated time descending, and lecture ID ascending. Take two per topic and stop at six overall.

- [ ] **Step 7: Generate exact audit reasons**

Use `WeakTopicExactDifficulty` or `WeakTopicLowerDifficultyFallback` for priority 0. Use `ProgressionExactDifficulty` or `ProgressionLowerDifficultyFallback` for priorities 1 and 2.

- [ ] **Step 8: Implement cold start**

If no qualified context exists, read `Student.CurrentGrade`. Return up to six Published level-1 lectures from active topics in that grade, ordered by likes, updated time, and lecture ID. Set target level 1, null point, zero evidence, no fallback, and `ColdStartGradeFoundation`.

- [ ] **Step 9: Normalize controller authentication error**

Missing/blank account ID returns:

```json
{
  "code": "AUTH_INVALID_TOKEN",
  "message": "Invalid or missing account id."
}
```

Wrap technical recommendation-query failures at the controller boundary, log the exception through `ILogger<RecommenderController>`, and return HTTP 503 with code `LECTURE_RECOMMENDATION_UNAVAILABLE`. Never expose the exception message.

- [ ] **Step 10: Run Recommender tests**

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
```

Expected: all Recommender tests pass.

- [ ] **Step 11: Commit recommendation behavior**

```powershell
git add src/MathInsight.Modules.Recommender tests/MathInsight.Modules.Recommender.Tests
git commit -m "feat(recommender): rank lectures by topic and difficulty"
```

---

### Task 6: Add Disposable SQL Server Contract Smoke Test

**Files:**
- Create: `tests/MathInsight.Modules.Recommender.Tests/Integration/LectureRecommendationSqlServerSmokeTests.cs`

**Interfaces:**
- Consumes: environment variable `RECOMMENDER_SQLSERVER_CONNECTION` and canonical database scripts.
- Produces: opt-in verification against actual SQL Server semantics.

- [ ] **Step 1: Add an opt-in test guard**

When `RECOMMENDER_SQLSERVER_CONNECTION` is absent, skip with a clear reason. Never point the test at Azure/shared DB.

- [ ] **Step 2: Create a uniquely named disposable database**

Use a GUID suffix, run the canonical create script, seed only required account/student/topic/difficulty/mastery/lecture rows, and always drop the database in `finally`.

- [ ] **Step 3: Verify personalized and cold-start SQL paths**

Assert exact match, lower fallback, no harder lecture, semantic `student_01`, and level-1 cold start.

- [ ] **Step 4: Run the smoke test locally**

```powershell
if ([string]::IsNullOrWhiteSpace($env:RECOMMENDER_SQLSERVER_CONNECTION)) { throw 'Set RECOMMENDER_SQLSERVER_CONNECTION from your local secret store before running the SQL smoke test.' }
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore --filter LectureRecommendationSqlServerSmokeTests
```

Expected: temporary DB is created and removed, all assertions pass.

- [ ] **Step 5: Commit smoke coverage**

```powershell
git add tests/MathInsight.Modules.Recommender.Tests/Integration/LectureRecommendationSqlServerSmokeTests.cs
git commit -m "test(recommender): verify lecture ranking on SQL Server"
```

---

### Task 7: Hand Frontend Contract to Antigravity

**Files:**
- Create: `specs/005-recommender/frontend-lecture-difficulty-recommendation-handoff.md`

**Interfaces:**
- Consumes: stable backend contracts from Tasks 4 and 5.
- Produces: frontend-only implementation described in the handoff prompt.

- [ ] **Step 1: Save the exact Antigravity prompt**

Use the companion handoff file created with this plan. It must prohibit changes outside `frontend/` and require reading the design contract first.

- [ ] **Step 2: Give Antigravity the backend response examples**

Include personalized exact, personalized fallback, cold start, empty list, and stable error examples.

- [ ] **Step 3: Require frontend verification evidence**

Antigravity must report modified files, interaction checks, and full `npm run build` output. It must not claim backend endpoints were implemented.

- [ ] **Step 4: Review Antigravity output before accepting it**

Check request property names, no mock fallback in production paths, keyboard-accessible cards, Vietnamese error mapping, legacy null-difficulty handling, and no backend/schema edits.

- [ ] **Step 5: Commit frontend separately**

```powershell
git add frontend
git commit -m "feat(frontend): support difficulty-aware lecture recommendations"
```

---

### Task 8: Run Final Quality Gates and Close Spec Tasks

**Files:**
- Modify: `specs/005-recommender/tasks.md`
- Modify: `specs/006-learning-lecture/tasks.md`

- [ ] **Step 1: Run affected module tests**

```powershell
dotnet test tests/MathInsight.Modules.Learning_Lecture.Tests/MathInsight.Modules.Learning_Lecture.Tests.csproj --no-restore
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
```

- [ ] **Step 2: Run full backend verification**

```powershell
dotnet test MathInsight.sln --no-restore
dotnet build MathInsight.sln --no-restore
dotnet format MathInsight.sln --no-restore --verify-no-changes
```

- [ ] **Step 3: Run frontend verification**

```powershell
Set-Location frontend
npm run build
```

- [ ] **Step 4: Run repository hygiene checks**

```powershell
git diff --check
git status --short
```

Expected: no generated `dist`, `bin`, or `obj` files are staged.

- [ ] **Step 5: Perform manual workflow checks**

Verify Teacher create/update/publish with difficulty; Student exact recommendation; lower-level fallback explanation; cold-start card; empty state; click-through to lecture detail.

- [ ] **Step 6: Tick only passed SpecKit items**

Record skipped SQL smoke separately if no disposable SQL Server was available. Do not mark it complete based only on InMemory tests.

- [ ] **Step 7: Commit gate evidence and checklist state**

```powershell
git add specs/005-recommender/tasks.md specs/006-learning-lecture/tasks.md
git commit -m "docs(recommender): record lecture recommendation quality gates"
```
