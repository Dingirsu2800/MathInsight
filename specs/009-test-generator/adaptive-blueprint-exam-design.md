# Adaptive BlueprintExam Design

**Approved:** 2026-08-19
**Scope:** Checkpoint 6B - mastery-aware personal BlueprintExam
**Status:** Approved for implementation

## Goal

Allow a Student to choose an approved/active blueprint and create `Tạo đề theo năng lực`. The generated Test remains faithful to the approved blueprint structure while moving each eligible detail at most one difficulty level according to that Student's topic mastery.

## Product Boundaries

- Personal generation uses the existing `POST /api/test-generator/tests/blueprint-exams` route and stores `GeneratedForStudentID`, `TestCode = NULL`, `TestMode = BlueprintExam`, and `GeneratedBy = System`.
- Shared `Đề cố định` and `Đề theo cấu trúc` remain non-adaptive, reusable, code-addressable Tests.
- Adaptive generation does not change section, topic, question type, quantity, part shape, scoring rule, score budget, duration, or total question count.
- TestGen creates only Test/TestQuestion. Testing still creates TestSession/TestAnswer.
- No database/schema/migration, Redis, queue, internal HTTP, or direct TestGen-to-Recommender project reference is added.

## Student Experience

The Student exam page presents one create action and one shared catalog:

```text
[ Tạo đề theo năng lực ]       Kho đề: [ Đề cố định ] [ Đề theo cấu trúc ]
```

`Tạo đề theo năng lực` opens a dialog that lazily loads eligible blueprint options. The Student selects a blueprint, reviews grade, sections, question count, score, and duration, then chooses `Tạo và bắt đầu`.

The frontend calls generation once and immediately calls Testing `StartSession` with the returned TestID. If session start fails, it retains that TestID and offers `Thử bắt đầu lại`; it must not generate a replacement Test. The generated personal Test never appears in the shared Fixed/Random catalog, but its session appears in normal Student history.

## Mastery Policy

TestGen requests mastery once for all distinct exact topic IDs in the blueprint through `IStudentTopicMasteryProvider`.

| Evidence and OfficialPoint | Preferred level relative to blueprint |
|---|---|
| Missing or `EvidenceCount < 3` | Original level |
| `0.00 <= point < 5.00` | Original minus one |
| `5.00 <= point < 7.50` | Original level |
| `7.50 <= point <= 10.00` | Original plus one |

Levels are clamped to `1..4`. A weak level-1 slot and a strong level-4 slot therefore remain unchanged. Missing mastery is a normal baseline path, not an error. Invalid point/evidence data is a provider-contract error.

## Selection Design

The existing baseline capacity selector remains unchanged. A new adaptive selector receives baseline requirements, per-detail preferred difficulty plans, and candidates from the union of original and preferred difficulty IDs.

It computes a complete minimum-cost capacity assignment:

- each Question has capacity one;
- each BlueprintDetail requires its exact Quantity;
- candidate shape must match topic, question type, scoring rule, and composite part count;
- a preferred-difficulty edge has cost `0`;
- an original-difficulty fallback edge has cost `1`;
- no other difficulty edge is allowed;
- candidate order is shuffled before assignment so equal-cost choices remain random and tests can inject deterministic randomization.

The selector first maximizes total flow, then minimizes fallback cost. This preserves global uniqueness and avoids false insufficient-pool failures with multi-topic Questions. Incomplete flow returns the existing `TEST_GENERATION_INSUFFICIENT_QUESTIONS` before persistence.

## Audit Contract

An actually adjusted row stores:

```text
SelectionReason = BlueprintNormal
IsAdaptiveSelected = true
RecommendedForTagID = BlueprintDetail.TagID
RecommendedDifficultyID = preferred DifficultyID
PtagAtSelection = OfficialPoint
RuleVersion = BlueprintExam-Mastery-v1
```

A neutral row, insufficient-evidence row, clamped-no-change row, or preferred-pool fallback row stores the existing baseline audit shape: `IsAdaptiveSelected = false` and recommendation fields null.

The generation response adds:

```text
wasAdaptive
adaptiveQuestionCount
baselineQuestionCount
ruleVersion = BlueprintExam-Mastery-v1
```

## Failure Contract

| Code | HTTP | Meaning |
|---|---:|---|
| `ADAPTIVE_EXAM_MASTERY_UNAVAILABLE` | 503 | Batch mastery provider failed technically |
| `ADAPTIVE_EXAM_MASTERY_INVALID` | 503 | Qualified mastery advice is malformed |
| Existing BlueprintExam codes | unchanged | Authentication, blueprint, grade, structure, pool, or persistence failure |

All validation and candidate assignment complete before Test/TestQuestion writes. Provider and selection failures leave no partial Test.

## Acceptance

- Every personal BlueprintExam evaluates every detail without a percentage cap or adaptive probability.
- Qualified weak mastery lowers at most one level; qualified strong mastery raises at most one level.
- Missing/insufficient mastery and preferred-pool shortage preserve the original blueprint difficulty.
- Exact blueprint structure, global uniqueness, scoring snapshots, aggregate transaction, stable TestID retry, and Testing ownership remain intact.
- Shared Fixed/Random generation behavior is unchanged.
- Frontend copy contains no `WeakTag`, `Ptag`, `adaptive`, `baseline`, `recommender`, or `ma trận` terms.
