# Scoring Contract V2

**Status**: Approved implementation checkpoint
**Database source of truth**: `../../Database/database/001_Create_MathInsight_Azure.sql`

## Business Rules

- Question and QuestionPart store positive relative weights, default `1.00`; final points are assigned only when a Test is generated.
- BlueprintExam distributes each section ScoreBudget by selected Question weights. TopicPractice, Diagnostic, and AdaptivePractice normalize all selected weights to Test.MaxScore.
- QuestionVersion is an immutable full snapshot. Its V2 JSON includes content, solution, picture, taxonomy, answers, and parts; scalar version columns remain a compatibility fallback. TestQuestion stores the exact QuestionVersionID, WeightSnapshot, MaxPointsSnapshot, and ScoringRuleSnapshot.
- Answer and QuestionPart rows replaced by an edit are archived instead of deleted so historical TestAnswer foreign keys remain valid. Current authoring/generation queries exclude archived rows.
- Student history renders the linked QuestionVersion and preserves machine grading. An invalidated version overlays effective full points without rewriting TestAnswer.PointsEarned or IsCorrect.
- A session-linked Student report can use `InvalidateAndAwardFull` only after the Question owner creates a newer version or deactivates the Question.

## Scoring Rules

- `AllOrNothing`: correct receives the full question MaxPointsSnapshot.
- `TieredTrueFalse`: exactly four Boolean parts; 0/1/2/3/4 correct receive 0/10/25/50/100 percent.
- `WeightedParts`: parent MaxPointsSnapshot is split by immutable part weights.
- Cent rounding uses largest remainder with QuestionOrder or PartOrder as the stable tie-breaker.

## Persistence Contract

- Question: DefaultWeight, CreatedTime, UpdatedTime.
- Answer: IsArchived.
- QuestionPart: DefaultWeight, IsArchived; current `(QuestionID, PartOrder)` uniqueness is a filtered index.
- QuestionVersion: VersionNumber, SnapshotSchemaVersion, immutable JSON payload.
- Blueprint: TotalScore. BlueprintSection: ScoreBudget and ScoringRule.
- Test: MaxScore and ScoringPolicy.
- TestQuestion: QuestionVersionID, scoring snapshots, invalidation audit.
- QuestionReport: SessionID, QuestionVersionID, ResolutionAction, ScoreAdjustedTime.
- TestSession and StudentTopicSessionResult: GradeRevision.

## Integration Contract

- Testing validates submitted AnswerID/PartID against both relational existence and the TestQuestion QuestionVersion snapshot.
- Grading reads immutable answers and scoring snapshots, then publishes GradeCalculatedEvent with GradeRevision and weighted topic totals.
- Recommender replaces only a newer session/topic revision and replays affected mastery; scoring weights remain independent from difficulty evidence weight `w_D`.
- Automatic score adjustment applies only to Student reports created from a trusted TestSession context in this MVP.

## Delivery Tasks

- [ ] SQL migration, fresh schema, and seed are aligned and idempotent.
- [x] QuestionBank weight/archive/version/report contracts are implemented.
- [x] TestGen Blueprint budgets and generated scoring snapshots are implemented.
- [x] Testing renders and validates immutable snapshots and supports session reports.
- [x] Grading supports snapshot scoring, effective points, and idempotent recalculation.
- [x] Recommender consumes revision-aware weighted topic results.
- [x] Expert and Student frontend contracts are updated.
- [ ] Backend tests/build, frontend build, and disposable SQL Server/Docker smoke pass.
