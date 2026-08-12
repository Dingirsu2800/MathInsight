# Topic Practice Manual Difficulty Handoff

Use `GET /api/test-generator/tests/topic-practice-options` as the source of truth. Each topic now contains `difficultyAvailability`, ordered by level, with `availableQuestionCount` and `canGenerate`.

Send the existing request unchanged for the recommended path:

```json
{ "tagId": "TOPIC-G12-COMPLEX" }
```

Send a selected difficulty for manual practice:

```json
{ "tagId": "TOPIC-G12-COMPLEX", "difficultyId": "DIFF-3" }
```

Disable manual levels where `canGenerate` is false. Do not invent a fallback in the client. Map `TOPIC_PRACTICE_DIFFICULTY_NOT_FOUND` and `TOPIC_PRACTICE_DIFFICULTY_UNAVAILABLE` to Vietnamese, then refresh options. The response exposes `difficultySelectionMode` (`Recommended` or `Manual`) and selected difficulty metadata for the confirmation/result view.
