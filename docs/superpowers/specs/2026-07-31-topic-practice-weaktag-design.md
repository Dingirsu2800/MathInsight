# WeakTag-Aware Topic Practice Design

**Date:** 2026-07-31
**Status:** Approved for implementation planning
**Scope:** TestGen TopicPractice, Recommender shared advice contract, and Student TopicPractice UI

## Context

Checkpoint 6C currently generates a personal, unlimited, ten-question TopicPractice Test from one Student-selected active topic subtree. It uses a fixed level profile of `3/4/2/1`, allows at most two Composite Questions, and prefers unseen Questions before the oldest previously seen Questions.

The Recommender already derives per-Student WeakTags from `TagsMastery`, but TopicPractice does not consume that advice. Current TopicPractice persistence confirms the baseline behavior:

- `SelectionReason = TopicPractice`
- `IsAdaptiveSelected = false`
- `RecommendedDifficultyID = NULL`
- `RuleVersion = TopicPractice-v1`

This checkpoint adds WeakTag-aware selection without implementing Adaptive BlueprintExam, changing the mastery formula, or adding database columns.

## Goals

- Keep the Student in control of the selected TopicPractice topic.
- Use qualified WeakTag evidence from the selected active subtree to adapt the ten-question difficulty profile.
- Focus at least six slots on the weakest qualified descendant when the candidate pool permits it.
- Preserve coverage of a selected parent topic when the pool permits it.
- Record enough per-question audit data to explain the recommendation at generation time.
- Keep TestGen independent of Recommender implementation and persistence details.
- Fail without partial writes when an enabled recommendation provider is unavailable or returns an invalid contract.
- Keep the existing unlimited Testing and grading flow unchanged.

## Non-Goals

- Adaptive BlueprintExam generation.
- Multiple WeakTags influencing one TopicPractice Test.
- Freshness or mastery decay over a 30- or 60-day window.
- Prioritizing previously incorrect Questions.
- Changing the `OfficialPoint` or recommended-difficulty formulas.
- Redis, ML, queues, a separate recommendation service, or an admin rule editor.
- A new recommendation snapshot table or a database migration.
- Removing the existing `IsRemedial` field from the Recommender REST contract.

## Terminology

- **Selected topic:** the active topic explicitly selected by the Student.
- **Selected subtree:** the selected topic and all of its active descendants in the Student's current grade.
- **Qualified WeakTag:** a tag with `OfficialPoint < 5.00` and `EvidenceCount >= 3`.
- **Representative WeakTag:** the one qualified WeakTag selected from the active selected subtree to drive this Test.
- **Focus candidate:** a Question tagged to the representative WeakTag subtree.
- **General candidate:** any valid Question tagged to the selected subtree.
- **Baseline policy:** the current `3/4/2/1` level profile without recommendation audit.
- **Adaptive policy:** the Level 1 or Level 2 WeakTag profile defined below.

## Architecture

### Shared Recommendation Boundary

Add a neutral in-process contract to `MathInsight.Shared`:

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

IDs remain `string` because the canonical database supports semantic IDs. The shared contract deliberately omits `IsRemedial`. For this MVP, recommended level 1 identifies the intensive recovery profile, while level 2 identifies the reinforcement profile.

`RecommenderService` implements both its existing module API and `IStudentRecommendationProvider`. Its existing REST/module DTO may retain `IsRemedial` to avoid a breaking change. TestGen references only `MathInsight.Shared`; it must not reference the Recommender project, call the Recommender REST API, or read `TagsMastery` directly.

The provider returns only qualified WeakTag advice. An empty list is a successful `NoAdvice` result. Technical failures are exceptions and are distinct from an empty result.

### Dependency Injection

The WebAPI composition root registers the Recommender implementation for `IStudentRecommendationProvider`. TestGen consumes the interface through constructor injection in both the options query and generation command.

No Redis, HTTP client, message broker, or third-party service is introduced.

## Feature Flag

Configuration includes:

```json
{
  "TopicPractice": {
    "WeakTagAdaptiveEnabled": true
  }
}
```

Behavior is explicit:

| Flag | Provider outcome | Result |
|---|---|---|
| Disabled | Provider is not called | Baseline `3/4/2/1` |
| Enabled | Empty qualified advice | Baseline `3/4/2/1` |
| Enabled | Valid qualified advice | WeakTag-aware generation |
| Enabled | Provider throws | HTTP 503, zero writes |
| Enabled | Advice violates the contract | HTTP 503, zero writes |

The kill switch is operational, not a silent error fallback. An explicitly disabled feature uses baseline generation. An enabled but broken provider fails closed.

## WeakTag Resolution

TestGen builds the cycle-safe active selected subtree first, then filters provider advice to tags in that subtree. Advice outside the active subtree is ignored rather than treated as invalid.

The representative WeakTag is selected deterministically:

1. Lowest `OfficialPoint`.
2. Lowest recommended level when points are equal.
3. Deepest tag in the selected subtree when still equal.
4. Lowest `DisplayOrder`.
5. Ordinal `TagId` comparison as the final tie-break.

Only advice satisfying all of the following can drive generation:

```text
TagId is non-empty
0.00 <= OfficialPoint < 5.00
EvidenceCount >= 3
RecommendedDifficultyLevel is 1 or 2
TagId belongs to the active selected subtree
```

The existing recommendation mapping means qualified WeakTags can only resolve to levels 1 and 2. Any provider row with an out-of-range point, negative evidence, or a qualified weak point mapped to level 3 or 4 is an invalid contract.

## Selection Policies

### Baseline

When adaptive generation is disabled or no qualified WeakTag exists:

| Level | Questions |
|---:|---:|
| 1 | 3 |
| 2 | 4 |
| 3 | 2 |
| 4 | 1 |

All existing baseline selection and audit behavior remains unchanged.

### WeakTag Level 1

Target profile:

| Pool | Level 1 | Level 2 | Level 3 | Level 4 | Total |
|---|---:|---:|---:|---:|---:|
| Focus slots | 5 | 1 | 0 | 0 | 6 |
| General slots | 3 | 1 | 0 | 0 | 4 |
| Total | 8 | 2 | 0 | 0 | 10 |

### WeakTag Level 2

Target profile:

| Pool | Level 1 | Level 2 | Level 3 | Level 4 | Total |
|---|---:|---:|---:|---:|---:|
| Focus slots | 1 | 4 | 1 | 0 | 6 |
| General slots | 1 | 3 | 0 | 0 | 4 |
| Total | 2 | 7 | 1 | 0 | 10 |

### Coverage Rules

- When the Student selects an ancestor of the representative WeakTag, six slots first target the representative WeakTag subtree.
- When the pool permits it, at least two selected Questions must come from outside the representative WeakTag subtree. This limits focus coverage to eight Questions and preserves selected-parent breadth.
- When the Student directly selects the representative WeakTag, the coverage cap is disabled and all ten Questions may come from that subtree.
- The coverage cap is a preferred constraint, not a reason for a false insufficient-pool error. If fewer than two valid outside-focus candidates exist but the complete selected subtree still has ten valid unique Questions, the selector relaxes the cap only as much as needed and records the fallback count.
- If the focus pool contains fewer than six valid unique Questions, focus slots fall back to the complete selected subtree. `AdaptiveQuestionCount` records the actual count rather than claiming six.

### Per-Slot Fallback

For each slot, selection proceeds in this order:

1. Exact target level in the requested pool.
2. Nearest level in the requested pool, preferring the lower level on equal distance.
3. For an unfilled focus slot, exact then nearest level in the selected subtree.
4. Relax the parent coverage cap only when required to reach ten valid unique Questions.

Across all steps, existing invariants remain:

- Exactly ten unique Questions or no Test is written.
- At most two Composite Questions.
- Only current valid QuestionVersion V2 candidates.
- Unseen Questions before seen Questions.
- Oldest `LastSeenAt` before more recently seen Questions.
- Candidate ties use the existing injected `IGenerationRandomizer`.

## Persistence and Audit

No schema changes are required. Existing `TestQuestion` recommendation fields are used.

Focus Questions selected from the representative WeakTag policy store:

```text
SelectionReason = WeakTagPractice
IsAdaptiveSelected = true
RecommendedForTagID = representative WeakTag ID
RecommendedDifficultyID = DifficultyID resolved from RecommendedDifficultyLevel
PtagAtSelection = OfficialPoint
RuleVersion = TopicPractice-WeakTag-v1
```

General Questions store:

```text
SelectionReason = TopicPractice
IsAdaptiveSelected = false
RecommendedForTagID = selected topic ID
RecommendedDifficultyID = NULL
PtagAtSelection = NULL
RuleVersion = TopicPractice-WeakTag-v1
```

Baseline Questions retain:

```text
SelectionReason = TopicPractice
IsAdaptiveSelected = false
RecommendedForTagID = selected topic ID
RecommendedDifficultyID = NULL
PtagAtSelection = NULL
RuleVersion = TopicPractice-v1
```

The database stores `RecommendedDifficultyID`, not the numeric level. Audit consumers resolve `TagDifficulty.LevelValue` through the existing foreign key.

The selected topic remains represented by the Test name, generation response, and general-question audit rows. The MVP does not add `Test.RequestedTagID`.

## Transaction and Retry Boundaries

Generation order is:

```text
Validate Student and selected topic
Resolve active selected subtree
Read latest recommendation advice
Validate advice
Load candidate pool and seen history
Build and validate all ten selections in memory
Allocate ten points
Begin relational transaction
Persist Test and TestQuestions
Commit
```

Provider calls and selection occur before persistence. Provider failure, invalid advice, or an insufficient complete pool writes neither `Test` nor `TestQuestion` rows.

The existing relational execution strategy remains. One request keeps a stable `TestId` and `CreatedTime` across retries. Ambiguous-commit verification must validate the applied rule version, audit fields, selected count, total score, and recommendation summary before treating a persisted aggregate as successful.

## API Contracts

Routes remain unchanged:

```http
GET  /api/test-generator/tests/topic-practice-options
POST /api/test-generator/tests/topic-practices
```

The POST body remains server-authoritative and contains only:

```json
{
  "tagId": "TOPIC-G12-OXYZ"
}
```

The client must not submit WeakTag ID, point, evidence, difficulty, quota, or rule version.

### Topic Options

Each topic option adds:

```csharp
bool IsWeakRecommended,
string? WeakTagId,
string? WeakTagName,
decimal? OfficialPoint,
int? EvidenceCount,
byte? RecommendedDifficultyLevel,
string? RecommendationReason
```

For a selected parent preview, these fields describe the representative WeakTag that would currently drive generation. The generation command calls the provider again and uses the newest advice.

### Generation Response

The generation response adds:

```csharp
bool WasAdaptive,
string? WeakTagId,
string? WeakTagName,
byte? RecommendedDifficultyLevel,
int AdaptiveQuestionCount,
int FallbackQuestionCount,
string RuleVersion
```

These fields report the policy actually persisted, not the earlier options preview.

### Stable Errors

| Code | HTTP | Meaning |
|---|---:|---|
| `TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE` | 503 | Enabled provider failed technically |
| `TOPIC_PRACTICE_RECOMMENDATION_INVALID` | 503 | Enabled provider returned a contract TestGen cannot safely apply |
| `TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS` | 409 | The complete selected subtree cannot provide ten valid unique Questions |
| `TOPIC_PRACTICE_GENERATION_CONFLICT` | 409 | The persisted aggregate cannot be verified after retry or ambiguous commit |

Existing authentication, Student, topic, and availability errors remain unchanged.

## Frontend UX

Within each sibling group, topics with a qualified WeakTag preview appear first, then retain existing `DisplayOrder` and name ordering.

Qualified topics show:

- Badge: `Cần củng cố`.
- Current `OfficialPoint` as a score such as `2.4/10`, not as a weakness probability.
- Recommended label:
  - Level 1: `Nền tảng`
  - Level 2: `Củng cố`

If the Student selects a parent, the confirmation dialog names the representative weak descendant:

```text
Bài luyện tập sẽ ưu tiên "Ứng dụng đạo hàm"
vì đây là phần bạn cần củng cố.
```

The final UI source uses proper Vietnamese diacritics. It does not expose formulas, claim a calibrated weakness percentage, or promise that one Test is sufficient to change mastery.

Topics below the evidence threshold use the normal baseline presentation. A changed recommendation between preview and generation is accepted; the backend response describes the applied policy.

Both new 503 codes map to stable Vietnamese messages with a retry action. Raw backend messages are never displayed.

## Logging

Generation emits structured logs containing:

```text
StudentId
SelectedTopicId
WeakTagId
OfficialPoint
EvidenceCount
RecommendedDifficultyLevel
AdaptiveQuestionCount
FallbackQuestionCount
RuleVersion
GenerationResult
```

Logs must not include answer content, tokens, or complete Question content.

## Testing Strategy

### Shared and Recommender

- Semantic string IDs pass through the shared contract.
- Advice requires `OfficialPoint < 5.00` and `NumberDone >= 3`.
- Level 1 and level 2 mapping is preserved.
- Empty advice is successful and distinct from a provider exception.
- The composition root resolves `IStudentRecommendationProvider` to the Recommender implementation.

### TestGen Selection

- No advice uses `3/4/2/1`.
- Level 1 uses `8/2/0/0` when the pool can fulfill exact targets.
- Level 2 uses `2/7/1/0` when the pool can fulfill exact targets.
- Parent selection chooses the weakest qualified active descendant using every deterministic tie-break.
- Six focus slots are fulfilled when the focus pool permits it.
- Parent selection keeps at least two outside-focus Questions when the pool permits it.
- Direct representative-tag selection disables the coverage cap.
- Focus and coverage fallbacks still produce ten unique Questions when the complete pool permits it.
- Composite cap, unseen-first, oldest-seen, nearest-level, and lower-level tie behavior remain intact.

### TestGen Generation

- Disabled flag never calls the provider and persists baseline audit.
- Provider exception returns 503 with zero writes.
- Invalid advice returns 503 with zero writes.
- Complete pool below ten returns 409 with zero writes.
- Focus and general rows persist their distinct audit values.
- `MaxPointsSnapshot` totals exactly `10.00`.
- Generation re-reads advice instead of trusting preview or client input.
- Retry and ambiguous-commit verification preserve one Test identity and validate recommendation audit.

### SQL Smoke

The opt-in disposable SQL Server smoke uses `TESTGEN_SQLSERVER_CONNECTION`, creates a temporary database from the canonical schema, seeds semantic IDs and `TagsMastery`, verifies baseline/level-1/level-2 generation, inspects persisted audit fields, and drops the database in `finally`. It must never run against Azure or a shared team database.

### Frontend

The project does not add a frontend test framework solely for this checkpoint. Verification covers production build and browser smoke for sibling sorting, badges, modal copy, baseline behavior, request shape, 503 retry, and the existing generate/start/autosave/resume/submit flow.

## Quality Gates

```powershell
dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore
dotnet test tests/MathInsight.Modules.Testing.Tests/MathInsight.Modules.Testing.Tests.csproj --no-restore
dotnet build MathInsight.sln --no-restore
dotnet test MathInsight.sln --no-build --no-restore
npm run build
git diff --check
```

Pre-existing package vulnerability and unrelated formatting warnings do not expand this checkpoint, but new or modified files must introduce no new build, test, formatting, or whitespace failures.

## SpecKit Updates Required During Implementation

- `specs/005-recommender/spec.md`: shared provider, evidence threshold for TestGen advice, and string-ID contract.
- `specs/005-recommender/plan.md`: provider boundary and no external infrastructure.
- `specs/005-recommender/tasks.md`: provider and TestGen-consumer quality gates.
- `specs/009-test-generator/spec.md`: WeakTag-aware TopicPractice policy, API additions, errors, and audit.
- `specs/009-test-generator/plan.md`: update Checkpoint 6C successor scope.
- `specs/009-test-generator/tasks.md`: add a separately tracked WeakTag-aware TopicPractice phase without marking Adaptive BlueprintExam complete.
