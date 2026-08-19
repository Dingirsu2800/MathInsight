# Frontend Handoff: Mastery Evidence V2 and Student Exam UX

**Backend status:** Part A is implemented on `feature/mastery-evidence-v2`.
This handoff covers the remaining Student frontend work only. It does not
change the backend routes for shared Fixed/Random discovery or TestCode
resolution.

## Student Exam Layout

Keep the shared catalog visibly separate from the personal create action:

```text
[ Tạo đề theo năng lực ] [ Nhập mã đề ]     Kho đề: [ Đề cố định ] [ Đề theo cấu trúc ]
```

`Tạo đề theo năng lực` is a command above the catalog, not a catalog tab. The
shared catalog continues to show only reusable Fixed/Random Tests. Do not show
raw BlueprintID, TestID, WeakTag, Ptag, adaptive, baseline, recommender, or
ma trận terms in visible Student copy.

## Blueprint Options API

```http
GET /api/test-generator/tests/blueprint-options?search=&pageIndex=1&pageSize=20
```

Response:

```json
{
  "items": [
    {
      "blueprintId": "...",
      "blueprintName": "...",
      "grade": 12,
      "totalQuestions": 40,
      "totalScore": 10,
      "durationMinutes": 90,
      "status": "Approved",
      "sectionCount": 3
    }
  ],
  "totalCount": 1,
  "pageIndex": 1,
  "pageSize": 20
}
```

`pageSize` must remain in `1..50`. The dialog should request 20 rows per page,
debounce search by about 300 ms, reset to page 1 when search changes, and show
server `totalCount` with `Trước`/`Tiếp` controls. Abort stale requests without
showing an error.

## Adaptive Create Flow

1. Lazily load blueprint options when the dialog opens.
2. Show name, grade, section count, question count, duration, and score.
3. Explain in natural Vietnamese that the approved structure is preserved and
   the difficulty may be adjusted based on recent results.
4. Call `POST /api/test-generator/tests/blueprint-exams` exactly once after
   final confirmation.
5. Keep the returned `testId` and call the existing Testing `startSession`.
6. If starting fails, keep the same `testId` across close/reopen and retry
   `startSession`; never generate a replacement Test.
7. Clear the retained ID only after session start succeeds.

Handle loading, empty, retry, selected, generating, starting, and start-retry
states. Prevent double submit with both UI state and an in-flight ref; closing
is disabled while generation or start is in progress.

## Backend Errors

Localize these stable codes in the existing frontend error mapper:

- `ADAPTIVE_EXAM_MASTERY_UNAVAILABLE` -> recommendation data is temporarily unavailable.
- `ADAPTIVE_EXAM_MASTERY_INVALID` -> recommendation data cannot be safely used.

The frontend should not calculate mastery, difficulty, fallback, evidence, or
audit fields. It only renders the blueprint selection and starts the returned
personal Test.

## Verification

- Unit tests cover lazy loading, search debounce, stale response cancellation,
  pagination, empty results, one generate call, and start retry without
  regeneration.
- Build with `npm test -- --run` and `npm run build`.
- Integrated browser smoke must run against a rebuilt backend and frontend,
  and verify the personal command remains separate from Fixed/Random catalog
  tabs.
