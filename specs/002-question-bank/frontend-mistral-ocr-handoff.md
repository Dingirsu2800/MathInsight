# Frontend Handoff: Mistral OCR Question Draft

## Scope

Implement the Expert frontend for the existing backend endpoint:

```http
POST /api/question-bank/questions/ocr-draft
Content-Type: multipart/form-data
field: file
```

The feature belongs inside the existing Question Editor, not as a new page or a separate question-creation flow. It replaces the current mock OCR scanner panel in `frontend/src/pages/expert/QuestionEditorPage.jsx`.

Do not modify backend code, database schema, Docker configuration, authentication, or environment variables. In particular, do not add an API key, Mistral SDK, or any `VITE_MISTRAL_*` variable to the frontend.

## API Contract

### Request

- Authenticated Expert only; the existing Axios client supplies the bearer token.
- Exactly one file in field `file`.
- Client-side guard: JPEG, PNG, WebP only; maximum 5 MB.
- The backend repeats and enforces the validation, so frontend validation is only for immediate feedback.

### Successful response: `200`

```json
{
  "rawMarkdown": "Câu 1. Tính $x$...",
  "pageConfidence": 0.91,
  "warnings": [
    "OCR answer suggestions are not confirmed answer keys; verify them before saving."
  ],
  "extractedImages": [
    {
      "id": "page-0-diagram-1",
      "dataUrl": "data:image/png;base64,...",
      "annotation": "A geometry diagram."
    }
  ],
  "draft": {
    "questionContent": "Câu 1. Tính $x$...",
    "solutionContent": "",
    "suggestedQuestionType": "SINGLE_CHOICE",
    "answers": [
      { "content": "$x=1$", "suggestedIsCorrect": null }
    ],
    "parts": [
      {
        "label": "a",
        "content": "Mệnh đề ...",
        "partType": "TRUE_FALSE",
        "explanation": null,
        "suggestedCorrectBoolean": null,
        "suggestedCorrectText": null,
        "suggestedCorrectNumeric": null,
        "numericTolerance": null
      }
    ]
  }
}
```

`suggestedQuestionType` can be `SINGLE_CHOICE`, `MULTIPLE_CHOICE`, `TRUE_FALSE`, `SHORT_ANSWER`, `COMPOSITE`, or `UNKNOWN`. `partType` can be `TRUE_FALSE`, `SHORT_ANSWER`, `NUMERIC_ANSWER`, or `UNKNOWN`.

### Error Contract

Read `error.response?.data?.code`; show the corresponding Vietnamese message and keep the selected file available for retry unless the user changes/removes it.

| HTTP | Code | UI behavior |
|---|---|---|
| 400 | `IMAGE_REQUIRED`, `IMAGE_TYPE_NOT_SUPPORTED` | Inline file error |
| 413 | `IMAGE_TOO_LARGE` | Inline file error |
| 422 | `OCR_DRAFT_UNAVAILABLE` | Tell the Expert to crop/retake one complete question image |
| 429 | `OCR_RATE_LIMIT_EXCEEDED`, `OCR_PROVIDER_RATE_LIMITED` | Disable scan button for the current request; tell the user to wait and retry manually |
| 502 | `OCR_PROVIDER_UNAVAILABLE`, `OCR_INVALID_RESPONSE` | Provider failure; do not fabricate a draft |
| 503 | `OCR_NOT_CONFIGURED` | System unavailable; do not expose configuration details |
| 504 | `OCR_TIMEOUT` | Timeout; allow a manual retry |

## Required UX

1. Keep the existing “OCR Scanner Panel” location in the Question Editor, but rename it to “Tạo bản nháp từ ảnh đề”. It must say this accepts **one complete question**, not a formula-only image and not a full page containing multiple questions.
2. Use an image-only file picker with `accept="image/jpeg,image/png,image/webp"`. Show local preview, file name, type, and readable size.
3. Before request, show a compact checklist: one question only, crop out unrelated questions/notes, and verify every OCR result before saving.
4. The primary action is an icon-plus-text button “Quét tạo bản nháp”. Use the current project icon system and existing `Button` component. Disable it while no file is chosen or while a request is in flight.
5. During request, show progress with a spinner and text “Đang đọc ảnh đề…”. Do not simulate progress percentages.
6. On success, open a **review drawer or modal** above the editor. Do not silently overwrite the form. The drawer/modal must show:
   - the source image preview;
   - recognized type and optional confidence, labelled as a suggestion;
   - all backend warnings in a visible warning region;
   - editable question content and solution content previews/textareas;
   - editable extracted options or parts, according to suggested type;
   - a collapsible “Markdown OCR gốc” diagnostic section;
   - buttons: `Hủy`, `Áp dụng bản nháp`, and `Áp dụng nội dung câu hỏi`.
7. `Hủy` closes the review UI and leaves the existing editor form unchanged. `Áp dụng nội dung câu hỏi` updates only `questionContent` and optionally `solutionContent` when non-empty. `Áp dụng bản nháp` updates the editor fields described below.
8. Source-image attachment is optional and **off by default**. Display any `extractedImages` as selectable candidates. The Expert may choose at most one detected image, manually drag a rectangular crop from the source image, or check “Đính kèm toàn bộ ảnh nguồn vào câu hỏi”, never more than one. The manual crop is generated in the browser with Canvas and is the fallback when OCR returns no image candidate. Only the selected source/candidate/crop is uploaded through `uploadQuestionImage(...)` during apply; its returned `pictureUrl` is used for `form.pictureUrl`. If nothing is selected, OCR does not upload an image.
9. If source-image upload fails after OCR review, do not lose the reviewed draft. Show the image-upload error in the review UI and allow retry or apply without attachment.
10. Preserve accessibility: focus moves into the review UI, Escape closes it only when not processing, focus returns to the scan button, inputs have labels, errors use `role="alert"`, and all controls are keyboard reachable.

## Form Mapping Rules

Implement a small pure mapper, preferably in `frontend/src/pages/expert/questionMappers.js` or a new adjacent `ocrDraftMappers.js`. Unit-test it if the repository has frontend test infrastructure.

- Never set metadata automatically: do **not** set grade, difficulty, topics, default weight, status, expert, or report state from OCR.
- Map `MULTIPLE_CHOICE` to the Question Editor's existing multi-select enum expected by `mapEditorStateToCreateUpdateRequest`. Verify the currently accepted editor enum before coding; do not introduce a second incompatible spelling.
- For `SINGLE_CHOICE`/multi-select: map extracted answer content to `form.options`, but set every `isCorrect` to `false`. Display any `suggestedIsCorrect` only as an untrusted visual hint in the review UI; it must not become an answer key after apply.
- For `TRUE_FALSE`: retain the editor's canonical two “Đúng/Sai” options. Do not infer the correct value from OCR. If OCR returns statement-like content, treat that as a warning or require the Expert to classify it as `COMPOSITE`.
- For `SHORT_ANSWER`: put no OCR answer into `shortAnswer`; leave answer key empty for the Expert. The stem and solution may be applied.
- For `COMPOSITE`: map each valid OCR part to current state shape: `partOrder`, `partLabel`, `partContent`, `partType`, `explanation`, `defaultWeight`. Set `correctBoolean`, `correctText`, `correctNumeric`, and `numericTolerance` to `null` regardless of OCR suggestions. Ignore `UNKNOWN` parts on full apply and state the number ignored.
- For `UNKNOWN`: do not change `form.questionType` on apply. Allow “apply content only”; let the Expert select a type manually.
- Do not overwrite grade/difficulty/topics, and do not submit/create/update a question from the OCR action.
- Any form changes are ordinary unsaved editor changes. Existing Save validation remains authoritative.

## Files To Change

1. `frontend/src/services/questionBankApi.js`
   - Add `extractQuestionOcrDraft(file)` using `FormData` and `POST /api/question-bank/questions/ocr-draft`.
   - Follow the exact multipart pattern used by `uploadQuestionImage`.
2. `frontend/src/pages/expert/QuestionEditorPage.jsx`
   - Replace current mock OCR state/handlers/UI.
   - Keep local object URL cleanup.
   - Add request, review, apply, cancellation, retry, and source-image attachment states.
3. `frontend/src/pages/expert/questionMappers.js` or a small new mapper module
   - Add pure normalization/mapping for OCR draft to editor state patch.
4. Optional small presentational component under `frontend/src/components/expert/`
   - Only if it makes `QuestionEditorPage.jsx` materially smaller: `QuestionOcrDraftReviewDialog.jsx`.
   - Reuse existing shadcn-style Dialog/Sheet primitives if already installed; do not add a second modal library.

## Explicit Non-Goals

- No client-side OCR, Mistral SDK, API key, or direct call from browser to Mistral.
- No support for PDF, DOCX, multiple images, a whole exam page, batch OCR, auto-retry, or background queue in this checkpoint.
- No automatic save, publish, topic tagging, difficulty assignment, grade assignment, or answer-key confirmation.
- Do not modify Question Bank list, tag management, report flow, routes, backend API, schema, Docker, or environment configuration.

## Acceptance Checklist

- [ ] A valid JPEG/PNG/WebP under 5 MB can be previewed and sent to backend.
- [ ] Unsupported or oversized files are rejected before network call.
- [ ] Scan calls only `POST /api/question-bank/questions/ocr-draft` and sends no Mistral credentials.
- [ ] Successful OCR never changes the editor until the Expert explicitly applies a reviewed draft.
- [ ] Applying a draft never sets an answer key from OCR suggestion.
- [ ] Attach-source-image checkbox is unchecked by default and only invokes Cloudinary upload when selected.
- [ ] All documented backend errors have Vietnamese user-facing messages and retry behavior where appropriate.
- [ ] Existing create/edit/save behavior remains unchanged when OCR is not used.
- [ ] `npm run build` passes.

## Prompt For Antigravity

```text
Implement only the frontend OCR draft integration described in:
Implementation/MathInsight/specs/002-question-bank/frontend-mistral-ocr-handoff.md

Read and follow the existing frontend patterns, especially:
- frontend/src/pages/expert/QuestionEditorPage.jsx
- frontend/src/pages/expert/questionMappers.js
- frontend/src/services/questionBankApi.js

The backend already exists at POST /api/question-bank/questions/ocr-draft. Do not modify backend, database, Docker, routes, authentication, or environment variables. Never add an OCR API key, Mistral SDK, or VITE_MISTRAL_* variable.

Replace the current mock OCR panel inside QuestionEditorPage with the reviewed-draft workflow in the handoff document. Preserve the existing visual language, Tailwind/shadcn components, Vietnamese UI, responsive desktop-first editor layout, keyboard accessibility, and current non-OCR behavior. Do not add mock data or fake OCR success states. Run npm run build when finished and report changed files plus any assumptions.
```
