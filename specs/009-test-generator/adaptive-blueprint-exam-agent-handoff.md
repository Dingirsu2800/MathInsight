# Adaptive BlueprintExam Agent Handoff

Run in this order: **Terra backend -> Antigravity frontend -> Luna verification -> Codex review**.

## 1. Backend Prompt - Sidechat Terra 5.6 High

```text
Implement Checkpoint 6B Mastery-Aware Personal BlueprintExam in:
E:\Summer_2026\SEP409\Implementation\MathInsight

Use Terra 5.6, effort high. Work on a dedicated feature branch/worktree. Read first:
- specs/constitution.md
- AGENTS.md
- specs/009-test-generator/spec.md
- specs/009-test-generator/plan.md
- specs/009-test-generator/tasks.md
- specs/009-test-generator/adaptive-blueprint-exam-design.md
- specs/009-test-generator/adaptive-blueprint-exam-implementation-plan.md

Implement ONLY Tasks 1-4 of the implementation plan using TDD.

Required behavior:
- Only personal POST /api/test-generator/tests/blueprint-exams becomes mastery-aware.
- Shared Fixed/Random generation must remain unchanged.
- Batch-call MathInsight.Shared IStudentTopicMasteryProvider once for distinct exact blueprint topics.
- Missing or EvidenceCount < 3 keeps original difficulty.
- Qualified point <5 lowers one level; 5..<7.5 keeps; >=7.5 raises one; clamp 1..4.
- Evaluate every slot; no 20% cap and no 40% probability.
- Preserve exact section/topic/type/quantity/part/scoring/duration/score.
- Global assignment prefers target difficulty, falls back only to original, preserves uniqueness, and must not false-fail when a complete assignment exists.
- Use a separate adaptive minimum-cost capacity selector; do not change baseline selector semantics.
- Adaptive audit only when actual selected difficulty is the genuinely adjusted preferred level.
- Add response metadata and stable 503 errors defined by the plan.
- Preserve stable TestID transaction retry and strengthen post-commit aggregate verification.

Hard constraints:
- No schema, SQL migration, Redis, RabbitMQ, internal HTTP, direct TestGen reference to Recommender, new external package, recent-question deduplication, frontend, or Testing session creation.
- Do not edit unrelated modules or package vulnerabilities.
- Do not silently change accepted SpecKit rules. Report a conflict before changing scope.

Verification before reporting:
- focused red/green tests for every task;
- dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore
- dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
- dotnet build MathInsight.sln --no-restore
- dotnet format only touched backend projects/files --verify-no-changes
- git diff --check

Review the complete diff for shared/fixed regression and secrets. Commit backend only with focused commits from the plan. Do not push or merge. Return:
- commit hashes;
- files changed;
- exact test counts/skips;
- API/DTO changes;
- unresolved risks or blockers.
```

## 2. Frontend Prompt - Antigravity

```text
Implement ONLY the Student frontend for Checkpoint 6B after the backend commits are present in:
E:\Summer_2026\SEP409\Implementation\MathInsight

Read first:
- AGENTS.md
- specs/constitution.md
- specs/009-test-generator/adaptive-blueprint-exam-design.md
- Task 5 in specs/009-test-generator/adaptive-blueprint-exam-implementation-plan.md
- the actual backend request/response/error contracts from the current branch
- existing SharedBlueprintExamDiscoveryPage.jsx, StartTestDialog.jsx, PracticeSetupPanel.jsx, testGeneratorApi.js, testingApi.js, and existing tests

Build the approved UX:

[ Tạo đề theo năng lực ]       Kho đề: [ Đề cố định ] [ Đề theo cấu trúc ]

Requirements:
- The create action is a command button, not a third catalog tab.
- Use the existing Material Symbol auto_awesome, not an emoji or custom SVG.
- Keep Fixed/Random server filtering, pagination, TestCode resolution, and start behavior unchanged.
- Clicking the command lazily loads eligible blueprint options.
- Dialog shows blueprint name, grade, section count, question count, duration, and score.
- Explain naturally in Vietnamese: structure stays the same and difficulty may be adjusted from recent learning results.
- Final button: Tạo và bắt đầu.
- Generate exactly once, retain TestID, call existing Testing startSession, and on start failure retry the same TestID without regenerating.
- Add loading, empty, retry, selection, generating, starting, and start-retry states.
- Prevent double submits using state plus an in-flight ref.
- Responsive: desktop command and catalog heading may share a row; mobile stacks them. Interactive controls >=44px.
- Do not expose WeakTag, Ptag, adaptive, baseline, recommender, ma trận, raw IDs, or English fallback copy.
- Use React JavaScript, existing Tailwind/design tokens/components, and existing centralized Axios clients. Add no dependency.

Testing:
- write/update Vitest tests before implementation where practical;
- test lazy options loading;
- test one generation request;
- test start retry does not regenerate;
- test empty/error states and localization;
- test Fixed/Random catalog regression;
- npm test;
- npm run build;
- git diff --check.

Do not change backend, schema, seed, Docker, unrelated dashboard UI, or SpecKit business rules. Commit frontend only, do not push/merge, and report commit hash, changed files, test counts, build result, and any browser-smoke gap.
```

## 3. Verification Prompt - Sidechat Luna

```text
Independently verify the completed backend + frontend Adaptive BlueprintExam branch in:
E:\Summer_2026\SEP409\Implementation\MathInsight

Use Luna. This is verification only: do not modify source, tests, schema, seed, Docker configuration, or database data directly. Application calls may create normal Test/TestSession smoke data. SQL access, if configured, is SELECT-only.

Read:
- specs/009-test-generator/adaptive-blueprint-exam-design.md
- Task 6 in specs/009-test-generator/adaptive-blueprint-exam-implementation-plan.md
- git log/status and the Terra/Antigravity commits

Run the minimum evidence gates:
1. dotnet test tests/MathInsight.Modules.TestGen.Tests/MathInsight.Modules.TestGen.Tests.csproj --no-restore
2. dotnet test tests/MathInsight.Modules.Recommender.Tests/MathInsight.Modules.Recommender.Tests.csproj --no-restore
3. dotnet build MathInsight.sln --no-restore
4. frontend: npm test
5. frontend: npm run build
6. repository root: git diff --check and git status --short

Then run one focused Docker/browser smoke with an existing seeded Student account:
- verify Kho đề still separates Đề cố định and Đề theo cấu trúc;
- open Tạo đề theo năng lực;
- verify eligible blueprint options and natural Vietnamese copy;
- select one blueprint and click Tạo và bắt đầu;
- verify exactly one POST /blueprint-exams and one start-session request;
- verify successful navigation to the returned session;
- verify no severe console errors;
- if start is intentionally failed/retried, verify no second generation POST occurs.

If current data permits, verify a qualified mastery case yields adaptiveQuestionCount > 0. Also verify a missing/insufficient-mastery case falls back to a valid baseline Test; do not edit mastery rows to force it. If data is unavailable, mark only that assertion BLOCKED.

If a safe configured SQL connection exists, run read-only SELECT checks for the generated TestID:
- exact TotalQuestions and continuous QuestionOrder;
- unique QuestionID;
- per-BlueprintDetail quantities;
- adjusted rows use preferred adjacent difficulty and complete audit;
- fallback rows use original difficulty and null recommendation fields;
- MaxPointsSnapshot sum equals Test.MaxScore.

Report PASS/BLOCKED, not a generic success summary. Include exact test counts/skips, commands, TestID, SessionID, network call counts, SQL evidence, console findings, warnings, and worktree status. Do not commit, push, merge, or tick SpecKit tasks.
```

## Review Boundary

After all three reports, send Codex:

- Terra commit hashes and summary;
- Antigravity commit hash and walkthrough;
- Luna verification report;
- any diff not committed;
- current `git status --short --branch`.

Codex will perform a short correctness/scope review only and will not rerun the suites unless the evidence is contradictory or incomplete.
