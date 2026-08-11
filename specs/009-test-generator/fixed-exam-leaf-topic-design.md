# Fixed Exam And Direct-Child Topic Design

**Status:** Approved design, 2026-08-11

## Objective

Add Expert-authored fixed Blueprint exams, restrict academic assignment and TopicPractice to direct-child topics, permit Students to practice topics from their current or lower grades, and standardize Vietnamese copy in the Expert dashboard.

## Decisions

### Fixed Blueprint exams

- A fixed exam must belong to a Blueprint owned by the current Expert.
- The Blueprint must be `Approved` or `Active` and structurally valid.
- The Expert chooses every Question and the global Question order.
- The selection must fulfill every BlueprintDetail quantity and its grade, topic, difficulty, Question type, scoring rule, and part-count requirements.
- Creation immediately persists an `Active`, shared `BlueprintExam` with `GeneratedBy = Expert` and `GeneratedForStudentID = NULL`.
- Every TestQuestion freezes the selected QuestionVersion, order, source BlueprintDetail, weights, allocated points, and scoring rule.
- Random and fixed Tests may coexist under the same Blueprint.
- Each Test is archived independently. Archive blocks new sessions but preserves existing sessions and history. Reactivation is outside MVP.
- Deactivating a Blueprint makes all of its Tests unavailable for new sessions.

### Topic taxonomy

- Topic hierarchy is exactly two levels: root grouping topic and direct-child assignable topic.
- A root topic is never assignable, even when it has no children.
- Only a direct child whose parent is an active root of the same grade may be assigned to Question, BlueprintDetail, Lecture, mastery, recommendation, or TopicPractice.
- TagDifficulty remains flat.
- Existing invalid references are reported for manual remapping. They are never guessed or automatically reassigned.

### TopicPractice grade access

- Grade 10 Students may practice grade 10 direct-child topics.
- Grade 11 Students may practice grade 10 and 11 direct-child topics.
- Grade 12 Students may practice grade 10, 11, and 12 direct-child topics.
- Missing or invalid Student.CurrentGrade blocks option retrieval and generation.
- Lower-grade practice updates mastery for the practiced Tag. CompetencyPoint remains scoped to the Student's current grade.

### Vietnamese presentation

- Backend keeps stable English machine codes and internal enum/status values.
- Frontend owns Vietnamese localization.
- Technical IDs, internal scoring-rule names, `Tag`, `Preview`, `Draft`, and raw role names are not shown as primary user copy.
- Expert copy uses one glossary and sentence case. Internal values are localized through central label/error maps.

## API Contracts

### Fixed candidate search

`GET /api/test-generator/blueprints/{blueprintId}/fixed-test-candidates`

Query: `blueprintDetailId`, `search`, `pageIndex`, `pageSize`.

The server returns only currently eligible Questions for the requested BlueprintDetail and includes the latest supported QuestionVersion ID, preview content, topic, difficulty, type, part count, default weight, and scoring support.

### Fixed exam creation

`POST /api/test-generator/blueprints/{blueprintId}/fixed-tests`

```json
{
  "testName": "De tham khao THPT 2025 - Ma 0101",
  "durationMinutes": 90,
  "questions": [
    {
      "questionId": "question_01",
      "blueprintDetailId": "detail_01",
      "questionOrder": 1
    }
  ]
}
```

The command revalidates the complete selection. Frontend filtering is not trusted as an authorization or integrity boundary.

### Generated Test presentation

Existing generated-Test list and preview contracts add `generationType`, serialized as `Random` or `Fixed`. The existing status endpoint remains the archive contract.

### TopicPractice options

The existing route remains unchanged. Its response contains only active assignable direct-child topics from grades less than or equal to the Student's CurrentGrade. Each item includes its own `grade` and parent display information so the client can group options without making roots selectable.

## Stable Errors

- `TOPIC_PARENT_NOT_ASSIGNABLE`
- `TOPIC_MUST_BE_DIRECT_CHILD`
- `TOPIC_PARENT_GRADE_MISMATCH`
- `TOPIC_DEPTH_LIMIT_EXCEEDED`
- `TOPIC_PRACTICE_GRADE_NOT_ALLOWED`
- `STUDENT_GRADE_REQUIRED`
- `FIXED_TEST_BLUEPRINT_NOT_APPROVED`
- `FIXED_TEST_QUESTION_DUPLICATED`
- `FIXED_TEST_ORDER_INVALID`
- `FIXED_TEST_DETAIL_QUANTITY_MISMATCH`
- `FIXED_TEST_QUESTION_NOT_ELIGIBLE`
- `FIXED_TEST_QUESTION_VERSION_UNAVAILABLE`
- `TEST_ALREADY_ARCHIVED`

## Database Impact

No taxonomy table or column is added. Application validation enforces the two-level contract.

The fixed-exam audit value requires an approved SQL migration adding `FixedExam` to the `TestQuestion.SelectionReason` check constraint. The canonical create script must be updated in the Database repository in the same deployment unit. A read-only preflight script reports hierarchy depth and invalid parent references before deployment.

## Delivery Order

1. Direct-child taxonomy contract and data preflight.
2. Lower-grade direct-child TopicPractice and Recommender alignment.
3. Fixed BlueprintExam backend and SQL constraint update.
4. Expert and Student frontend changes plus Vietnamese copy normalization.

Each step is independently reviewable and must pass its affected module tests before the next step begins.
