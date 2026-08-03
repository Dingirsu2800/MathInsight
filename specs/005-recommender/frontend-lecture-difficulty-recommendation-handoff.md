# Antigravity Handoff: Lecture Difficulty Recommendation Frontend

Use this prompt only after the backend implementation for lecture difficulty and recommendation has stabilized.

## Prompt

You are implementing frontend only for MathInsight, a React JavaScript + Vite + Tailwind application.

Read these files before editing:

1. `specs/005-recommender/lecture-difficulty-recommendation-design.md`
2. `docs/superpowers/plans/2026-08-03-lecture-difficulty-recommendation.md`
3. Existing components and service conventions in `frontend/src/pages/teacher`, `frontend/src/pages/student/dashboard`, and `frontend/src/services`.

## Hard Scope

You may modify only:

- `frontend/src/services/learningApi.js`
- `frontend/src/services/recommenderApi.js`
- `frontend/src/pages/teacher/LectureEditorPage.jsx`
- `frontend/src/pages/teacher/LectureListPage.jsx`
- `frontend/src/pages/teacher/LectureDetailPage.jsx`
- `frontend/src/pages/student/dashboard/RecommendedLecturesCard.jsx`
- Existing frontend test files if a frontend test setup exists.

Do not modify backend C#, SQL, specs, Docker, package versions, routes, or authentication. Do not add TypeScript. Do not add dark mode work. Preserve the existing desktop-focused MathInsight design system and existing responsive behavior.

## Backend Contracts

### Active difficulties

```http
GET /api/v1/difficulties
```

```json
[
  {
    "difficultyId": "DIFF-LEVEL-1",
    "difficultyName": "Nhận biết",
    "levelValue": 1,
    "displayOrder": 1
  }
]
```

### Create and update lecture

Send the existing payload plus:

```json
{
  "difficultyId": "DIFF-LEVEL-2"
}
```

### Lecture DTO

Expect:

```json
{
  "difficultyId": "DIFF-LEVEL-2",
  "difficultyName": "Thông hiểu",
  "difficultyLevel": 2
}
```

Legacy lectures may return all three fields as null.

### Recommended lectures

```http
GET /api/v1/recommender/lectures
```

Personalized exact example:

```json
{
  "lectureId": "lecture_01",
  "title": "Ứng dụng đạo hàm cơ bản",
  "thumbnailUrl": "https://example.invalid/thumb.jpg",
  "tagId": "TOPIC-G12-DERIVAPP",
  "tagName": "Ứng dụng đạo hàm",
  "difficultyId": "DIFF-LEVEL-2",
  "difficultyName": "Thông hiểu",
  "difficultyLevel": 2,
  "targetDifficultyLevel": 2,
  "officialPoint": 3.8,
  "evidenceCount": 5,
  "likes": 12,
  "isDifficultyFallback": false,
  "reason": "WeakTopicExactDifficulty"
}
```

Lower-level fallback differs by:

```json
{
  "difficultyLevel": 1,
  "targetDifficultyLevel": 2,
  "isDifficultyFallback": true,
  "reason": "WeakTopicLowerDifficultyFallback"
}
```

Cold start differs by:

```json
{
  "officialPoint": null,
  "evidenceCount": 0,
  "difficultyLevel": 1,
  "targetDifficultyLevel": 1,
  "isDifficultyFallback": false,
  "reason": "ColdStartGradeFoundation"
}
```

No recommendation is `200 OK` with `[]`.

Stable backend error codes include:

```text
LECTURE_DIFFICULTY_REQUIRED
LECTURE_DIFFICULTY_NOT_FOUND
LECTURE_DIFFICULTY_INACTIVE
LECTURE_TOPIC_INACTIVE
LECTURE_RECOMMENDATION_UNAVAILABLE
AUTH_INVALID_TOKEN
```

Map these codes to concise Vietnamese messages in frontend. Do not display backend developer messages directly.

## Required Teacher UX

1. Load active difficulties when the Lecture editor opens.
2. Add a required difficulty select near the existing topic classification control.
3. Use labels from the API; do not hardcode the four difficulty records.
4. Include `difficultyId` in create and update requests.
5. In edit mode, preserve the loaded difficulty.
6. If a legacy lecture has no difficulty, show a restrained inline warning and keep publish unavailable until a value is selected and saved.
7. Show difficulty as a compact badge/column in Lecture list and detail.
8. Use stable dimensions so adding the badge does not shift table actions.
9. Keep current OCR, material attachment, next-lecture, ownership, and publication workflows intact.

## Required Student UX

1. Replace the placeholder thumbnail with `thumbnailUrl` when present; retain the existing neutral fallback when absent.
2. Make each recommendation a keyboard-accessible link to `/student/lectures/{lectureId}`. Do not use a clickable `div`.
3. Show topic and actual lecture difficulty.
4. For `WeakTopicExactDifficulty`, show: `Đề xuất vì bạn đang cần củng cố chủ đề này.`
5. For `ProgressionExactDifficulty`, show: `Mức học tiếp theo phù hợp với tiến độ hiện tại của bạn.`
6. For a lower-level fallback, show: `Bài giảng nền tảng để ôn lại trước mức {targetDifficultyLevel}.`
7. For cold start, show: `Bài giảng khởi đầu phù hợp với khối lớp của bạn.`
8. Show `Điểm chủ đề: x/10` only when `officialPoint` is not null.
9. Do not label a cold-start lecture as personalized by mastery.
10. Keep loading, error, and empty states. Add an explicit retry button for technical loading failure.
11. Limit visual output to the six records returned by backend; do not re-rank in frontend.

## Accessibility and Interaction

- All inputs have labels.
- Recommendation links have visible focus states.
- Badges are not the only way meaning is communicated.
- Long titles and topic names do not overlap actions.
- No nested cards and no new decorative gradients or dark mode treatment.

## Verification

Run:

```powershell
Set-Location frontend
npm run build
```

Manually verify:

1. Teacher creates lecture with difficulty.
2. Edit preserves difficulty.
3. Legacy null difficulty warning appears.
4. Exact recommendation renders and navigates.
5. Lower fallback explanation is correct.
6. Cold-start explanation does not show a mastery point.
7. Empty and API-error states remain usable.

Return a walkthrough listing every modified file, the API assumptions used, build output, and any behavior that could not be verified. Do not claim that backend or SQL was implemented.
