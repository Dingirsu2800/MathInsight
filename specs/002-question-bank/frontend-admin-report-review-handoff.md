# Admin Question Report Review Frontend Handoff

## Expert Report Inbox

Existing Expert screens may continue to call:

```http
GET /api/question-bank/reports/mine?status=Pending&pageIndex=1&pageSize=10
GET /api/question-bank/questions/{questionId}/reports?status=Pending
```

For these two Expert endpoints, `status=Pending` now means **action required** and includes report records in `Pending`, `PendingFix`, and `PendingReview`.

An Admin report with `PendingFix` needs an Expert action. Show a submit-review action only when the current Expert owns the question:

```http
POST /api/question-bank/reports/{reportId}/submit-review
```

On success, refresh the report list/detail. The report becomes `PendingReview`; no request body is required.

## Admin Review Inbox

Admin sees only workflows created by that Admin account:

```http
GET /api/question-bank/admin/reports/mine?status=PendingReview&pageIndex=1&pageSize=10
```

Supported status values are `PendingFix`, `PendingReview`, and `Resolved`. The response contains the question content/status, Expert owner, report reason, review note, and submit/review audit fields.

Approve a submitted workflow:

```http
POST /api/question-bank/admin/reports/{reportId}/approve
```

Reject requires a non-empty note with at most 2000 characters:

```http
POST /api/question-bank/admin/reports/{reportId}/reject
Content-Type: application/json

{
  "reviewNote": "Formula in statement c needs correction."
}
```

## Error Handling

Use backend `code` values, not backend `message`, for localized UI:

| HTTP | Code | Suggested UI behavior |
|---|---|---|
| 409 | `ADMIN_REPORT_WORKFLOW_ALREADY_EXISTS` | Refresh report state; an Admin workflow already exists. |
| 409 | `ADMIN_REPORT_REQUIRES_REVIEW` | Do not show regular resolve/dismiss for this report. |
| 409 | `REPORT_ALREADY_HANDLED` | Refresh; workflow state changed. |
| 403 | `REPORT_ACCESS_FORBIDDEN` | Hide/disable action; current account is not allowed. |
| 400 | `REVIEW_NOTE_REQUIRED` | Show required validation for review note. |
| 400 | `REVIEW_NOTE_TOO_LONG` | Show 2000-character validation. |
| 409 | `QUESTION_HAS_PENDING_REPORTS` | Keep question active; do not allow deactivate/delete while reports are active. |

The frontend must not call approve/reject for a report it did not create and must not offer deactivate/delete while the API reports an active workflow.
