export const QUESTION_TYPE_LABELS = {
  SINGLE_CHOICE: "Trắc nghiệm một đáp án",
  MULTIPLE_CHOICE: "Trắc nghiệm nhiều đáp án",
  TRUE_FALSE: "Đúng / Sai",
  SHORT_ANSWER: "Trả lời ngắn",
  COMPOSITE: "Câu hỏi nhiều mệnh đề",
};

export const QUESTION_PART_TYPE_LABELS = {
  TRUE_FALSE: "Đúng / Sai",
  SHORT_ANSWER: "Trả lời ngắn",
  NUMERIC_ANSWER: "Điền kết quả số",
};

export function getQuestionTypeLabel(type) {
  return QUESTION_TYPE_LABELS[type] || type || "Chưa xác định";
}

export function getQuestionPartTypeLabel(type) {
  return QUESTION_PART_TYPE_LABELS[type] || type || "Chưa xác định";
}

export const QUESTION_STATUS_LABELS = {
  APPROVED: "Đã duyệt",
  REPORTED: "Bị báo cáo",
  REJECTED: "Từ chối",
  DEACTIVATED: "Ngừng sử dụng",
  PENDING: "Chờ duyệt",
};

export function normalizeQuestionStatus(status) {
  if (!status) return "";
  return String(status).trim().replace(/\s+/g, "_").toUpperCase();
}

export function getQuestionStatusLabel(status) {
  const normalized = normalizeQuestionStatus(status);
  return QUESTION_STATUS_LABELS[normalized] || status || "Chưa xác định";
}

export function getQuestionStatusVariant(status) {
  return normalizeQuestionStatus(status) || "default";
}

export const QUESTION_TYPE_SHORT_LABELS = {
  SINGLE_CHOICE: "Một đáp án",
  MULTIPLE_CHOICE: "Nhiều đáp án",
  TRUE_FALSE: "Đúng / Sai",
  SHORT_ANSWER: "Trả lời ngắn",
  COMPOSITE: "Nhiều mệnh đề",
};

export function getQuestionTypeShortLabel(type) {
  return QUESTION_TYPE_SHORT_LABELS[type] || getQuestionTypeLabel(type);
}
