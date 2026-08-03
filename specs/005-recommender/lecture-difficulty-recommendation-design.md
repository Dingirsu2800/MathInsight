# Lecture Difficulty Recommendation Design

**Date**: 2026-08-03

**Status**: Approved design, pending implementation plan

**Affected areas**: Database, Learning & Lecture, Recommender, Student and Teacher frontend

## 1. Goal

Provide personalized lecture recommendations using both the student's topic mastery and the difficulty assigned to each lecture.

The feature supports:

- Weak-topic remediation.
- Continued learning for students at intermediate and advanced mastery levels.
- Safe lower-difficulty fallback when no lecture exists at the exact target level.
- Grade-based foundation recommendations for students without enough mastery evidence.

The MVP remains SQL Server and rule based. It does not require Redis, Python, machine learning, or an external recommendation service.

## 2. Approved Decisions

1. Every new lecture has exactly one `TagID` and one `DifficultyID`.
2. `Lecture.DifficultyID` references the existing `TagDifficulty` taxonomy used by Question Bank.
3. Recommendation covers all four difficulty levels, not only weak-topic levels 1 and 2.
4. Exact difficulty is preferred. If unavailable, use the nearest lower difficulty.
5. A lecture above the student's target difficulty is never selected as fallback.
6. A personalized mastery row requires at least three completed evidence items.
7. Return at most two lectures per topic and six lectures overall.
8. Students without qualified mastery evidence receive grade-based level-1 foundation lectures.
9. Recommendation results are calculated on demand and are not stored in a new table.

## 3. Data Model

Add a nullable transition column to an existing database:

```sql
ALTER TABLE dbo.Lecture
ADD DifficultyID VARCHAR(36) NULL;
```

Add the foreign key and recommendation index:

```sql
ALTER TABLE dbo.Lecture
ADD CONSTRAINT FK_Lecture_TagDifficulty_DifficultyID
FOREIGN KEY (DifficultyID)
REFERENCES dbo.TagDifficulty(DifficultyID);

CREATE INDEX IX_Lecture_Status_TagID_DifficultyID
ON dbo.Lecture(Status, TagID, DifficultyID);
```

For a fresh database, `Lecture.DifficultyID` is `NOT NULL` from creation.

### Existing Lecture Migration

- Do not assign an arbitrary default difficulty to existing lectures.
- Existing lectures with `DifficultyID = NULL` remain editable and viewable.
- A legacy lecture cannot be published until a Teacher or Admin assigns a valid active difficulty.
- Recommender excludes lectures with a null difficulty.
- After all legacy rows are classified, a later migration changes the column to `NOT NULL`.

The schema source, migration script, seed script, ERD, and submitted database documentation must be updated together because the constitution protects the current database contract.

## 4. Module Ownership

### Learning & Lecture

Learning owns lecture creation, update, publication, and presentation. It must:

- Accept and return `DifficultyID`.
- Validate that the selected topic and difficulty exist and are active.
- Require difficulty for newly created lectures.
- Prevent publication of a legacy lecture without difficulty.
- Expose active difficulties to the Teacher editor through a read-only endpoint.

Learning may read `TagDifficulty` through a cross-module read model. The read model must use `ExcludeFromMigrations()` and must never write taxonomy data.

### Recommender

Recommender owns candidate selection and ranking. It reads `Student`, `TagsMastery`, `TagTopic`, `TagDifficulty`, and `Lecture` through SQL Server. Cross-module entities are read-only and excluded from migrations.

Recommender never creates or updates lectures.

## 5. Personalized Algorithm

### 5.1 Qualified Mastery

A mastery row can drive personalized recommendation when:

```text
NumberDone >= 3
TagTopic.IsActive = true
```

Topics are prioritized as follows:

| Priority | Classification | OfficialPoint |
|---:|---|---:|
| 1 | Weak | `< 5.00` |
| 2 | Learning | `5.00 to < 7.50` |
| 3 | Mastered progression | `>= 7.50` |

The target difficulty is `TagsMastery.RecommendedDifficultyLevel`.

### 5.2 Candidate Eligibility

A lecture candidate must satisfy:

```text
Lecture.Status = Published
Lecture.TagID = TagsMastery.TagID
Lecture.DifficultyID is not null
TagTopic.IsActive = true
TagDifficulty.IsActive = true
TagDifficulty.LevelValue <= RecommendedDifficultyLevel
```

### 5.3 Ranking

Topics are ordered by:

1. Classification priority: Weak, Learning, Mastered progression.
2. `OfficialPoint` ascending.
3. `NumberDone` descending.
4. `TagID` ascending for deterministic output.

Lectures within a topic are ordered by:

1. Exact target difficulty.
2. Nearest lower difficulty.
3. `Likes` descending.
4. `UpdatedTime` descending.
5. `LectureID` ascending for deterministic output.

Take at most two lectures for each topic and six lectures overall. The MVP uses deterministic tuple ordering rather than an artificial weighted score.

## 6. Cold Start

Cold start applies when the student has no mastery rows with `NumberDone >= 3`.

Candidates must be:

```text
Student.CurrentGrade matches TagTopic.Grade
TagTopic.IsActive = true
TagDifficulty.IsActive = true
TagDifficulty.LevelValue = 1
Lecture.Status = Published
```

Order by `Likes` descending, `UpdatedTime` descending, and `LectureID` ascending. Return at most six results.

If `Student.CurrentGrade` is null or there are no eligible lectures, return an empty successful result.

Cold-start recommendations use reason `ColdStartGradeFoundation` and must not claim to be based on measured mastery.

## 7. API Contracts

Keep the existing student route:

```text
GET /api/v1/recommender/lectures
```

Recommended response shape:

```json
{
  "lectureId": "lecture_01",
  "title": "Ung dung dao ham co ban",
  "thumbnailUrl": "https://example.invalid/thumbnail.jpg",
  "tagId": "TOPIC-G12-DERIVAPP",
  "tagName": "Ung dung dao ham",
  "difficultyId": "DIFF-LEVEL-2",
  "difficultyName": "Thong hieu",
  "difficultyLevel": 2,
  "targetDifficultyLevel": 2,
  "officialPoint": 3.80,
  "evidenceCount": 5,
  "likes": 12,
  "isDifficultyFallback": false,
  "reason": "WeakTopicExactDifficulty"
}
```

`OfficialPoint` is nullable and `EvidenceCount` is zero for cold-start results. The recommendation list must not return the complete `Lecture.Content` field.

Supported audit reasons:

- `WeakTopicExactDifficulty`
- `WeakTopicLowerDifficultyFallback`
- `ProgressionExactDifficulty`
- `ProgressionLowerDifficultyFallback`
- `ColdStartGradeFoundation`

Learning create and update contracts add `difficultyId`. Learning lecture responses add `difficultyId`, `difficultyName`, and `difficultyLevel`.

Learning exposes `GET /api/v1/difficulties` as an authenticated, read-only active difficulty endpoint for the Teacher editor, following the existing Learning taxonomy endpoint pattern.

## 8. Frontend Behavior

### Teacher

- Lecture editor requires a difficulty selection.
- The difficulty list only contains active values.
- Changing topic does not reset difficulty because difficulty taxonomy is global.
- Legacy lectures without difficulty display a required-classification warning.
- Publish remains disabled until a difficulty is selected.

### Student

The recommendation card displays:

- Real thumbnail when available.
- Lecture title.
- Topic and lecture difficulty.
- Recommendation explanation.
- Current topic point when the recommendation is mastery based.
- A foundation label instead of a mastery claim for cold start.

Selecting a card navigates to `/student/lectures/{lectureId}`.

When a lower-difficulty fallback is used, the UI explains that the lecture is a foundation step before the target level.

## 9. Error Contracts

New stable error codes:

| Code | HTTP | Meaning |
|---|---:|---|
| `LECTURE_DIFFICULTY_REQUIRED` | 400 | Create or publish lacks difficulty |
| `LECTURE_DIFFICULTY_NOT_FOUND` | 404 | Difficulty does not exist |
| `LECTURE_DIFFICULTY_INACTIVE` | 409 | Difficulty is inactive |
| `LECTURE_TOPIC_INACTIVE` | 409 | Topic is inactive |
| `LECTURE_RECOMMENDATION_UNAVAILABLE` | 503 | Technical recommendation query failure |

No eligible recommendations are represented by `200 OK` with `[]`.

## 10. Verification

### Learning Tests

- Reject create without difficulty.
- Reject missing or inactive difficulty.
- Update difficulty successfully.
- Prevent publication of a legacy null-difficulty lecture.
- Return difficulty metadata in lecture DTOs.

### Recommender Tests

- Select exact topic and difficulty first.
- Fall back to the nearest lower level.
- Never select a level above the target.
- Exclude inactive topic and difficulty records.
- Exclude Draft, Deactivated, and null-difficulty lectures.
- Ignore mastery evidence below three completed items.
- Enforce two-per-topic and six-overall limits.
- Prioritize Weak before Learning and Mastered progression.
- Cold start uses current grade and level 1.
- Null grade returns an empty list.
- Semantic IDs such as `student_01` work without GUID parsing.

### SQL and Quality Gates

- Verify the Lecture-to-TagDifficulty foreign key.
- Verify the `(Status, TagID, DifficultyID)` index.
- Verify all cross-module read models are excluded from migrations.
- Run the migration against a disposable SQL Server database.
- Run Learning tests, Recommender tests, full solution tests, `dotnet build`, frontend build, formatter verification, and `git diff --check`.

## 11. Out of Scope

- Redis or persisted recommendation cache.
- Machine-learning ranking.
- Collaborative filtering.
- Using activity history to estimate lecture completion.
- Recommending a lecture above the student's target level.
- A many-to-many lecture-to-difficulty relationship.
