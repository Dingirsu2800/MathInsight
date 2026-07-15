export function getBlueprintErrorMessage(err, fallback = "Có lỗi xảy ra. Vui lòng thử lại.") {
  if (!err) return fallback;

  // Retrieve code from standard Axios error response structures
  const code = err.response?.data?.code || err.data?.code || err.message || err;

  const mappings = {
    "BLUEPRINT_NOT_FOUND": "Cấu trúc đề không tồn tại hoặc đã ngừng sử dụng.",
    "BLUEPRINT_REQUEST_INVALID": "Dữ liệu cấu trúc đề không hợp lệ.",
    "REQUEST_INVALID": "Dữ liệu cấu trúc đề không hợp lệ.",
    "BLUEPRINT_MUTATION_FORBIDDEN": "Bạn không có quyền chỉnh sửa cấu trúc đề này.",
    "BLUEPRINT_SELF_REVIEW_FORBIDDEN": "Bạn không thể phản biện cấu trúc do mình tạo.",
    "BLUEPRINT_STATUS_INVALID": "Trạng thái hiện tại không cho phép thao tác này.",
    "BLUEPRINT_STRUCTURE_INVALID": "Phần thi hoặc phân bổ câu hỏi chưa hợp lệ.",
    "BLUEPRINT_TOTAL_MISMATCH": "Tổng số câu giữa cấu trúc, phần thi và phân bổ chưa khớp.",
    "BLUEPRINT_REVIEW_NOTE_REQUIRED": "Vui lòng nhập lý do từ chối.",
    "BLUEPRINT_REVIEW_NOTE_TOO_LONG": "Lý do từ chối không được vượt quá 2000 ký tự.",
    "BLUEPRINT_TAXONOMY_INVALID": "Chủ đề hoặc độ khó không còn hợp lệ với khối lớp.",
    "BLUEPRINT_IN_USE": "Cấu trúc đang chờ phản biện hoặc đã có lịch sử sử dụng."
  };

  return mappings[code] || fallback;
}
