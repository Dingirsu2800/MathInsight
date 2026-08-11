# Fixed Exam And Direct-Child Topic Agent Handoff

## Required order

1. Backend checkpoint A: direct-child taxonomy and data preflight.
2. Backend checkpoint B: lower-grade TopicPractice and Recommender alignment.
3. Backend checkpoint C: fixed BlueprintExam, after SQL constraint approval.
4. Frontend checkpoint D: taxonomy UI, TopicPractice grouping, fixed composer, and Vietnamese copy.

Do not run A, B, and C concurrently because they share TestGen contracts, errors, SpecKit, and tests. Frontend may begin only after backend response DTOs and error codes are committed.

## Backend prompt for Terra 5.6 high

```text
Bạn đang triển khai backend cho MathInsight tại:
E:\Summer_2026\SEP409\Implementation\MathInsight

Model/effort mong muốn: Terra 5.6, high.

Đọc trước:
- specs/constitution.md
- specs/009-test-generator/fixed-exam-leaf-topic-design.md
- docs/superpowers/plans/2026-08-11-topic-taxonomy-and-practice.md
- docs/superpowers/plans/2026-08-11-fixed-blueprint-exam.md
- specs/002-question-bank/spec.md
- specs/005-recommender/spec.md
- specs/009-test-generator/spec.md
- ../Database/database/001_Create_MathInsight_Azure.sql

Thực hiện đúng thứ tự checkpoint A, B, C trong kế hoạch. Dùng TDD: viết test thất bại, xác nhận thất bại đúng lý do, implement tối thiểu, chạy lại test rồi mới commit từng slice.

Ràng buộc bắt buộc:
- Không revert hoặc commit ba file System test đang dirty từ trước.
- Không tạo EF migration.
- Không tự chạy SQL trên Azure/shared DB.
- Database nằm ngoài Git root ứng dụng; migration 007 và canonical schema phải được báo riêng và chỉ sửa sau khi xác nhận approval cho constraint FixedExam.
- Chỉ topic con trực tiếp của root active cùng grade mới assignable. Root không assignable kể cả không có con.
- TopicPractice dùng topic con của grade <= Student.CurrentGrade và candidate filter dùng grade của topic được chọn.
- Không để lower-grade mastery lọt vào CompetencyPoint của current grade; bỏ fallback average mọi grade.
- Fixed Test reuse Test/TestQuestion, snapshot QuestionVersion, exact order, GeneratedBy Expert, shared Active, SelectionReason FixedExam.
- Backend giữ stable error code tiếng Anh; không hard-code thông báo UI tiếng Việt.

Sau mỗi checkpoint:
1. Review diff theo constitution/spec.
2. Chạy affected module tests.
3. Chạy dotnet build MathInsight.sln --no-restore.
4. Chạy dotnet format MathInsight.sln --verify-no-changes --no-restore.
5. Chạy git diff --check.
6. Báo rõ test level; không gọi InMemory test là SQL integration/UAT.
7. Commit riêng checkpoint với conventional commit.

Dừng và báo blocker nếu canonical schema khác giả định của design, nếu cần thêm cột/bảng, hoặc nếu migration 007 chưa được team duyệt.
```

## Frontend prompt for Antigravity

```text
Triển khai frontend MathInsight tại:
E:\Summer_2026\SEP409\Implementation\MathInsight\frontend

Đọc trước:
- ../specs/constitution.md
- ../specs/009-test-generator/fixed-exam-leaf-topic-design.md
- ../docs/superpowers/plans/2026-08-11-expert-dashboard-frontend.md
- src/pages/expert/BlueprintDetailPage.jsx
- src/pages/expert/TagManagementPage.jsx
- src/components/student/PracticeSetupPanel.jsx
- src/services/testGeneratorApi.js

Chỉ làm frontend React JavaScript + Tailwind + component system hiện tại. Không sửa backend, SQL, seed, Docker, route contract hoặc đổi sang TypeScript. Không thêm dark mode hay thiết kế mobile riêng.

Backend phải được xem là source of truth. Chỉ bắt đầu fixed composer sau khi endpoints và DTO đã tồn tại:
- GET /api/test-generator/blueprints/{blueprintId}/fixed-test-candidates
- POST /api/test-generator/blueprints/{blueprintId}/fixed-tests
- generated Test response có generationType Random/Fixed
- TopicPractice option có grade và parent display data

Thực hiện theo đúng Task 1-7 của frontend plan:
- tập trung nhãn tiếng Việt vào utility maps;
- Tag UI chỉ cho tạo nhóm hoặc topic con trực tiếp;
- Difficulty vẫn phẳng;
- TopicPractice nhóm theo khối/nhóm topic nhưng chỉ topic con chọn được;
- Blueprint Detail có Tạo đề ngẫu nhiên và Tạo đề cố định;
- fixed composer theo BlueprintDetail quota, preview, add/remove/reorder và giữ state khi lỗi;
- generated Test có badge Ngẫu nhiên/Cố định và menu Xem đề/Lưu trữ đề;
- hoàn tất audit tiếng Việt toàn Expert dashboard.

Quy tắc copy:
- Không hiện Tag, Preview, Live Preview, Draft, Expert, UUID, raw scoring rules cho người dùng.
- Dùng sentence case, không viết hoa toàn bộ label.
- Backend error message không được hiện trực tiếp; map code sang tiếng Việt.
- Dùng thuật ngữ: Cấu trúc đề, Ma trận câu hỏi, Chờ phản biện, Đã thông qua, Cần chỉnh sửa, Đang sử dụng, Đã lưu trữ, Ngẫu nhiên, Cố định.

Verification:
- chạy frontend tests;
- npm run build;
- git diff --check;
- browser smoke ở 1280, 1440, 1920;
- kiểm tra dialog, loading, empty, validation và error states;
- không tuyên bố backend E2E/UAT pass nếu endpoint chưa có hoặc chưa test thật.

Commit frontend theo từng task group, không gộp toàn bộ vào một commit lớn.
```

## Review gates

- Gate A: no assignable root topic remains in QuestionBank, Learning, Blueprint, TestGen, or Recommender paths.
- Gate B: grade-access matrix and current-grade CompetencyPoint tests pass.
- Gate C: fixed Test exact-order snapshots and independent archive pass against disposable SQL Server.
- Gate D: frontend build and browser smoke pass with no raw internal value or machine-translated Expert copy.
