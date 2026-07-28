export const TEST_GENERATION_ERROR_MAP = {
  TEST_GENERATION_REQUEST_INVALID: "Thông tin đề chưa hợp lệ.",
  BLUEPRINT_MUTATION_FORBIDDEN: "Bạn không có quyền sinh đề từ cấu trúc này.",
  BLUEPRINT_STATUS_INVALID: "Trạng thái cấu trúc đề không cho phép sinh đề.",
  BLUEPRINT_STRUCTURE_INVALID: "Cấu trúc đề chưa hợp lệ.",
  BLUEPRINT_SCORE_BUDGET_MISMATCH: "Tổng điểm các phần không khớp tổng điểm đề.",
  QUESTION_VERSION_MISSING: "Một số câu hỏi chưa có phiên bản hợp lệ.",
  QUESTION_POOL_INSUFFICIENT: "Ngân hàng câu hỏi chưa đủ để sinh đề.",
  TEST_GENERATION_CONFLICT: "Không thể hoàn tất sinh đề, vui lòng thử lại.",
  TEST_CODE_NOT_AVAILABLE: "Mã đề không khả dụng.",
  GENERATED_TEST_NOT_FOUND: "Không tìm thấy đề đã sinh.",
  TESTING_TEST_ACCESS_DENIED: "Bạn không thể bắt đầu đề này.",
  RATE_LIMIT_EXCEEDED: "Bạn thao tác quá nhanh, vui lòng thử lại sau.",
  TESTING_SESSION_EXPIRED: "Phiên làm bài thi đã hết thời gian làm bài.",
  TESTING_SESSION_NOT_EXPIRED: "Phiên làm bài thi chưa hết giờ làm bài.",
  TESTING_SESSION_ALREADY_IN_PROGRESS: "Bạn đang có một phiên làm bài chưa hoàn thành."
};

export function getTestGenErrorMessage(err, defaultMessage = "Thao tác thất bại. Vui lòng thử lại sau.") {
  if (!err) return defaultMessage;

  const code = err.response?.data?.code || err.code;
  if (code && TEST_GENERATION_ERROR_MAP[code]) {
    return TEST_GENERATION_ERROR_MAP[code];
  }

  const status = err.response?.status;
  if (status === 429) {
    return TEST_GENERATION_ERROR_MAP.RATE_LIMIT_EXCEEDED;
  }

  return defaultMessage;
}
