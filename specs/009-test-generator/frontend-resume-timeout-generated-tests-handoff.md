# Frontend Handoff: Resume, Timeout, and Generated Tests

Implement frontend only. Do not change backend, SQL, seed files, or API routes.

## Backend contracts now available

### 1. Duplicate start can resume

`POST /api/v1/tests/sessions/start`

When an InProgress session already exists, HTTP 409 returns:

```json
{
  "code": "TESTING_SESSION_ALREADY_IN_PROGRESS",
  "message": "...",
  "existingSessionId": "session_01"
}
```

Update `StartTestDialog.jsx` to use `existingSessionId` and offer "Tiếp tục bài đang làm". Do not look for `sessionId` in an ordinary error response.

### 2. Session content includes saved answers

`GET /api/v1/tests/sessions/{sessionId}` returns the existing fields plus:

```json
{
  "remainingSeconds": 3521,
  "savedAnswers": [
    {
      "questionId": "question_01",
      "answerId": "answer_02",
      "shortAnswerText": null,
      "timeSpent": 35,
      "selectedOptions": [
        { "answerId": "answer_03" }
      ],
      "parts": [
        {
          "partId": "part_01",
          "booleanAnswer": true,
          "textAnswer": null,
          "numericAnswer": null
        }
      ]
    }
  ]
}
```

In `TestSession.jsx`:

- Hydrate `userAnswers` from `savedAnswers` immediately after loading session content.
- Support SingleChoice/TrueFalse, MultipleChoice, ShortAnswer, and Composite mappings.
- Do not schedule auto-save while hydrating initial state.
- Use response `remainingSeconds` directly. Remove the empty auto-save heartbeat.
- A reload must render exactly the persisted answers before accepting edits.

### 3. Timeout is server-authoritative

Add API function:

```http
POST /api/v1/tests/sessions/{sessionId}/timeout-submit
```

Rules:

- Timer reaching zero calls `timeout-submit`, never normal `submit`.
- HTTP 409 `TESTING_SESSION_NOT_EXPIRED`: refetch session content and resync timer.
- HTTP 409 `TESTING_SESSION_EXPIRED` from auto-save: stop editing and call `timeout-submit`.
- Backend also converts a late normal-submit request to `TimeoutSubmit`; frontend still uses the explicit timeout route for clear intent.
- Prevent duplicate timeout/normal submit with the existing in-flight ref.
- Do not send `submissionType` from the browser.

### 4. Expert generated-Test list

Add API function:

```http
GET /api/test-generator/blueprints/{blueprintId}/tests?pageIndex=1&pageSize=20
```

Response:

```json
{
  "pageIndex": 1,
  "pageSize": 20,
  "totalCount": 2,
  "items": [
    {
      "testId": "test_01",
      "blueprintId": "blueprint_01",
      "testName": "Đề luyện tập số 1",
      "testCode": "MATH7K2P",
      "testStatus": "Active",
      "durationMinutes": 90,
      "totalQuestions": 22,
      "maxScore": 10.00,
      "createdTime": "2026-07-26T10:00:00Z"
    }
  ]
}
```

In `BlueprintDetailPage.jsx` add an unframed section named "Đề đã sinh":

- Load owner list with pagination after Blueprint detail loads.
- Show Active/Archived status, TestCode, duration, question count, max score, and creation time.
- Preview navigates to `/expert/tests/{testId}/preview`.
- Active item can call the existing archive confirmation flow/API.
- Archived item remains previewable but has no archive action.
- Refresh this list after generation or archive.
- Include loading, empty, error/retry, and pagination states.

## Error localization

Add Vietnamese mappings:

```text
TESTING_SESSION_EXPIRED
TESTING_SESSION_NOT_EXPIRED
TESTING_SESSION_ALREADY_IN_PROGRESS
```

Never display backend English messages directly.

## Constraints

- Preserve React JavaScript + Tailwind + existing shadcn-style components.
- Reuse `testGeneratorApi`, `Button`, `Dialog`, existing layouts, badges, and error localizer patterns.
- Do not redesign unrelated pages.
- Do not expose Expert answer keys in Student components.
- Keep controls desktop-first but responsive and keyboard accessible.

## Verification

1. Start a Test twice and resume using `existingSessionId`.
2. Answer every supported type, reload, and verify exact selections remain visible.
3. Verify hydration does not immediately overwrite saved answers.
4. Verify timer uses backend `remainingSeconds`.
5. Verify zero calls `timeout-submit` and handles early 409 by resyncing.
6. Verify Expert can revisit and archive a generated Test after page reload.
7. Run `npm run build` and report changed files and result.
