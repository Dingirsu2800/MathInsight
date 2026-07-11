# Question Image Upload Frontend Handoff

## Backend Contract

```http
POST /api/question-bank/questions/image-upload
Authorization: Bearer <expert-token>
Content-Type: multipart/form-data

file=<JPEG|PNG|WebP, maximum 5 MB>
```

Success response:

```json
{
  "pictureUrl": "https://res.cloudinary.com/.../image.webp"
}
```

Stable error responses use the existing API error shape:

```json
{
  "code": "IMAGE_TYPE_NOT_SUPPORTED",
  "message": "Only JPEG, PNG, and WebP images are supported."
}
```

| HTTP | Code | Frontend behavior |
|---|---|---|
| 400 | `IMAGE_REQUIRED` | Keep the editor open and show file-required state. |
| 400 | `IMAGE_TYPE_NOT_SUPPORTED` | Show a localizable unsupported-format message. |
| 413 | `IMAGE_TOO_LARGE` | Show a localizable 5 MB limit message. |
| 503 | `IMAGE_STORAGE_UNAVAILABLE` | Show retry-later state. |
| 502 | `IMAGE_UPLOAD_FAILED` | Show retry state without exposing provider details. |

## Antigravity Prompt

Update the existing React JavaScript Question Editor image upload flow to use the MathInsight backend API instead of direct Cloudinary upload.

Requirements:

1. Replace `cloudinaryUploadApi.js` usage with `POST /api/question-bank/questions/image-upload` using `FormData` field name `file` and the existing JWT from `access_token`/`token` localStorage.
2. Add `uploadQuestionImage(file)` to `questionBankApi.js`. Do not manually set `Content-Type` for the multipart request; Axios must add its boundary.
3. Read `response.data.pictureUrl`, then preserve the current preview, loading, retry, remove, and 5 MB client-side validation behavior.
4. Continue sending the returned `pictureUrl` in existing create/update Question payloads. Do not alter the question form contract.
5. Remove the direct Cloudinary frontend client and remove use of `VITE_CLOUDINARY_CLOUD_NAME` and `VITE_CLOUDINARY_UPLOAD_PRESET`. The backend alone owns `Cloudinary__CloudName`, `Cloudinary__ApiKey`, and `Cloudinary__ApiSecret` through user-secrets, Docker, or Azure configuration.
6. Do not implement OCR, Pix2Text, LaTeX extraction, multiple images, or image-to-question import in this change.
7. Surface backend stable error codes through the existing Vietnamese UI error treatment. Never show raw HTTP/provider response bodies.
