# Mastery-Aware Personal BlueprintExam Implementation Plan

> **For agentic workers:** Implement tasks in order with TDD. Backend is assigned to Terra 5.6, frontend to Antigravity, and independent verification to Luna.

**Goal:** Complete Checkpoint 6B so a Student can create and immediately start a blueprint-faithful exam whose difficulty moves at most one level according to qualified topic mastery.

**Architecture:** Extend only the personal `GenerateBlueprintExam` path. Reuse the shared batch mastery provider, add an isolated adaptive planner/selector, preserve baseline/shared selectors, and store audit in existing TestQuestion fields. Frontend adds a create command above the unchanged shared catalog.

**Tech Stack:** .NET 10, ASP.NET Core, MediatR, EF Core/SQL Server, xUnit, React JavaScript, Vite, Vitest.

## Global Constraints

- Follow `specs/constitution.md` and `specs/009-test-generator/adaptive-blueprint-exam-design.md`.
- Do not change SQL schema, EF migrations, Test/TestQuestion columns, routes, request body, or shared Fixed/Random semantics.
- TestGen depends only on `MathInsight.Shared.Recommendations.IStudentTopicMasteryProvider`, never on the Recommender project or an HTTP endpoint.
- Use stable error codes; Vietnamese localization remains in frontend.
- Preserve semantic string IDs and current SQL mappings.
- Do not implement recent-question deduplication in this checkpoint.

---

### Task 1: Characterize Policy and Resolve Difficulty Plans

**Files:**
- Create: `src/MathInsight.Modules.TestGen/Generation/AdaptiveBlueprintExamPolicy.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/AdaptiveBlueprintExamPlan.cs`
- Test: `tests/MathInsight.Modules.TestGen.Tests/AdaptiveBlueprintExamPolicyTests.cs`

**Interfaces:**

```csharp
public static class AdaptiveBlueprintExamPolicy
{
    public const string RuleVersion = "BlueprintExam-Mastery-v2";
    public const int MinimumItemCount = 5;
    public const int MinimumSessionCount = 2;
    public const int StrongItemCount = 8;
    public const int StrongSessionCount = 3;

    public static int ResolvePreferredLevel(
        int originalLevel,
        TopicMasteryAdvice? mastery);
}

public sealed record AdaptiveBlueprintDetailPlan(
    string BlueprintDetailId,
    string TagId,
    string OriginalDifficultyId,
    string PreferredDifficultyId,
    decimal? OfficialPoint,
    bool HasQualifiedMastery,
    bool HasDifficultyAdjustment);
```

- [ ] Write boundary tests for points `0`, `4.99`, `5`, `7.49`, `7.5`, `10`, evidence `2/3`, missing advice, level-1 downscale, and level-4 upscale.
- [ ] Run only `AdaptiveBlueprintExamPolicyTests` and confirm RED.
- [ ] Implement the minimum policy: qualified `<5` minus one, `5..<7.5` unchanged, `>=7.5` plus one, clamped `1..4`.
- [ ] Reject malformed qualified advice later at the application boundary; the pure policy must not perform database access.
- [ ] Run the focused tests and commit: `feat(testgen): define adaptive blueprint mastery policy`.

### Task 2: Add Preferred-Then-Original Global Assignment

**Files:**
- Create: `src/MathInsight.Modules.TestGen/Generation/IAdaptiveBlueprintExamQuestionSelector.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/AdaptiveBlueprintExamQuestionSelector.cs`
- Test: `tests/MathInsight.Modules.TestGen.Tests/AdaptiveBlueprintExamQuestionSelectorTests.cs`
- Modify: `src/MathInsight.Modules.TestGen/TestGenModuleExtensions.cs`

**Interface:**

```csharp
public interface IAdaptiveBlueprintExamQuestionSelector
{
    BlueprintExamSelection Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        CancellationToken cancellationToken);
}
```

- [ ] Write RED tests proving preferred difficulty wins, original fallback completes a shortage, unrelated difficulty is rejected, multi-topic Questions remain globally unique, full flow is found when greedy selection would fail, and cancellation is honored.
- [ ] Implement a minimum-cost max-flow assignment. Candidate-to-detail cost is `0` for preferred and `1` for original fallback; target flow equals total required Quantity.
- [ ] Shuffle candidates and equal-cost requirement edges through the existing `IGenerationRandomizer`; do not use `Random` directly.
- [ ] Return existing `BlueprintExamAssignment` rows in section/detail/candidate order.
- [ ] Register the new selector without replacing `IBlueprintExamQuestionSelector` used by baseline/shared generation.
- [ ] Run focused selector tests and existing `BlueprintExamGenerationTests`.
- [ ] Commit: `feat(testgen): add adaptive capacity assignment`.

### Task 3: Expand Candidate Loading Without Regressing Shared Generation

**Files:**
- Modify: `src/MathInsight.Modules.TestGen/Generation/IBlueprintExamCandidateProvider.cs`
- Modify: `src/MathInsight.Modules.TestGen/Generation/BlueprintExamCandidateProvider.cs`
- Test: `tests/MathInsight.Modules.TestGen.Tests/BlueprintExamGenerationTests.cs`

**Interface:** retain the existing overload and add:

```csharp
Task<BlueprintExamCandidatePool> GetCandidatesAsync(
    Blueprint blueprint,
    IReadOnlyCollection<string> difficultyIds,
    CancellationToken cancellationToken);
```

- [ ] Write a RED test proving an adjacent preferred difficulty is returned while grade/topic/type/version gates remain unchanged.
- [ ] Make the existing overload delegate with blueprint-original difficulty IDs so Fixed/Random callers keep identical behavior.
- [ ] Query once for the union of original and preferred IDs; do not query per detail.
- [ ] Run candidate, baseline, fixed, and shared generation tests.
- [ ] Commit: `feat(testgen): load adaptive blueprint candidates`.

### Task 4: Integrate Mastery, Persistence Audit, Retry Verification, and API Errors

**Files:**
- Modify: `src/MathInsight.Modules.TestGen/Commands/GenerateBlueprintExam/GenerateBlueprintExamCommandHandler.cs`
- Modify: `src/MathInsight.Modules.TestGen/Contracts/Tests/BlueprintExamResponses.cs`
- Modify: `src/MathInsight.Modules.TestGen/Errors/TestGenerationErrors.cs`
- Modify: `src/MathInsight.Modules.TestGen/Controllers/StudentTestsController.cs`
- Test: `tests/MathInsight.Modules.TestGen.Tests/BlueprintExamGenerationTests.cs`
- Test: `tests/MathInsight.Modules.TestGen.Tests/StudentTestsControllerTests.cs`

**Additive response:**

```csharp
bool WasAdaptive,
int AdaptiveQuestionCount,
int BaselineQuestionCount,
string RuleVersion
```

- [ ] Write RED handler tests for one batch provider call, weak downscale, neutral baseline, strong upscale, insufficient evidence, missing mastery, level clamp, target shortage fallback, provider exception, malformed advice, no partial write, and mixed audit rows.
- [ ] Bulk-load active TagDifficulty levels `1..4`; resolve original IDs and preferred IDs without comparing a level number directly to DifficultyID.
- [ ] Call `IStudentTopicMasteryProvider` once with distinct exact blueprint TagIDs. Missing keys are baseline. Validate used advice has matching nonblank TagID, `OfficialPoint` in `0..10`, and nonnegative item/session evidence counts.
- [ ] Use the adaptive selector and persist audit only when the actual candidate difficulty equals a genuinely adjusted preferred difficulty.
- [ ] Extend ambiguous-commit verification to accept and validate the exact mixed adaptive/baseline aggregate, response counts, score snapshots, order, quantities, and blueprint activation.
- [ ] Add `ADAPTIVE_EXAM_MASTERY_UNAVAILABLE` and `ADAPTIVE_EXAM_MASTERY_INVALID`, both mapped to HTTP 503.
- [ ] Keep request and routes unchanged. Add no TestSession creation.
- [ ] Run TestGen tests and Recommender contract tests.
- [ ] Commit: `feat(testgen): generate mastery-aware blueprint exams`.

### Task 5: Add Student `Tạo đề theo năng lực` UI

**Files:**
- Create: `frontend/src/components/student/AdaptiveBlueprintExamDialog.jsx`
- Create: `frontend/src/components/student/AdaptiveBlueprintExamDialog.test.jsx`
- Modify: `frontend/src/pages/student/SharedBlueprintExamDiscoveryPage.jsx`
- Modify: `frontend/src/pages/student/SharedBlueprintExamDiscoveryPage.test.jsx`
- Modify: `frontend/src/services/testGeneratorApi.js`
- Modify: `frontend/src/utils/testGenerationErrorLocalizer.js`

**API methods:**

```js
getBlueprintExamOptions()
generateBlueprintExam(blueprintId)
```

- [ ] Add a command button with Material Symbol `auto_awesome` and label `Tạo đề theo năng lực`.
- [ ] Keep `Kho đề` with tabs `Đề cố định` and `Đề theo cấu trúc`; do not place the create command inside their tablist.
- [ ] On mobile, stack the command above the catalog tabs; keep controls at least 44px high.
- [ ] Lazily fetch blueprint options when the dialog opens. Provide loading, empty, retry, selected, generating, starting, and start-retry states.
- [ ] Show blueprint name, grade, sections, questions, duration, and score. Copy must explain that structure is preserved and some difficulty levels may be adjusted from recent results.
- [ ] Final command is `Tạo và bắt đầu`. Call generation once, retain returned TestID, then call existing `startSession`. On start failure, retry the same TestID without another generation request.
- [ ] Prevent double submit with state plus an in-flight ref. Closing is disabled while generating/starting.
- [ ] Localize the two new 503 codes. Do not show technical terms `WeakTag`, `Ptag`, `adaptive`, `baseline`, `recommender`, or `ma trận`.
- [ ] Add tests for layout separation, lazy options, one generate call, start retry without regeneration, no-options copy, stable errors, and Fixed/Random regression.
- [ ] Run frontend tests and build.
- [ ] Commit: `feat(student): add mastery-aware exam creation`.

### Task 6: Independent Verification and SpecKit Closure

**Files:**
- Modify only after evidence: `specs/009-test-generator/tasks.md`
- No production source edits are allowed in the verification run.

- [ ] Run TestGen tests, Recommender tests, full solution build, frontend tests, frontend build, and `git diff --check`.
- [ ] Browser smoke with an existing Student account: Fixed/Random catalog remains separate; open adaptive dialog; select blueprint; generate once; start session; verify navigation and no severe console errors.
- [ ] Verify one qualified-mastery case produces at least one adaptive row when data permits. Verify missing/insufficient mastery produces a valid baseline Test rather than an error.
- [ ] If SQL access is configured, use read-only SELECTs to verify quantities, order, difficulty, audit, scoring totals, and no duplicate QuestionID. Do not alter seed/schema.
- [ ] Mark only evidence-backed task boxes. Keep disposable SQL fault-injection and recent-question deduplication open unless actually executed.
- [ ] Report PASS/BLOCKED with exact commands, counts, TestID/SessionID, network calls, and remaining warnings.
