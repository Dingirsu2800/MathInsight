export const TEST_GENERATION_ERROR_MAP = {
  TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE: "Hệ thống gợi ý đang tạm thời gián đoạn. Vui lòng thử lại.",
  TOPIC_PRACTICE_RECOMMENDATION_INVALID: "Dữ liệu gợi ý hiện chưa thể dùng để tạo bài. Vui lòng thử lại sau.",
  AUTH_INVALID_TOKEN: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
  TEST_GENERATION_REQUEST_INVALID: "Thông tin đề chưa hợp lệ.",
  BLUEPRINT_MUTATION_FORBIDDEN: "Bạn không có quyền sinh đề từ cấu trúc này.",
  BLUEPRINT_STATUS_INVALID: "Trạng thái cấu trúc đề không cho phép sinh đề.",
  BLUEPRINT_STRUCTURE_INVALID: "Cấu trúc đề chưa hợp lệ.",
  BLUEPRINT_SCORE_BUDGET_MISMATCH: "Tổng điểm các phần không khớp tổng điểm đề.",
  QUESTION_VERSION_MISSING: "Một số câu hỏi chưa có phiên bản hợp lệ.",
  QUESTION_POOL_INSUFFICIENT: "Ngân hàng câu hỏi chưa đủ để sinh đề.",
  TEST_GENERATION_CONFLICT: "Không thể hoàn tất sinh đề. Vui lòng thử lại.",
  TEST_CODE_NOT_AVAILABLE: "Mã đề không khả dụng.",
  GENERATED_TEST_NOT_FOUND: "Không tìm thấy đề đã sinh.",
  TESTING_TEST_ACCESS_DENIED: "Bạn không thể bắt đầu đề này.",
  TESTING_TEST_NOT_FOUND: "Không tìm thấy phiên làm bài thi.",
  RATE_LIMIT_EXCEEDED: "Bạn thao tác quá nhanh, vui lòng thử lại sau.",
  TESTING_SESSION_EXPIRED: "Phiên làm bài thi đã hết thời gian làm bài.",
  TESTING_SESSION_NOT_EXPIRED: "Phiên làm bài thi chưa hết giờ làm bài.",
  TESTING_SESSION_ALREADY_IN_PROGRESS: "Bạn đang có một phiên làm bài chưa hoàn thành.",
  TOPIC_PRACTICE_STUDENT_NOT_FOUND: "Không tìm thấy thông tin học sinh.",
  TOPIC_PRACTICE_TOPIC_NOT_FOUND: "Chủ đề học tập không tồn tại hoặc đã bị xóa.",
  TOPIC_PRACTICE_TOPIC_UNAVAILABLE: "Chủ đề này hiện chưa khả dụng cho khối lớp của bạn.",
  TOPIC_PRACTICE_INSUFFICIENT_QUESTIONS: "Chủ đề chưa đủ 10 câu hỏi hợp lệ để tạo bài luyện tập.",
  TOPIC_PRACTICE_GENERATION_CONFLICT: "Hệ thống chưa thể tạo bài luyện tập. Vui lòng thử lại.",
  TESTING_TEST_HAS_NO_TIME_LIMIT: "Bài luyện tập này không có giới hạn thời gian.",
  TOPIC_PARENT_NOT_ASSIGNABLE: "Nhóm chủ đề không thể gán trực tiếp cho câu hỏi hay đề thi.",
  TOPIC_MUST_BE_DIRECT_CHILD: "Chỉ được chọn chủ đề con trực tiếp.",
  TOPIC_PARENT_GRADE_MISMATCH: "Khối lớp của chủ đề không khớp với nhóm chủ đề cha.",
  TOPIC_DEPTH_LIMIT_EXCEEDED: "Cấu trúc chủ đề chỉ hỗ trợ tối đa 2 cấp.",
  TOPIC_PRACTICE_GRADE_NOT_ALLOWED: "Bạn chỉ có thể luyện tập các chủ đề thuộc khối lớp hiện tại hoặc thấp hơn.",
  STUDENT_GRADE_REQUIRED: "Cần cập nhật thông tin khối lớp để xem danh sách luyện tập.",
  FIXED_TEST_BLUEPRINT_NOT_APPROVED: "Cấu trúc đề phải ở trạng thái Đã thông qua hoặc Đang sử dụng mới được tạo đề cố định.",
  FIXED_TEST_QUESTION_DUPLICATED: "Danh sách câu hỏi được chọn có câu bị trùng lặp.",
  FIXED_TEST_ORDER_INVALID: "Thứ tự câu hỏi chưa hợp lệ hoặc không liên tục.",
  FIXED_TEST_DETAIL_QUANTITY_MISMATCH: "Số lượng câu hỏi đã chọn không khớp với chỉ tiêu ma trận đề thi.",
  FIXED_TEST_QUESTION_NOT_ELIGIBLE: "Một số câu hỏi được chọn không đáp ứng điều kiện của ma trận đề thi.",
  FIXED_TEST_QUESTION_VERSION_UNAVAILABLE: "Phiên bản câu hỏi được chọn hiện không khả dụng.",
  TEST_ALREADY_ARCHIVED: "Đề thi này đã ở trạng thái đã lưu trữ."
};

export function getTestGenErrorMessage(err, defaultMessage = "Thao tác thất bại. Vui lòng thử lại sau.") {
  if (!err) return defaultMessage;

  const code = err.response?.data?.code || err.code;
  if (code && TEST_GENERATION_ERROR_MAP[code]) {
    return TEST_GENERATION_ERROR_MAP[code];
  }

  const status = err.response?.status;
  if (status === 401) {
    return TEST_GENERATION_ERROR_MAP.AUTH_INVALID_TOKEN;
  }
  if (status === 403) {
    return TEST_GENERATION_ERROR_MAP.TESTING_TEST_ACCESS_DENIED;
  }
  if (status === 429) {
    return TEST_GENERATION_ERROR_MAP.RATE_LIMIT_EXCEEDED;
  }

  // Closed mapping: use caller's defaultMessage when code is unmapped
  return defaultMessage;
}
