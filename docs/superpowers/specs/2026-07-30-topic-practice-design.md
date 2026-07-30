# TopicPractice Backend and Frontend Design

## 1. Mục tiêu

Hoàn thiện flow luyện tập theo chủ đề cho Student:

1. Student xem các topic active thuộc khối hiện tại.
2. Student chọn một topic bất kỳ, không bắt buộc là WeakTag.
3. TestGen sinh một personal Test gồm đúng 10 câu.
4. Testing tạo Practice session không giới hạn thời gian.
5. Student làm bài, autosave, tự nộp và nhận kết quả chấm đồng bộ.

TopicPractice trong checkpoint này độc lập với Recommender. Sau này Recommender chỉ cung cấp topic và difficulty profile được khuyến nghị cho cùng generation core; không thay thế generation core.

## 2. Phạm vi

### Trong phạm vi

- Student topic-practice options API.
- TopicPractice generation API.
- Topic tree descendant expansion.
- Difficulty profile mặc định và fallback.
- Giới hạn tối đa hai Composite.
- Ưu tiên câu chưa gặp và tái sử dụng câu cũ nhất khi cần.
- Immutable QuestionVersion và scoring snapshot.
- Unlimited Practice bằng `DurationMinutes = 0`.
- Testing contract cho countdown hoặc count-up timer.
- Student frontend chọn topic, tạo bài và làm bài.
- SQL migration và cập nhật canonical fresh schema cho duration bằng 0.

### Ngoài phạm vi

- WeakTag bắt buộc hoặc Recommender integration.
- AdaptivePractice và Diagnostic.
- Tự động thay đổi difficulty profile theo năng lực.
- Student chọn số câu, thời lượng hoặc question type.
- HTTP idempotency bền vững.
- Tạo Blueprint tạm cho TopicPractice.
- Redis, background generation hoặc queue mới.

## 3. Quyết định nghiệp vụ

| Nội dung | Quyết định |
|---|---|
| Topic hợp lệ | Mọi topic active thuộc `Student.CurrentGrade` |
| Topic cha | Bao gồm chính topic và toàn bộ descendants active |
| Số câu | Cố định 10 |
| Difficulty mặc định | 3 Easy, 4 Medium, 2 Hard, 1 Very Hard |
| Thiếu quota difficulty | Bù từ level gần nhất; hòa thì ưu tiên level thấp hơn |
| Question type | SingleChoice, Composite, ShortAnswer |
| Composite | Tối đa 2, không bắt buộc phải có |
| Câu đã gặp | Ưu tiên chưa gặp; sau đó lấy lần xuất hiện cũ nhất |
| Pool cuối dưới 10 | Trả 409 và không ghi dữ liệu |
| Thời gian | Không giới hạn, `DurationMinutes = 0` |
| Điểm tối đa | 10.00 |
| Scoring policy | `NormalizedWeight` |
| Recommender | Không phụ thuộc trong checkpoint này |

## 4. Kiến trúc

### TestGen

TestGen sở hữu:

- Topic-practice option query.
- Candidate catalog và immutable-version validation.
- Difficulty profile.
- TopicPractice selector.
- Test/TestQuestion persistence.

Generation core nhận một policy nội bộ thay vì gọi Recommender. Policy mặc định được dùng trong checkpoint này; policy dựa trên WeakTag có thể được thêm sau mà không đổi API tạo bài cơ bản.

### Testing

Testing tiếp tục sở hữu:

- Quyền mở personal Test.
- Tạo/resume TestSession.
- Autosave và behavior tracking.
- Timeout đối với timed Test.
- Submit và điều phối Grading.

`TopicPractice` được map sang `TestFormat = Practice`. TestGen không tạo TestSession.

### Frontend

Frontend mở rộng Student Test area hiện tại bằng route/topic tab mới. Màn làm bài hiện tại được tái sử dụng và timer hỗ trợ count-up khi Test không có giới hạn thời gian.

## 5. API contract

### 5.1 Topic-practice options

```http
GET /api/test-generator/tests/topic-practice-options
Authorization: Bearer <Student token>
```

Response:

```json
{
  "grade": 12,
  "requiredQuestionCount": 10,
  "topics": [
    {
      "tagId": "TOPIC-G12-CALCULUS",
      "parentTagId": null,
      "tagName": "Giải tích",
      "displayOrder": 10,
      "availableQuestionCount": 28,
      "canGenerate": true
    }
  ]
}
```

Danh sách là flat list có `parentTagId`; frontend dựng cây. `availableQuestionCount` tính trên candidate hợp lệ của chính topic và descendants active. Topic inactive hoặc khác grade không được trả về.

### 5.2 Generate TopicPractice

```http
POST /api/test-generator/tests/topic-practices
Authorization: Bearer <Student token>
Content-Type: application/json

{
  "tagId": "TOPIC-G12-CALCULUS"
}
```

Response `201 Created`:

```json
{
  "testId": "generated-id",
  "selectedTagId": "TOPIC-G12-CALCULUS",
  "selectedTagName": "Giải tích",
  "testMode": "TopicPractice",
  "testName": "Luyện tập: Giải tích",
  "durationMinutes": 0,
  "totalQuestions": 10,
  "maxScore": 10.00,
  "scoringPolicy": "NormalizedWeight",
  "createdTime": "2026-07-30T12:00:00Z"
}
```

Student ID, Test ID, số câu, duration, difficulty quota và recommendation data không được nhận từ client.

### 5.3 Start session

Frontend dùng API Testing hiện có sau khi generation thành công:

```http
POST /api/v1/tests/sessions/start

{
  "testId": "generated-id"
}
```

TestGen và Testing giữ transaction boundary riêng.

## 6. Stable errors

| Code | HTTP | Điều kiện |
|---|---:|---|
| `AUTH_INVALID_TOKEN` | 401 | Thiếu Student claim |
| `TOPIC_PRACTICE_STUDENT_NOT_FOUND` | 404 | Student profile không sử dụng được |
| `TOPIC_PRACTICE_TOPIC_NOT_FOUND` | 404 | Topic không tồn tại |
| `TOPIC_PRACTICE_TOPIC_UNAVAILABLE` | 422 | Topic inactive hoặc sai grade |
| `TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS` | 409 | Candidate pool cuối dưới 10 |
| `TOPIC_PRACTICE_GENERATION_CONFLICT` | 409 | Retry/ambiguous commit không xác minh được |
| `TESTING_TEST_HAS_NO_TIME_LIMIT` | 409 | Client gọi timeout-submit cho unlimited Test |

Frontend map code sang tiếng Việt và không hiển thị raw backend message.

## 7. Candidate và topic traversal

Candidate phải thỏa toàn bộ:

- Grade bằng Student current grade.
- Status `Approved`.
- Question active.
- Có topic thuộc selected subtree.
- Có latest `QuestionVersion` schema V2 hợp lệ.
- Snapshot khớp active Answer/QuestionPart shape.
- DefaultWeight dương.
- Question type/part structure được Grading hỗ trợ.

Topic subtree traversal dùng visited set. Selected topic được tính cả khi là topic lá; chỉ descendants active được thêm vào subtree.

Phần EF query/filter riêng của BlueprintExam và TopicPractice được giữ tách biệt. Logic dựng và xác minh immutable candidate được tách thành pure/shared TestGen component để tránh sao chép rule snapshot.

## 8. Selection policy

### Difficulty quota

```text
Level 1: 3
Level 2: 4
Level 3: 2
Level 4: 1
```

Selector lấy quota hiện có trước. Slot thiếu được bù từ candidate còn lại có khoảng cách level gần nhất với level thiếu; nếu bằng nhau, level thấp hơn được ưu tiên.

### Composite cap

Tối đa hai Composite trong kết quả cuối. Composite không phải quota bắt buộc. Khi cap đã đạt, Composite còn lại không được dùng cho quota hoặc fallback.

### Recent-question preference

Trong cùng priority group:

1. Question chưa từng có trong Test personal của Student.
2. Question đã gặp, sắp theo lần xuất hiện gần nhất từ cũ đến mới.
3. Tie cuối được random hóa qua injected `IGenerationRandomizer`.

Không cấm lặp tuyệt đối.

## 9. Persistence và scoring

Test được lưu với:

```text
BlueprintID          = NULL
TestStatus           = Active
TestMode             = TopicPractice
GeneratedForStudentID = authenticated StudentID
GeneratedBy          = System
TestName             = "Luyện tập: {TagName}"
TestCode             = NULL
DurationMinutes      = 0
TotalQuestions       = 10
MaxScore             = 10.00
ScoringPolicy        = NormalizedWeight
```

Mỗi TestQuestion:

```text
SourceBlueprintDetailID = NULL
SelectionReason         = TopicPractice
IsAdaptiveSelected      = false
RecommendedForTagID     = selected TagID
RecommendedDifficultyID = NULL
PtagAtSelection         = NULL
RuleVersion             = TopicPractice-v1
QuestionVersionID       = latest valid version
WeightSnapshot          = Question.DefaultWeight
IsScoreInvalidated      = false
InvalidatedByReportID   = NULL
```

`ScoringAllocator.Allocate(10.00, weights)` phân bổ MaxPointsSnapshot và bảo đảm tổng chính xác 10.00.

Scoring rule:

- SingleChoice: `AllOrNothing`.
- ShortAnswer: `AllOrNothing`.
- Composite có đúng bốn Boolean parts: `TieredTrueFalse`.
- Composite còn lại: `WeightedParts`.

Test và mười TestQuestion được ghi trong cùng transaction. Stable TestID và CreatedTime được tạo ngoài execution strategy. Ambiguous commit chỉ được coi thành công khi persisted aggregate thỏa toàn bộ post-condition.

## 10. Unlimited Practice

SQL constraint của Test đổi từ `DurationMinutes > 0` thành `DurationMinutes >= 0`.

Business rules:

- BlueprintExam vẫn bắt buộc duration dương.
- TopicPractice bắt buộc duration bằng 0.
- Duration bằng 0 nghĩa là không có deadline.

Testing session responses bổ sung:

```text
HasTimeLimit      bool
RemainingSeconds int?
ElapsedSeconds   int
```

Với unlimited session:

- `HasTimeLimit = false`.
- `RemainingSeconds = null`.
- Backend tính `ElapsedSeconds` từ StartTime.
- Autosave không trả session-expired vì thời gian.
- Timeout-submit trả `TESTING_TEST_HAS_NO_TIME_LIMIT`.
- Student tự submit.
- TestSession.Duration lưu số giây thực tế khi submit.
- Resume tiếp tục elapsed timer.

## 11. Frontend design

Routes:

```text
/student/test           shared BlueprintExam discovery
/student/test/topics    TopicPractice selection
/student/test/:sessionId shared test-taking screen
```

Hai trang discovery dùng local tabs `Đề thi` và `Luyện theo chủ đề`; không thêm sidebar item.

TopicPractice page gồm:

- Search.
- Topic tree.
- Available count và trạng thái đủ 10 câu.
- Disabled state có giải thích khi `canGenerate = false`.
- Confirmation dialog hiển thị 10 câu, không giới hạn thời gian và tối đa hai Composite.
- Loading/error/empty states.
- Submit lock chống double-click.
- Generate thành công thì start session và điều hướng đến màn làm bài.

`SessionTimer` hỗ trợ countdown và elapsed mode. Unlimited Practice hiển thị `Đã làm HH:MM:SS`, không có warning, timeout hoặc proctoring UI. Start dialog và test metadata hiển thị `Không giới hạn` khi duration bằng 0.

Frontend được triển khai trực tiếp trong React + Tailwind theo design tokens/component hiện có. Không dùng Stitch; Antigravity có thể dùng ui-ux-pro-max để review accessibility, tree interaction, disabled states và feedback.

## 12. Verification

Backend phải chứng minh:

- Chỉ Student claim hợp lệ truy cập được.
- Options chỉ trả active topic đúng grade.
- Parent selection bao gồm descendants active và không loop khi dữ liệu cycle.
- Candidate filter loại Question/version/child shape không hợp lệ.
- Difficulty quota và fallback đúng.
- Không quá hai Composite.
- Ưu tiên unseen rồi oldest seen.
- Pool dưới 10 trả 409 và không có write.
- Test có đúng 10 unique Question.
- Tổng MaxPointsSnapshot bằng 10.00.
- Persistence audit fields đúng.
- Student khác không start được personal Test.
- TopicPractice session có TestFormat Practice và unlimited behavior.
- Timed BlueprintExam countdown/timeout không bị regression.
- SQL migration chạy được trên schema hiện tại và fresh schema có cùng constraint.

Frontend phải chứng minh:

- Options loading/error/empty/success states.
- Search và tree interaction dùng được bằng keyboard.
- Topic thiếu câu bị disable.
- Generate không double-submit.
- Unlimited metadata không hiển thị `0 phút`.
- Timer count-up resume đúng.
- Exam countdown và proctoring UI không regression.
- Production build thành công.

## 13. Branch và commit boundaries

Checkpoint nên thực hiện trên branch riêng sau khi commit `e2cd6aa` được merge/cherry-pick vào main và file untracked QuestionBank test được xử lý riêng.

Các commit dự kiến:

1. `docs(testgen): define topic practice generation contract`
2. `feat(testgen): add topic practice options and selector`
3. `feat(testgen): persist personal topic practice tests`
4. `feat(testing): support unlimited practice sessions`
5. `feat(student-ui): add topic practice workflow`
6. `test(topic-practice): add cross-module regression coverage`
