# Expert Excel Question Import Handoff

## Scope

Use a dialog launched by the secondary **Nhập Excel** button on the Question Bank list. Do not add a sidebar item, route, client-side Excel parser, queue polling, Word import, or mock API.

## Endpoints

| Method | Path | Body | Result |
|---|---|---|---|
| GET | `/api/question-bank/questions/import-template` | None | `.xlsx` attachment |
| POST | `/api/question-bank/questions/import-preview` | `multipart/form-data`, field `file` | Preview response |
| POST | `/api/question-bank/questions/import-confirm` | JSON request below | `201` result or `400` validation response |

All endpoints require the `Expert` role.

## Excel Topic Contract

The `Topics` sheet uses `QuestionKey | TopicCode | IsPrimary`.

- `TopicCode` is the stable `TagID` shown in the `Catalogs` sheet, for example `TOPIC-G12-DERIVAPP`.
- `TopicName` is displayed in `Catalogs` only to help the Expert choose the code; it is not imported.
- The code must refer to an active topic whose grade matches the question grade.
- Template version 3 is required. Version 2 files that used `TopicName` are rejected and must be recreated from the latest template.

## Preview

```json
{
  "importId": "guid",
  "fileName": "questions.xlsx",
  "totalCount": 2,
  "validCount": 1,
  "invalidCount": 1,
  "fileErrors": [],
  "items": [
    {
      "questionKey": "Q001",
      "sourceRow": 2,
      "isValid": true,
      "errors": [],
      "draft": {
        "questionContent": "...",
        "solutionContent": "...",
        "pictureUrl": null,
        "difficultyId": "guid",
        "grade": 12,
        "questionType": "SINGLE_CHOICE",
        "defaultWeight": 1.0,
        "topics": [{ "tagId": "guid", "isPrimary": true }],
        "answers": [{ "answerContent": "A", "isCorrect": true }],
        "parts": []
      }
    }
  ]
}
```

Only `isValid: true` items may be selected. Sending a different file resets the preview. The client must not alter a normalized draft before Confirm.

## Confirm

```json
{
  "importId": "guid",
  "items": [
    { "questionKey": "Q001", "draft": { "...": "normalized preview draft" } }
  ]
}
```

Success (`201`):

```json
{
  "code": "",
  "importId": "guid",
  "createdCount": 1,
  "questions": [{ "questionKey": "Q001", "questionId": "guid" }],
  "errors": []
}
```

Validation failure (`400`) returns the same shape with `code: "QUESTION_IMPORT_VALIDATION_FAILED"`, `createdCount: 0`, and row errors. It writes no question.

## Error Codes

- `QUESTION_IMPORT_FILE_REQUIRED`
- `QUESTION_IMPORT_FILE_TOO_LARGE` (`413`)
- `QUESTION_IMPORT_FILE_TYPE_NOT_SUPPORTED` (`415`)
- `QUESTION_IMPORT_TEMPLATE_INVALID`
- `QUESTION_IMPORT_TEMPLATE_VERSION_UNSUPPORTED`
- `QUESTION_IMPORT_LIMIT_EXCEEDED`
- `QUESTION_IMPORT_VALIDATION_FAILED`

Map these codes to Vietnamese UI text. Do not render backend English messages directly. Disable Confirm while its request is pending. If the network fails after sending Confirm, tell the Expert to refresh the question list before retrying because the stateless MVP cannot guarantee idempotency after a lost response.
