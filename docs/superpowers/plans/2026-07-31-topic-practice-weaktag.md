# WeakTag-Aware Topic Practice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Student TopicPractice use qualified WeakTag advice to adapt its ten-question difficulty mix while preserving the current baseline behavior when no usable advice exists.

**Architecture:** `MathInsight.Shared` owns a small in-process recommendation contract. `MathInsight.Modules.Recommender` implements that contract from `TagsMastery`, while `MathInsight.Modules.TestGen` resolves one representative WeakTag inside the Student-selected active topic subtree, applies a deterministic focus/general selection plan, and persists existing `TestQuestion` audit fields. The frontend only consumes additive response fields; it continues to submit `{ tagId }` and never sends recommendation decisions back to the server.

**Tech Stack:** .NET 10, ASP.NET Core, MediatR 12, EF Core 9 SQL Server, xUnit 2, Moq 4, React 18, Vite 5, Tailwind CSS, Axios.

## Global Constraints

- Follow `specs/constitution.md`: SpecKit first, stable error contracts, cross-module contracts in `MathInsight.Shared`, and no raw backend error messages in the frontend.
- Keep TopicPractice at exactly `10` unique Questions, `MaxScore = 10.00`, `DurationMinutes = 0`, `ScoringPolicy = NormalizedWeight`, and at most `2` Composite Questions.
- Keep request routes and body unchanged: `GET /api/test-generator/tests/topic-practice-options` and `POST /api/test-generator/tests/topic-practices` with `{ "tagId": "..." }`.
- Keep semantic IDs as `string`; do not parse Student, Tag, Question, Test, or Difficulty IDs as `Guid`.
- A qualified WeakTag requires `OfficialPoint < 5.00` and `EvidenceCount >= 3`.
- The shared `WeakTagAdvice` contract must not expose `IsRemedial`; the existing Recommender REST DTO may retain it for backward compatibility.
- `WeakTagAdaptiveEnabled = false` means baseline `3/4/2/1` selection and the provider is not called.
- `WeakTagAdaptiveEnabled = true` plus an empty advice list means baseline selection; provider failure or invalid advice means HTTP `503` and zero Test/TestQuestion writes.
- Do not add tables, columns, EF migrations, Redis, ML, multiple-WeakTag blending, freshness decay, incorrect-answer prioritization, or Adaptive BlueprintExam behavior.
- Use the existing `IGenerationRandomizer`; do not introduce another randomness abstraction.
- The generation command must re-read recommendation advice instead of trusting option-preview fields from the client.
- Run disposable SQL tests only with `TESTGEN_SQLSERVER_CONNECTION`; never point them at Azure or the shared team database.

---

### Task 1: Align Recommender and TestGen SpecKit Contracts

**Files:**
- Modify: `specs/005-recommender/spec.md`
- Modify: `specs/005-recommender/plan.md`
- Modify: `specs/005-recommender/tasks.md`
- Modify: `specs/009-test-generator/spec.md`
- Modify: `specs/009-test-generator/plan.md`
- Modify: `specs/009-test-generator/tasks.md`

**Interfaces:**
- Consumes: approved design in `docs/superpowers/specs/2026-07-31-topic-practice-weaktag-design.md`.
- Produces: normative requirements for the shared provider, feature flag, representative-tag resolution, selection profiles, audit fields, API additions, errors, and verification gates used by Tasks 2-9.

- [ ] **Step 1: Add the Recommender shared-provider contract to SpecKit**

Add this exact cross-module contract to `specs/005-recommender/spec.md` and replace stale `Guid` signatures in both Recommender `spec.md` and `plan.md`:

```csharp
public interface IStudentRecommendationProvider
{
    Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId,
        CancellationToken cancellationToken = default);
}

public sealed record WeakTagAdvice(
    string TagId,
    string TagName,
    decimal OfficialPoint,
    int EvidenceCount,
    byte RecommendedDifficultyLevel,
    string Reason);
```

State explicitly that this provider returns only active, qualified rows (`OfficialPoint < 5.00`, `EvidenceCount >= 3`), that an empty list is a successful no-advice result, and that provider exceptions indicate technical failure. Keep `IRecommenderService` and `WeakTagAdviceDto` documented as the Recommender REST/internal compatibility surface.

- [ ] **Step 2: Add the WeakTag-aware TopicPractice phase to TestGen SpecKit**

Append a new section named `Checkpoint 6D: WeakTag-Aware TopicPractice` to `specs/009-test-generator/spec.md` and `plan.md`. Include these exact profiles:

```text
Baseline: Level 1/2/3/4 = 3/4/2/1
Recommended Level 1: 8/2/0/0, with six focus slots = 5xL1 + 1xL2
Recommended Level 2: 2/7/1/0, with six focus slots = 1xL1 + 4xL2 + 1xL3
```

Document the parent-selection breadth rule: target at least six focus Questions, prefer at least two Questions outside the representative WeakTag subtree, permit up to eight focus Questions when possible, and relax that cap only when necessary to avoid a false insufficient-pool result. Direct selection of the representative tag disables the breadth cap.

- [ ] **Step 3: Add API, failure, and audit requirements**

Document the additive option fields:

```text
isWeakRecommended
weakTagId
weakTagName
officialPoint
evidenceCount
recommendedDifficultyLevel
recommendationReason
```

Document the additive generation fields:

```text
wasAdaptive
weakTagId
weakTagName
recommendedDifficultyLevel
adaptiveQuestionCount
fallbackQuestionCount
ruleVersion
```

Document `TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE` and `TOPIC_PRACTICE_RECOMMENDATION_INVALID` as HTTP `503`, and document baseline versus adaptive `TestQuestion` audit values exactly as approved in the design.

- [ ] **Step 4: Add separately tracked checklist phases**

In `specs/005-recommender/tasks.md`, add unchecked items for the shared provider, evidence gate, semantic-ID tests, and DI registration. In `specs/009-test-generator/tasks.md`, add an unchecked `Phase 8D: WeakTag-Aware TopicPractice` containing resolver, selector, generation audit, options response, SQL smoke, frontend, and quality-gate items. Do not check any item in this task and do not mark `Phase 8B: Adaptive BlueprintExam Backlog` complete.

- [ ] **Step 5: Verify the documents contain no stale cross-module signature**

Run:

```powershell
rg -n "GetWeakTagAdviceAsync|EvidenceCount|WeakTagAdaptiveEnabled|TopicPractice-WeakTag-v1|TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE|Phase 8D" specs/005-recommender specs/009-test-generator
rg -n "GetStudentWeakTagAdviceAsync\(Guid|GetStudentWeakTagsAsync\(Guid" specs/005-recommender
```

Expected: the first command finds every new contract term; the second command returns no matches.

- [ ] **Step 6: Commit SpecKit alignment**

```powershell
git add specs/005-recommender/spec.md specs/005-recommender/plan.md specs/005-recommender/tasks.md specs/009-test-generator/spec.md specs/009-test-generator/plan.md specs/009-test-generator/tasks.md
git commit -m "docs(topic-practice): specify WeakTag-aware generation"
```

**Completion criteria:** SpecKit describes the approved feature without changing Adaptive BlueprintExam scope, and all checklist items remain unchecked until their implementation gates pass.

---

### Task 2: Add the Shared Provider and Recommender Implementation

**Files:**
- Create: `src/MathInsight.Shared/Recommendations/IStudentRecommendationProvider.cs`
- Modify: `src/MathInsight.Modules.Recommender/Services/RecommenderService.cs`
- Modify: `src/MathInsight.Modules.Recommender/RecommenderModuleExtensions.cs`
- Create: `tests/MathInsight.Modules.Recommender.Tests/Unit/StudentRecommendationProviderTests.cs`

**Interfaces:**
- Consumes: `TagsMastery.NumberDone`, `TagsMastery.OfficialPoint`, `TagsMastery.RecommendedDifficultyLevel`, and active `TagTopicReadOnly` rows.
- Produces: `IStudentRecommendationProvider.GetWeakTagAdviceAsync(string, CancellationToken)` and `WeakTagAdvice` from `MathInsight.Shared.Recommendations`.

- [ ] **Step 1: Write failing provider qualification tests**

Create `StudentRecommendationProviderTests.cs` with an EF InMemory fixture and semantic IDs such as `student_01` and `TOPIC-G12-DERIVAPP`. Add these tests:

```csharp
[Fact]
public async Task GetWeakTagAdviceAsync_ReturnsOnlyActiveRowsBelowFiveWithThreeEvidence()
{
    Seed("qualified", 4.20m, numberDone: 3, isActive: true, level: 2);
    Seed("too-little-evidence", 2.00m, numberDone: 2, isActive: true, level: 1);
    Seed("not-weak", 5.00m, numberDone: 10, isActive: true, level: 2);
    Seed("inactive", 1.00m, numberDone: 10, isActive: false, level: 1);
    await _db.SaveChangesAsync();

    var result = await _sut.GetWeakTagAdviceAsync("student_01");

    var advice = Assert.Single(result);
    Assert.Equal("qualified", advice.TagId);
    Assert.Equal(3, advice.EvidenceCount);
    Assert.Equal((byte)2, advice.RecommendedDifficultyLevel);
}
```

Also add:

```csharp
[Fact]
public async Task GetWeakTagAdviceAsync_OrdersByOfficialPointThenTagId()
```

Assert ascending `OfficialPoint`, then ordinal `TagId` for equal points. Add a compile-time assertion:

```csharp
Assert.IsAssignableFrom<IStudentRecommendationProvider>(_sut);
```

- [ ] **Step 2: Run the Recommender tests and verify RED**

Run:

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore --filter "FullyQualifiedName~StudentRecommendationProviderTests"
```

Expected: FAIL because `MathInsight.Shared.Recommendations`, `WeakTagAdvice`, and `GetWeakTagAdviceAsync` do not exist.

- [ ] **Step 3: Create the shared contract**

Add exactly:

```csharp
namespace MathInsight.Shared.Recommendations;

public interface IStudentRecommendationProvider
{
    Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
        string studentId,
        CancellationToken cancellationToken = default);
}

public sealed record WeakTagAdvice(
    string TagId,
    string TagName,
    decimal OfficialPoint,
    int EvidenceCount,
    byte RecommendedDifficultyLevel,
    string Reason);
```

- [ ] **Step 4: Implement the provider without changing the existing REST DTO**

Change the class declaration to:

```csharp
public sealed class RecommenderService : IRecommenderService, IStudentRecommendationProvider
```

Add a separate provider method; do not replace `GetStudentWeakTagAdviceAsync`:

```csharp
public async Task<IReadOnlyList<WeakTagAdvice>> GetWeakTagAdviceAsync(
    string studentId,
    CancellationToken cancellationToken = default)
{
    return await (
        from mastery in _db.TagsMasteries.AsNoTracking()
        join topic in _db.TagTopics.AsNoTracking() on mastery.TagId equals topic.TagId
        where mastery.StudentId == studentId
            && mastery.OfficialPoint < WeakThreshold
            && mastery.NumberDone >= 3
            && topic.IsActive
        orderby mastery.OfficialPoint, mastery.TagId
        select new WeakTagAdvice(
            mastery.TagId,
            topic.TagName,
            mastery.OfficialPoint,
            mastery.NumberDone,
            mastery.RecommendedDifficultyLevel,
            mastery.OfficialPoint < 4.00m ? "BottleneckSubTag" : "OfficialPointBelow5"))
        .ToListAsync(cancellationToken);
}
```

Keep `WeakTagAdviceDto.IsRemedial`, `RecommendedDifficultyId`, and existing Recommender routes unchanged.

- [ ] **Step 5: Register both interfaces to the same scoped service**

Replace the existing direct registration with:

```csharp
services.AddScoped<RecommenderService>();
services.AddScoped<IRecommenderService>(provider =>
    provider.GetRequiredService<RecommenderService>());
services.AddScoped<IStudentRecommendationProvider>(provider =>
    provider.GetRequiredService<RecommenderService>());
```

Add the `MathInsight.Shared.Recommendations` using directive.

- [ ] **Step 6: Run provider and existing Recommender tests**

Run:

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
```

Expected: PASS, including existing `WeakTagAdviceDto` and API tests.

- [ ] **Step 7: Commit the shared boundary**

```powershell
git add src/MathInsight.Shared/Recommendations/IStudentRecommendationProvider.cs src/MathInsight.Modules.Recommender/Services/RecommenderService.cs src/MathInsight.Modules.Recommender/RecommenderModuleExtensions.cs tests/MathInsight.Modules.Recommender.Tests/Unit/StudentRecommendationProviderTests.cs
git commit -m "feat(recommender): expose qualified WeakTag advice"
```

**Completion criteria:** Recommender returns only active qualified advice through the shared string-ID contract, existing Recommender endpoints remain compatible, and the full Recommender test project passes.

---

### Task 3: Add Feature Configuration and Deterministic Recommendation Resolution

**Files:**
- Create: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeFeatureOptions.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeRecommendationContracts.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/ITopicPracticeRecommendationResolver.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeRecommendationResolver.cs`
- Modify: `src/MathInsight.Modules.TestGen/TestGenModuleExtensions.cs`
- Modify: `src/MathInsight.Modules.TestGen/Errors/TestGenerationErrors.cs`
- Modify: `src/MathInsight.WebAPI/appsettings.json`
- Create: `tests/MathInsight.Modules.TestGen.Tests/TopicPracticeRecommendationResolverTests.cs`

**Interfaces:**
- Consumes: `IStudentRecommendationProvider`, active grade-filtered `TagTopicReadModel` rows, selected Tag ID, and `TopicPractice:WeakTagAdaptiveEnabled`.
- Produces: `ITopicPracticeRecommendationResolver.ResolveForTopicsAsync(string, IReadOnlyCollection<TagTopicReadModel>, CancellationToken)` returning one case-insensitive context map for all active grade topics after exactly one provider call.

- [ ] **Step 1: Write failing resolver tests**

Use Moq for `IStudentRecommendationProvider` and `Options.Create(...)`. Add tests with these exact assertions:

```csharp
[Fact]
public async Task ResolveForTopicsAsync_Disabled_DoesNotCallProviderAndReturnsBaselineContexts()
{
    var result = await CreateResolver(enabled: false).ResolveForTopicsAsync(
        "student_01", Topics(), CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.All(result.Value!.Values, context => Assert.False(context.IsAdaptive));
    _provider.Verify(
        item => item.GetWeakTagAdviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

Add these additional test methods:

```csharp
ResolveForTopicsAsync_EmptyAdvice_ReturnsBaselineContexts
ResolveForTopicsAsync_MapsAdviceOnlyToSelectedTagsWhoseSubtreeContainsIt
ResolveForTopicsAsync_SelectsLowestPointThenLowestLevelThenDeepestThenDisplayOrderThenTagId
ResolveForTopicsAsync_CallsProviderExactlyOnceForAllTopicContexts
ResolveForTopicsAsync_ProviderThrows_ReturnsRecommenderUnavailable
ResolveForTopicsAsync_RejectsBlankIdsDuplicateIdsOutOfRangePointLowEvidenceAndLevelThreeOrFour
```

For invalid rows, assert `TOPIC_PRACTICE_RECOMMENDATION_INVALID`; for provider exceptions, assert `TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE`.

- [ ] **Step 2: Run resolver tests and verify RED**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeRecommendationResolverTests"
```

Expected: FAIL because the resolver types and errors do not exist.

- [ ] **Step 3: Add feature and resolution contracts**

Create:

```csharp
public sealed class TopicPracticeFeatureOptions
{
    public const string SectionName = "TopicPractice";
    public bool WeakTagAdaptiveEnabled { get; set; } = true;
}

public sealed record TopicPracticeRecommendationContext(
    bool IsAdaptive,
    WeakTagAdvice? RepresentativeAdvice,
    IReadOnlySet<string> FocusTagIds)
{
    public static TopicPracticeRecommendationContext Baseline { get; } =
        new(false, null, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
```

Create the interface:

```csharp
public interface ITopicPracticeRecommendationResolver
{
    Task<Result<IReadOnlyDictionary<string, TopicPracticeRecommendationContext>>> ResolveForTopicsAsync(
        string studentId,
        IReadOnlyCollection<TagTopicReadModel> activeGradeTopics,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement validation and deterministic tie-breaking**

The resolver must:

1. Return `Baseline` immediately when the flag is disabled.
2. Call the provider exactly once when enabled.
3. Catch technical exceptions other than cancellation and return `TopicPracticeRecommenderUnavailable`.
4. Re-throw `OperationCanceledException` when the supplied token was cancelled.
5. Validate every returned row before subtree filtering: nonblank unique `TagId`, nonblank `TagName`, `0.00 <= OfficialPoint < 5.00`, `EvidenceCount >= 3`, recommended level `1` or `2`, nonblank `Reason`.
6. Build a case-insensitive dictionary entry for every active grade topic; each entry considers only advice whose tag belongs to that topic's `ResolveActiveSubtree(...)` result.
7. Select each entry's representative by `OfficialPoint`, level, deepest descendant depth from that selected topic, topic `DisplayOrder`, then ordinal `TagId`.
8. Resolve `FocusTagIds` from each representative tag's active subtree; topics with no matching advice receive `TopicPracticeRecommendationContext.Baseline`.

Inject `ILogger<TopicPracticeRecommendationResolver>` and log provider failures or invalid-contract detection at warning level with `StudentId`; do not log exception messages into API responses and do not log tokens, passwords, answers, or connection strings.

Add errors:

```csharp
public static readonly Error TopicPracticeRecommenderUnavailable = new(
    "TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE",
    "Recommendation advice is temporarily unavailable.");

public static readonly Error TopicPracticeRecommendationInvalid = new(
    "TOPIC_PRACTICE_RECOMMENDATION_INVALID",
    "Recommendation advice could not be safely applied.");
```

- [ ] **Step 5: Register configuration and resolver**

In `TestGenModuleExtensions.cs` add:

```csharp
services.Configure<TopicPracticeFeatureOptions>(
    configuration.GetSection(TopicPracticeFeatureOptions.SectionName));
services.AddScoped<ITopicPracticeRecommendationResolver, TopicPracticeRecommendationResolver>();
```

In `appsettings.json` add:

```json
"TopicPractice": {
  "WeakTagAdaptiveEnabled": true
}
```

- [ ] **Step 6: Run resolver tests**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeRecommendationResolverTests"
```

Expected: PASS.

- [ ] **Step 7: Commit feature resolution**

```powershell
git add src/MathInsight.Modules.TestGen/Generation/TopicPracticeFeatureOptions.cs src/MathInsight.Modules.TestGen/Generation/TopicPracticeRecommendationContracts.cs src/MathInsight.Modules.TestGen/Generation/ITopicPracticeRecommendationResolver.cs src/MathInsight.Modules.TestGen/Generation/TopicPracticeRecommendationResolver.cs src/MathInsight.Modules.TestGen/TestGenModuleExtensions.cs src/MathInsight.Modules.TestGen/Errors/TestGenerationErrors.cs src/MathInsight.WebAPI/appsettings.json tests/MathInsight.Modules.TestGen.Tests/TopicPracticeRecommendationResolverTests.cs
git commit -m "feat(testgen): resolve TopicPractice WeakTag advice"
```

**Completion criteria:** the feature flag has exact fail-closed semantics, representative-tag resolution is deterministic and cycle-safe, and no database write is involved.

---

### Task 4: Enrich TopicPractice Options Without Trusting the Preview

**Files:**
- Modify: `src/MathInsight.Modules.TestGen/Contracts/Tests/TopicPracticeContracts.cs`
- Modify: `src/MathInsight.Modules.TestGen/Queries/GetTopicPracticeOptions/GetTopicPracticeOptionsQueryHandler.cs`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/TopicPracticeOptionsTests.cs`

**Interfaces:**
- Consumes: `ITopicPracticeRecommendationResolver.ResolveForTopicsAsync(string, IReadOnlyCollection<TagTopicReadModel>, CancellationToken)` from Task 3.
- Produces: additive per-topic recommendation preview fields on `TopicPracticeTopicResponse`; route and query input remain unchanged.

- [ ] **Step 1: Write failing options tests**

Update the handler test factory to inject a fake resolver. Add:

```csharp
[Fact]
public async Task Options_MarksSelectedAncestorWithRepresentativeWeakDescendant()
{
    var result = await Handler(fixture, ResolverFor(new Dictionary<string, TopicPracticeRecommendationContext>
    {
        ["parent"] = new TopicPracticeRecommendationContext(
            true,
            new WeakTagAdvice("child", "Đạo hàm", 2.40m, 5, 1, "OfficialPointBelow5"),
            new HashSet<string>(["child"], StringComparer.OrdinalIgnoreCase))
    }))
        .Handle(new("student"), CancellationToken.None);

    var parent = Assert.Single(result.Value!.Topics, item => item.TagId == "parent");
    Assert.True(parent.IsWeakRecommended);
    Assert.Equal("child", parent.WeakTagId);
    Assert.Equal(2.40m, parent.OfficialPoint);
    Assert.Equal(5, parent.EvidenceCount);
    Assert.Equal((byte)1, parent.RecommendedDifficultyLevel);
}
```

Add `Options_NoAdvice_ReturnsFalseAndNullPreviewFields`, `Options_CallsResolverOnceForAllTopics`, and `Options_ProviderFailure_PropagatesStableError`.

- [ ] **Step 2: Run options tests and verify RED**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeOptionsTests"
```

Expected: FAIL because response fields and resolver injection are absent.

- [ ] **Step 3: Extend the response record additively**

Use this record shape:

```csharp
public sealed record TopicPracticeTopicResponse(
    string TagId,
    string? ParentTagId,
    string TagName,
    int DisplayOrder,
    int AvailableQuestionCount,
    bool CanGenerate,
    bool IsWeakRecommended,
    string? WeakTagId,
    string? WeakTagName,
    decimal? OfficialPoint,
    int? EvidenceCount,
    byte? RecommendedDifficultyLevel,
    string? RecommendationReason);
```

The outer `TopicPracticeOptionsResponse` remains unchanged.

- [ ] **Step 4: Resolve preview advice per selectable topic**

Inject `ITopicPracticeRecommendationResolver`. Fetch Student grade, active topics, difficulty IDs, and candidate pool once as today. Call `ResolveForTopicsAsync` exactly once with the already-loaded topic collection. If it fails, return that error and do not return a partially enriched list. For each topic, keep the existing count/cap calculation, look up its context by `TagId`, and populate the seven additive fields from its representative advice.

Do not accept recommendation preview input on `GenerateTopicPracticeRequest`.

- [ ] **Step 5: Run options tests**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeOptionsTests"
```

Expected: PASS.

- [ ] **Step 6: Commit option previews**

```powershell
git add src/MathInsight.Modules.TestGen/Contracts/Tests/TopicPracticeContracts.cs src/MathInsight.Modules.TestGen/Queries/GetTopicPracticeOptions/GetTopicPracticeOptionsQueryHandler.cs tests/MathInsight.Modules.TestGen.Tests/TopicPracticeOptionsTests.cs
git commit -m "feat(testgen): expose TopicPractice WeakTag previews"
```

**Completion criteria:** every topic option independently describes the current representative WeakTag, but generation still accepts only `tagId` and re-resolves advice.

---

### Task 5: Implement Focus/General Adaptive Question Selection

**Files:**
- Modify: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeSelectionContracts.cs`
- Create: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeSelectionPlanFactory.cs`
- Modify: `src/MathInsight.Modules.TestGen/Generation/ITopicPracticeQuestionSelector.cs`
- Modify: `src/MathInsight.Modules.TestGen/Generation/TopicPracticeQuestionSelector.cs`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/TopicPracticeQuestionSelectorTests.cs`

**Interfaces:**
- Consumes: `TopicPracticeRecommendationContext` from Task 3, candidate topic IDs, difficulty levels, last-seen time, and existing `IGenerationRandomizer`.
- Produces: a `TopicPracticeSelection` containing ten selected Questions annotated by whether they actually belong to the representative WeakTag focus subtree.

- [ ] **Step 1: Define failing policy-profile tests**

Add tests:

```csharp
[Theory]
[InlineData(1, 8, 2, 0, 0)]
[InlineData(2, 2, 7, 1, 0)]
public void CreateAdaptive_BuildsApprovedDifficultyProfile(
    byte recommendedLevel, int level1, int level2, int level3, int level4)
```

Assert ten slots, exactly six `FocusPreferred` slots, and the expected total level counts. Add `CreateBaseline_UsesThreeFourTwoOneAndNoFocusSlots`.

- [ ] **Step 2: Define failing selector behavior tests**

Add these exact test methods:

```csharp
Select_AdaptiveParent_UsesAtLeastSixFocusAndAtLeastTwoOutsideWhenPoolPermits
Select_AdaptiveParent_AllowsEightFocusButNotNineWhenTwoOutsideExist
Select_AdaptiveParent_RelaxesBreadthCapWhenOutsidePoolCannotFillTen
Select_DirectWeakTagSelection_AllowsAllTenFromFocusSubtree
Select_FocusSlot_FallsBackToSelectedSubtreeWhenFocusDifficultyIsMissing
Select_PrefersNearestDifficultyAndLowerLevelOnTieWithinRequestedScope
Select_AdaptiveStillCapsCompositeAtTwo
Select_AdaptiveStillPrefersUnseenThenOldestSeen
Select_BaselinePreservesThreeFourTwoOneBehavior
Select_ReturnsIncompleteOnlyWhenCompleteSelectedSubtreeHasFewerThanTenSelectableQuestions
```

For the first test, assert:

```csharp
Assert.InRange(selection.Selected.Count(item => item.IsWeakTagFocus), 6, 8);
Assert.True(selection.Selected.Count(item => !item.IsWeakTagFocus) >= 2);
```

- [ ] **Step 3: Run selector tests and verify RED**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeQuestionSelectorTests"
```

Expected: FAIL because plan/scope/focus annotations do not exist.

- [ ] **Step 4: Add exact plan and result contracts**

Define:

```csharp
public enum TopicPracticeSlotScope
{
    FocusPreferred,
    BreadthPreferred
}

public sealed record TopicPracticeSlot(int TargetDifficultyLevel, TopicPracticeSlotScope Scope);

public sealed record TopicPracticeSelectionPlan(
    IReadOnlyList<TopicPracticeSlot> Slots,
    IReadOnlySet<string> FocusTagIds,
    bool IsDirectFocusSelection,
    string RuleVersion);

public sealed record SelectedTopicPracticeQuestion(
    TopicPracticeCandidate Candidate,
    bool IsWeakTagFocus);

public sealed record TopicPracticeSelection(
    bool IsComplete,
    IReadOnlyList<SelectedTopicPracticeQuestion> Selected);
```

Update the selector interface:

```csharp
TopicPracticeSelection Select(
    IReadOnlyList<TopicPracticeCandidate> candidates,
    TopicPracticeSelectionPlan plan,
    CancellationToken cancellationToken);
```

- [ ] **Step 5: Implement the plan factory**

Use immutable slot lists:

```csharp
public static TopicPracticeSelectionPlan CreateBaseline() => new(
    [
        new(1, TopicPracticeSlotScope.BreadthPreferred), new(1, TopicPracticeSlotScope.BreadthPreferred), new(1, TopicPracticeSlotScope.BreadthPreferred),
        new(2, TopicPracticeSlotScope.BreadthPreferred), new(2, TopicPracticeSlotScope.BreadthPreferred), new(2, TopicPracticeSlotScope.BreadthPreferred), new(2, TopicPracticeSlotScope.BreadthPreferred),
        new(3, TopicPracticeSlotScope.BreadthPreferred), new(3, TopicPracticeSlotScope.BreadthPreferred),
        new(4, TopicPracticeSlotScope.BreadthPreferred)
    ],
    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    false,
    "TopicPractice-v1");
```

Level 1 adaptive slots must be five focus L1, one focus L2, three breadth L1, one breadth L2. Level 2 adaptive slots must be one focus L1, four focus L2, one focus L3, one breadth L1, and three breadth L2. Adaptive rule version is `TopicPractice-WeakTag-v1`.

- [ ] **Step 6: Implement scoped selection and soft breadth cap**

For each slot, order candidates in this sequence:

1. requested scope (`FocusPreferred` selects focus first; `BreadthPreferred` selects outside-focus first),
2. absolute difficulty distance,
3. lower difficulty on a tie,
4. unseen before seen,
5. oldest `LastSeenAt`,
6. existing randomizer only inside the final fully equal priority group.

Maintain unique Question IDs and the global Composite count. For ancestor selection, reserve two outside-focus candidates whenever at least two remain selectable; only relax the focus cap when doing otherwise makes ten Questions impossible. Set `IsWeakTagFocus` from actual overlap with `FocusTagIds`, not merely from the slot label.

- [ ] **Step 7: Run selector tests**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeQuestionSelectorTests"
```

Expected: PASS, including every pre-existing baseline test.

- [ ] **Step 8: Commit selection policy**

```powershell
git add src/MathInsight.Modules.TestGen/Generation/TopicPracticeSelectionContracts.cs src/MathInsight.Modules.TestGen/Generation/TopicPracticeSelectionPlanFactory.cs src/MathInsight.Modules.TestGen/Generation/ITopicPracticeQuestionSelector.cs src/MathInsight.Modules.TestGen/Generation/TopicPracticeQuestionSelector.cs tests/MathInsight.Modules.TestGen.Tests/TopicPracticeQuestionSelectorTests.cs
git commit -m "feat(testgen): select WeakTag-focused practice questions"
```

**Completion criteria:** baseline selection is unchanged, adaptive profiles and breadth behavior are deterministic, no Question is duplicated, and Composite/unseen/oldest rules still hold.

---

### Task 6: Integrate Advice, Audit, Retry Verification, Errors, and Logging

**Files:**
- Modify: `src/MathInsight.Modules.TestGen/Contracts/Tests/GenerateTopicPracticeRequest.cs`
- Create: `src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/PreparedTopicPracticeGeneration.cs`
- Modify: `src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/GenerateTopicPracticeCommandHandler.cs`
- Modify: `src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/TopicPracticePersistenceVerifier.cs`
- Modify: `src/MathInsight.Modules.TestGen/Controllers/StudentTestsController.cs`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/GenerateTopicPracticeTests.cs`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/TopicPracticePersistenceVerifierTests.cs`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/StudentTestsControllerTests.cs`

**Interfaces:**
- Consumes: resolver from Task 3 and selector/plan factory from Task 5.
- Produces: persisted baseline/adaptive audit, additive `GenerateTopicPracticeResponse`, stable 503 mapping, and structured generation logs.

- [ ] **Step 1: Write failing generation tests**

Add these tests:

```csharp
Generate_FlagDisabledOrNoAdvice_PersistsBaselineAudit
Generate_QualifiedLevelOneAdvice_PersistsSixToEightFocusRowsAndAdaptiveResponse
Generate_QualifiedLevelTwoAdvice_PersistsApprovedDifficultyProfile
Generate_ParentSelection_PersistsWeakTagAuditOnlyForFocusQuestions
Generate_DirectWeakTagSelection_AllowsTenAdaptiveRows
Generate_ProviderThrows_ReturnsUnavailableAndWritesNothing
Generate_InvalidAdvice_ReturnsInvalidAndWritesNothing
Generate_RequeriesAdviceAndDoesNotAcceptRecommendationFieldsFromRequest
Generate_AmbiguousCommit_VerifiesAdaptiveAggregateWithoutCallingProviderTwice
```

Add these controller tests:

```csharp
TopicPracticeOptions_RecommenderUnavailable_Returns503WithStableCode
GenerateTopicPractice_RecommendationInvalid_Returns503WithStableCode
```

Assert `ObjectResult.StatusCode == StatusCodes.Status503ServiceUnavailable` and `ApiErrorResponse.Code` equals the original application error code.

For each adaptive focus row assert:

```csharp
Assert.Equal("WeakTagPractice", row.SelectionReason);
Assert.True(row.IsAdaptiveSelected);
Assert.Equal("weak-child", row.RecommendedForTagId);
Assert.Equal("DIFF-1", row.RecommendedDifficultyId);
Assert.Equal(2.40m, row.PtagAtSelection);
Assert.Equal("TopicPractice-WeakTag-v1", row.RuleVersion);
```

For each general row assert `SelectionReason = TopicPractice`, `IsAdaptiveSelected = false`, `RecommendedForTagId = selected tag`, null difficulty/Ptag, and adaptive rule version. For baseline rows retain `TopicPractice-v1`.

- [ ] **Step 2: Write failing persistence-verifier tests**

Add:

```csharp
IsValid_AcceptsMixedAdaptiveAndGeneralAuditForParentSelection
IsValid_AcceptsAllAdaptiveAuditForDirectSelection
IsValid_RejectsMultipleWeakTagIds
IsValid_RejectsAdaptiveRowWithoutDifficultyPointOrWeakRuleVersion
IsValid_RejectsGeneralRowWithAdaptiveFields
```

Update the verifier signature to accept the command-selected tag and expected rule context instead of inferring a single selected tag from every `RecommendedForTagId`.

- [ ] **Step 3: Run generation tests and verify RED**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~GenerateTopicPracticeTests|FullyQualifiedName~TopicPracticePersistenceVerifierTests|FullyQualifiedName~StudentTestsControllerTests"
```

Expected: FAIL on missing response fields, dependencies, adaptive audit, and 503 mapping.

- [ ] **Step 4: Extend generation response additively**

Keep `GenerateTopicPracticeRequest` with only `TagId`. Extend the response:

```csharp
public sealed record GenerateTopicPracticeResponse(
    string TestId,
    string SelectedTagId,
    string SelectedTagName,
    string TestName,
    string TestMode,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    string ScoringPolicy,
    DateTime CreatedTime,
    bool WasAdaptive,
    string? WeakTagId,
    string? WeakTagName,
    byte? RecommendedDifficultyLevel,
    int AdaptiveQuestionCount,
    int FallbackQuestionCount,
    string RuleVersion);
```

- [ ] **Step 5: Split preparation from persistence**

Create the immutable preparation contracts:

```csharp
internal sealed record PreparedTopicPracticeGeneration(
    string TestId,
    string StudentId,
    string SelectedTagId,
    string SelectedTagName,
    string TestName,
    DateTime CreatedTime,
    TopicPracticeRecommendationContext Recommendation,
    string? RecommendedDifficultyId,
    IReadOnlyList<PreparedTopicPracticeQuestion> Questions);

internal sealed record PreparedTopicPracticeQuestion(
    BlueprintExamCandidate Question,
    int QuestionOrder,
    decimal MaxPoints,
    string ScoringRule,
    bool IsWeakTagFocus);
```

Refactor the handler into these responsibilities:

```csharp
private Task<Result<PreparedTopicPracticeGeneration>> PrepareAsync(
    GenerateTopicPracticeCommand command,
    string testId,
    DateTime createdTime,
    CancellationToken cancellationToken);
private Task<Result<GenerateTopicPracticeResponse>> PersistAsync(
    PreparedTopicPracticeGeneration prepared,
    CancellationToken cancellationToken);
private Task<(bool IsSuccessful, Result<GenerateTopicPracticeResponse> Result)> VerifySucceededAsync(
    PreparedTopicPracticeGeneration prepared,
    CancellationToken cancellationToken);
```

`PrepareAsync` must run once before `TestGenerationExecutionStrategy.ExecuteAsync`: validate Student/topic, load active topics/difficulties/candidates/history, call the recommendation resolver once, build the selection plan, select ten Questions, allocate score, and create immutable prepared rows. Generate stable `testId` and `createdTime` once.

Only `PersistAsync` executes inside the EF execution strategy and relational transaction. It checks the stable Test ID, writes the prepared `Test` aggregate, and commits. On ambiguous commit, verification reads the existing aggregate and builds the response from persisted audit without calling the provider again.

- [ ] **Step 6: Persist exact adaptive and baseline audit values**

Resolve the active `DifficultyID` whose `LevelValue` equals the recommendation level. If none or more than one exists, return `TOPIC_PRACTICE_RECOMMENDATION_INVALID` before writes.

For selected rows where `IsWeakTagFocus` is true and advice exists, write the adaptive audit. For all remaining adaptive-plan rows, write the general audit. Keep score allocation, immutable QuestionVersion, question ordering, and scoring-rule snapshots unchanged.

- [ ] **Step 7: Make ambiguous-commit verification adaptive-aware**

Verification must confirm:

```text
owner, TestMode, TestName, duration, count, score and scoring policy
ten unique Question IDs and order 1..10
QuestionVersionID, weight and max-point snapshots remain valid
one rule version across all rows
baseline rows have no adaptive fields
adaptive focus rows share exactly one WeakTag ID, difficulty ID and Ptag
adaptive general rows point to command.TagId and have null difficulty/Ptag
AdaptiveQuestionCount + FallbackQuestionCount = 10
```

Use `command.TagId` to recover the selected topic after retry; do not assume all `RecommendedForTagId` values are identical.

- [ ] **Step 8: Add structured logs**

Inject `ILogger<GenerateTopicPracticeCommandHandler>` and emit one successful-generation information log with structured properties:

```text
StudentId, SelectedTagId, WeakTagId, OfficialPoint, EvidenceCount,
RecommendedDifficultyLevel, AdaptiveQuestionCount, FallbackQuestionCount,
RuleVersion, TestId
```

The resolver already logs provider/contract failures from Task 3; do not duplicate those warnings in the command handler.

- [ ] **Step 9: Map the stable 503 responses**

In `StudentTestsController.ToErrorResult`, map both new errors with:

```csharp
return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorResponse(error));
```

Do not map them to `400`, `409`, or expose exception text.

- [ ] **Step 10: Run generation/controller tests**

Run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~GenerateTopicPracticeTests|FullyQualifiedName~TopicPracticePersistenceVerifierTests|FullyQualifiedName~StudentTestsControllerTests"
```

Expected: PASS.

- [ ] **Step 11: Commit generation integration**

```powershell
git add src/MathInsight.Modules.TestGen/Contracts/Tests/GenerateTopicPracticeRequest.cs src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/PreparedTopicPracticeGeneration.cs src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/GenerateTopicPracticeCommandHandler.cs src/MathInsight.Modules.TestGen/Commands/GenerateTopicPractice/TopicPracticePersistenceVerifier.cs src/MathInsight.Modules.TestGen/Controllers/StudentTestsController.cs tests/MathInsight.Modules.TestGen.Tests/GenerateTopicPracticeTests.cs tests/MathInsight.Modules.TestGen.Tests/TopicPracticePersistenceVerifierTests.cs tests/MathInsight.Modules.TestGen.Tests/StudentTestsControllerTests.cs
git commit -m "feat(testgen): generate audited WeakTag TopicPractice tests"
```

**Completion criteria:** advice is resolved once before persistence, provider failures cause zero writes, retries reuse one prepared aggregate, and persisted audit supports both parent and direct topic selection.

---

### Task 7: Extend the Disposable SQL Server Smoke Test

**Files:**
- Modify: `tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj`
- Modify: `tests/MathInsight.Modules.TestGen.Tests/TopicPracticeSqlServerSmokeTests.cs`

**Interfaces:**
- Consumes: canonical `Database/database/001_Create_MathInsight_Azure.sql`, `RecommenderService`, shared provider contract, TestGen generation handler, and Testing start-session handler.
- Produces: opt-in evidence that semantic IDs, qualified advice, adaptive audit, ambiguous commits, owner access, and unlimited Practice sessions work against the real SQL schema.

- [ ] **Step 1: Reference Recommender from the TestGen test project**

Add:

```xml
<ProjectReference Include="..\..\src\MathInsight.Modules.Recommender\MathInsight.Modules.Recommender.csproj" />
```

This reference is test-only. Do not add a TestGen production reference to Recommender.

- [ ] **Step 2: Write the failing adaptive SQL scenario**

Extend seed data with an active child topic and mastery:

```sql
INSERT INTO [TagTopic]
    ([TagID], [ParentTagID], [TagName], [Grade], [IsActive], [DisplayOrder])
VALUES
    ('TOPIC-G12-DERIVAPP', 'TOPIC-G12-CALCULUS', N'Ứng dụng đạo hàm', 12, 1, 2);

INSERT INTO [TagsMastery]
    ([TagsMasteryID], [StudentID], [TagID], [OfficialPoint], [PracticePoint],
     [ExamAnchor], [MasteryStatus], [NumberDone], [RecommendedDifficultyLevel])
VALUES
    ('TM-STUDENT01-DERIVAPP', 'student_01', 'TOPIC-G12-DERIVAPP', 2.40, 2.40,
     5.00, 'Learning', 5, 1);
```

Tag at least eight candidate Questions to the child and at least two only to the parent. Instantiate a real `RecommenderService`, pass it through a real `TopicPracticeRecommendationResolver`, and generate for the parent.

- [ ] **Step 3: Assert real SQL adaptive behavior and retry safety**

Assert:

```csharp
Assert.True(generation.Value!.WasAdaptive);
Assert.Equal("TOPIC-G12-DERIVAPP", generation.Value.WeakTagId);
Assert.InRange(generation.Value.AdaptiveQuestionCount, 6, 8);
Assert.Equal(10, generation.Value.AdaptiveQuestionCount + generation.Value.FallbackQuestionCount);
Assert.Equal(1, await generationContext.Tests.CountAsync());
Assert.Equal(10, await generationContext.TestQuestions.CountAsync());
```

Query the persisted rows and assert focus/general audit fields. Keep existing owner-only `StartSession`, unlimited time, and ambiguous-commit assertions.

- [ ] **Step 4: Run without the environment variable**

Run:

```powershell
Remove-Item Env:TESTGEN_SQLSERVER_CONNECTION -ErrorAction SilentlyContinue
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeSqlServerSmokeTests"
```

Expected: the smoke test is reported skipped/not run by `SqlServerSmokeFact`; no shared database is contacted.

- [ ] **Step 5: Run against a disposable local SQL Server when available**

Set `TESTGEN_SQLSERVER_CONNECTION` to a local SQL Server account that may create/drop databases, then run:

```powershell
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore --filter "FullyQualifiedName~TopicPracticeSqlServerSmokeTests"
```

Expected: PASS; the temporary database is dropped in `finally` even when an assertion fails. If no disposable local SQL Server exists, leave this gate explicitly recorded as not run; do not use Azure/shared DB as a substitute.

- [ ] **Step 6: Commit SQL smoke coverage**

```powershell
git add tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj tests/MathInsight.Modules.TestGen.Tests/TopicPracticeSqlServerSmokeTests.cs
git commit -m "test(testgen): cover WeakTag TopicPractice on SQL Server"
```

**Completion criteria:** the opt-in test creates and drops its own database, uses semantic IDs, proves adaptive audit and retry idempotency, and never requires a production schema change.

---

### Task 8: Add WeakTag Preview and Adaptive Result UX

**Files:**
- Modify: `frontend/src/components/student/PracticeSetupPanel.jsx`
- Modify: `frontend/src/components/student/TopicPracticeConfirmDialog.jsx`
- Modify: `frontend/src/utils/topicPracticeErrorLocalizer.js`
- Modify: `frontend/src/utils/testGenerationErrorLocalizer.js`
- Verify only: `frontend/src/services/testGeneratorApi.js`

**Interfaces:**
- Consumes: additive option and generation fields from Tasks 4 and 6.
- Produces: weak-topic-first sibling ordering, a Vietnamese “Cần củng cố” preview, adaptive confirmation copy, stable 503 retry UX, and unchanged `{ tagId }` request shape.

- [ ] **Step 1: Add pure frontend normalization helpers inside `PracticeSetupPanel.jsx`**

Add:

```javascript
function normalizeTopicPracticeOption(topic) {
  return {
    ...topic,
    isWeakRecommended: topic?.isWeakRecommended === true,
    officialPoint: Number.isFinite(Number(topic?.officialPoint))
      ? Number(topic.officialPoint)
      : null,
    evidenceCount: Number.isInteger(Number(topic?.evidenceCount))
      ? Number(topic.evidenceCount)
      : null,
    recommendedDifficultyLevel: Number.isInteger(Number(topic?.recommendedDifficultyLevel))
      ? Number(topic.recommendedDifficultyLevel)
      : null,
  };
}

function compareTopicPracticeSiblings(a, b) {
  if (a.isWeakRecommended !== b.isWeakRecommended) {
    return a.isWeakRecommended ? -1 : 1;
  }
  if (a.displayOrder !== b.displayOrder) return a.displayOrder - b.displayOrder;
  return (a.tagName || "").localeCompare(b.tagName || "", "vi");
}
```

Normalize API rows before storing them, and use the comparator only within each sibling array. Do not flatten or globally reorder the hierarchy.

- [ ] **Step 2: Render recommendation metadata without changing topic selection**

For `node.isWeakRecommended`, show a compact badge labeled `Cần củng cố`, then show:

```text
Trọng tâm: {weakTagName}
Mức khuyến nghị: {recommendedDifficultyLevel}
Năng lực hiện tại: {officialPoint formatted to 2 decimals}/10
```

Do not expose `recommendationReason` as raw English UI copy. Preserve 44px minimum action targets, keyboard focus, existing cycle-safe tree construction, search behavior, available count, and disabled insufficient-pool state.

- [ ] **Step 3: Enrich the confirmation dialog**

When `topic.isWeakRecommended` is true, add an un-nested full-width information band inside the existing dialog content:

```text
Bài luyện sẽ ưu tiên chủ đề {weakTagName} ở mức độ {recommendedDifficultyLevel}.
Hệ thống vẫn chọn đủ 10 câu trong phạm vi chủ đề bạn đã chọn.
```

When false, keep the current baseline copy. Do not add a control allowing the Student to edit WeakTag, level, quota, point, or rule version.

- [ ] **Step 4: Map stable 503 errors to Vietnamese**

Add exact mappings:

```javascript
TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE:
  "Hệ thống gợi ý đang tạm thời gián đoạn. Vui lòng thử lại.",
TOPIC_PRACTICE_RECOMMENDATION_INVALID:
  "Dữ liệu gợi ý hiện chưa thể dùng để tạo bài. Vui lòng thử lại sau.",
```

The retry action must call the same fetch/generate function. Never render `err.response.data.message` directly.

- [ ] **Step 5: Preserve API request ownership**

Verify `testGeneratorApi.generateTopicPractice` remains exactly equivalent to:

```javascript
generateTopicPractice(tagId) {
  return client.post("/api/test-generator/tests/topic-practices", { tagId });
}
```

Do not submit preview fields. On generation success, it is acceptable to retain the response only for diagnostics; the existing flow must continue to cache `testId`, call `startSession(testId)`, and navigate to `/student/test/{sessionId}`.

- [ ] **Step 6: Build the frontend**

Run:

```powershell
npm run build
```

Workdir: `frontend`

Expected: Vite production build succeeds with no missing imports or JSX errors.

- [ ] **Step 7: Browser-smoke baseline and adaptive paths**

With Docker/local services running, sign in as the Student test account and verify:

```text
1. Weak-containing siblings appear before non-weak siblings.
2. The badge, child name, level, and point render only when advice exists.
3. A baseline topic still generates and starts an unlimited Practice session.
4. A weak parent opens the adaptive confirmation and sends only { tagId }.
5. Generate/start/autosave/reload/submit still works.
6. A simulated/new 503 message is Vietnamese and retry does not duplicate a started session.
```

- [ ] **Step 8: Commit frontend integration**

```powershell
git add frontend/src/components/student/PracticeSetupPanel.jsx frontend/src/components/student/TopicPracticeConfirmDialog.jsx frontend/src/utils/topicPracticeErrorLocalizer.js frontend/src/utils/testGenerationErrorLocalizer.js
git commit -m "feat(topic-practice): show WeakTag recommendations"
```

**Completion criteria:** frontend consumes additive fields defensively, clearly distinguishes recommended topics, keeps the existing session flow, and never owns adaptive decisions.

---

### Task 9: Run Full Gates, Update Checklists, and Prepare Handoff

**Files:**
- Modify: `specs/005-recommender/tasks.md`
- Modify: `specs/009-test-generator/tasks.md`
- Verify only: all files changed by Tasks 1-8

**Interfaces:**
- Consumes: every implementation and test artifact from Tasks 1-8.
- Produces: verified branch state and truthful completed SpecKit checklist items.

- [ ] **Step 1: Run focused backend suites**

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore
dotnet test tests/MathInsight.Modules.Testing.Tests/MathInsight.Modules.Testing.Tests.csproj --no-restore
```

Expected: PASS except the explicitly opt-in SQL smoke when its environment variable is absent.

- [ ] **Step 2: Run full backend build and tests**

```powershell
dotnet build MathInsight.sln --no-restore
dotnet test MathInsight.sln --no-build --no-restore
```

Expected: zero build errors and all non-opt-in tests pass. Existing package-vulnerability warnings may be recorded but must not be presented as introduced by this checkpoint.

- [ ] **Step 3: Run formatting and whitespace gates**

```powershell
dotnet format src/MathInsight.Shared/MathInsight.Shared.csproj --verify-no-changes --no-restore
dotnet format src/MathInsight.Modules.Recommender/MathInsight.Modules.Recommender.csproj --verify-no-changes --no-restore
dotnet format src/MathInsight.Modules.TestGen/MathInsight.Modules.TestGen.csproj --verify-no-changes --no-restore
dotnet format tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --verify-no-changes --no-restore
dotnet format tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --verify-no-changes --no-restore
git diff --check
```

Expected: all commands exit `0`. Do not bulk-format unrelated modules.

- [ ] **Step 4: Run final frontend build**

```powershell
npm run build
```

Workdir: `frontend`

Expected: PASS.

- [ ] **Step 5: Check implemented SpecKit items only**

Mark the new Recommender shared-provider items and TestGen Phase 8D items complete only when their named tests/gates above passed. Leave disposable SQL smoke unchecked if it was not run. Leave Adaptive BlueprintExam, multiple WeakTags, decay, Redis, and ML items unchecked.

- [ ] **Step 6: Review branch scope**

Run:

```powershell
git status --short
git diff --stat origin/main...HEAD
git log --oneline origin/main..HEAD
```

Expected: no SQL migration/schema files, no Recommender formula changes, no Adaptive BlueprintExam implementation, and no unrelated generated files.

- [ ] **Step 7: Commit checklist evidence**

```powershell
git add specs/005-recommender/tasks.md specs/009-test-generator/tasks.md
git commit -m "docs(topic-practice): complete WeakTag checkpoint gates"
```

**Completion criteria:** all available gates are green, any skipped SQL smoke is disclosed, SpecKit reflects only verified work, and the branch contains no out-of-scope database or adaptive-exam changes.

---

## Requirement-to-Task Traceability

| Requirement | Task |
|---|---|
| Shared string-ID provider, no `IsRemedial` | 2 |
| `OfficialPoint < 5` and `EvidenceCount >= 3` | 2, 3 |
| Feature flag, baseline/no-advice, fail-closed | 3 |
| Deterministic representative WeakTag in active subtree | 3 |
| Additive options preview | 4 |
| Baseline `3/4/2/1` preserved | 5 |
| Level 1 `8/2/0/0`; Level 2 `2/7/1/0` | 5 |
| Six focus, parent breadth cap, direct-tag exception | 5 |
| Nearest/lower fallback, Composite cap, unseen/oldest | 5 |
| Re-query on generation; client sends only `tagId` | 6, 8 |
| Exact `TestQuestion` audit and rule versions | 6 |
| Advice/selection before persistence transaction | 6 |
| Ambiguous-commit verification without second provider call | 6, 7 |
| Stable HTTP 503 contracts | 3, 6, 8 |
| Structured logging | 6 |
| No schema change | Global constraints, 7, 9 |
| Disposable SQL proof | 7 |
| Weak-first frontend UX and Vietnamese errors | 8 |
| Full build/test/format/SpecKit gates | 9 |

## Explicitly Deferred

- Adaptive BlueprintExam and Diagnostic adaptation.
- More than one representative WeakTag per TopicPractice Test.
- Time decay, mastery freshness, answer-level error clustering, and Redis caching.
- Changes to mastery formulas, Grading events, or database schema.
- Student controls for selecting a recommended difficulty or WeakTag.
