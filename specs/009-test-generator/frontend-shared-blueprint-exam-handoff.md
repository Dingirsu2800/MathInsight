# Frontend Handoff: Shared BlueprintExam

## Scope

Implement only the UI integration for Expert shared BlueprintExam management and Student shared-Test discovery. TestGen creates or resolves a Test; Testing creates a TestSession in a separate request. Do not implement AdaptivePractice, TopicPractice, Diagnostic, WeakTag, or Recommender behavior.

## Existing Frontend Fix

Blueprint pages currently read `localStorage.getItem("AccountId")`, while the canonical auth key is `account_id`. Replace direct access in Blueprint list/detail/editor with `getAccountId()` from `src/services/authStorage.js`. Backend authorization remains authoritative.

## Expert Flow

An owner may generate from a Blueprint whose status is `Approved` or `Active`.

```text
Blueprint detail
-> open Generate dialog
-> POST shared Test
-> navigate to immutable Expert preview
-> optionally archive that Test variant
```

### Generate

```http
POST /api/test-generator/blueprints/{blueprintId}/tests
```

```json
{
  "testName": "Đề luyện tập số 1",
  "durationMinutes": 90
}
```

The backend generates TestID and TestCode. The frontend must not send MaxScore, scoring snapshots, QuestionVersionID, GeneratedBy, StudentID, ExpertID, or TestCode.

On success, navigate to `/expert/tests/{testId}/preview`. Disable all Generate dialog controls while submitting and use an in-flight ref in addition to React state. This prevents accidental double-clicks but is not durable backend idempotency; a deliberate retry after an uncertain network result may create another variant.

### Preview

```http
GET /api/test-generator/tests/{testId}/expert-preview
```

This owner-only contract contains immutable QuestionVersion content, solutions, correct answers, composite answer keys, explanations, and scoring snapshots. Label the page as Expert review mode. Never reuse this response or its components in a Student session screen.

Suggested route and files:

```text
src/App.jsx
src/pages/expert/GeneratedTestPreviewPage.jsx
src/components/expert/GeneratedTestSection.jsx
src/components/expert/GeneratedTestQuestionCard.jsx
```

Reuse the existing rich-text/LaTeX renderer. Render all five Question types and show section ScoreBudget, question MaxPointsSnapshot, WeightSnapshot, and ScoringRuleSnapshot.

### Archive

```http
PATCH /api/test-generator/tests/{testId}/status
```

```json
{
  "status": "Archived"
}
```

Only shared BlueprintExam variants owned through the Blueprint can be archived. Archive is idempotent. There is no reactivation in this checkpoint. After success, keep preview available, show an Archived badge, and disable the archive action. Existing Student sessions may finish, but new sessions cannot start.

## Student Flow

```text
Browse shared tests or enter TestCode
-> receive TestID
-> POST /api/v1/tests/sessions/start
-> receive SessionID
-> navigate to the Student test screen by SessionID
```

### Browse

```http
GET /api/test-generator/tests/shared-blueprint-exams?pageIndex=1&pageSize=20
```

The backend derives StudentID and grade from JWT/profile. Render Test name, grade, duration, total questions, max score, and creation time. Do not render answer keys or solutions.

### Resolve Code

```http
POST /api/test-generator/tests/resolve-code
```

```json
{
  "testCode": "math7k2p"
}
```

The backend trims and uppercases input. The UI may uppercase visually but must still send user input normally. Unknown, inactive, wrong-grade, deactivated-Blueprint, personal, and unsupported Tests all return the same `TEST_CODE_NOT_AVAILABLE` error. Do not reveal a more specific reason. The endpoint is limited to ten requests per Student per minute.

### Start Session

```http
POST /api/v1/tests/sessions/start
```

```json
{
  "testId": "test_xxx"
}
```

On `201`, navigate using `sessionId`. Testing revalidates personal ownership or shared Test grade/lifecycle, so handle `TESTING_TEST_ACCESS_DENIED` even after successful discovery. Handle `TESTING_SESSION_ALREADY_IN_PROGRESS` by directing the Student to resume the existing session flow if the product exposes it.

## API Service Changes

Extend `src/services/testGeneratorApi.js` using the existing authenticated Axios client:

```text
generateSharedBlueprintExam(blueprintId, payload)
getExpertTestPreview(testId)
archiveSharedBlueprintExam(testId)
getSharedBlueprintExams(params)
resolveTestCode(testCode)
```

Add or extend a centralized Testing API service for `startSession(testId)`. Do not create a second Axios instance.

## Blueprint Actions

Extend `src/utils/blueprintAuth.js`:

```text
canGenerate = isOwner && (status === "Approved" || status === "Active")
```

Generating once must not hide the action because one Blueprint may create many Test variants.

## Error Localization

Add `src/utils/testGenerationErrorLocalizer.js`. Map codes, never backend English messages:

| Code | Suggested Vietnamese message |
|---|---|
| `TEST_GENERATION_REQUEST_INVALID` | Thông tin đề chưa hợp lệ. |
| `BLUEPRINT_MUTATION_FORBIDDEN` | Bạn không có quyền sinh đề từ cấu trúc này. |
| `BLUEPRINT_STATUS_INVALID` | Trạng thái cấu trúc đề không cho phép sinh đề. |
| `BLUEPRINT_STRUCTURE_INVALID` | Cấu trúc đề chưa hợp lệ. |
| `BLUEPRINT_SCORE_BUDGET_MISMATCH` | Tổng điểm các phần không khớp tổng điểm đề. |
| `QUESTION_VERSION_MISSING` | Một số câu hỏi chưa có phiên bản hợp lệ. |
| `QUESTION_POOL_INSUFFICIENT` | Ngân hàng câu hỏi chưa đủ để sinh đề. |
| `TEST_GENERATION_CONFLICT` | Không thể hoàn tất sinh đề, vui lòng thử lại. |
| `TEST_CODE_NOT_AVAILABLE` | Mã đề không khả dụng. |
| `GENERATED_TEST_NOT_FOUND` | Không tìm thấy đề đã sinh. |
| `TESTING_TEST_ACCESS_DENIED` | Bạn không thể bắt đầu đề này. |
| `RATE_LIMIT_EXCEEDED` | Bạn thao tác quá nhanh, vui lòng thử lại sau. |

## Responsive and Accessibility

- Support 320, 375, 768, 1024, and 1440 pixel widths.
- Stack summary panels and actions on small screens.
- Keep question images within viewport width.
- Allow long math content and composite parts to wrap without horizontal page overflow.
- Preserve dialog focus trap and focus restoration.
- Prevent Escape/backdrop close while Generate is submitting.
- Use text labels in addition to color for correct answers and status.

## Acceptance Checks

- Owner sees Generate for Approved and Active; non-owner does not.
- Double-click sends one frontend request.
- Multiple deliberate generation intents create different variants.
- Preview survives refresh through the GET endpoint.
- Preview shows exact immutable answers and solution.
- Archive removes a Test from Student discovery and code resolution.
- Student browse and code flows both call StartSession with TestID.
- Start access denial never opens the test screen.
- Student UI never receives or renders Expert answer-key DTOs.
- `npm run build` passes with no new frontend environment variables.
