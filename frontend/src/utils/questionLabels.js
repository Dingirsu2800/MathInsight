export function normalizeQuestionType(type) {
  if (!type) return "";
  const cleaned = String(type).trim().replace(/[\s_-]+/g, "").toUpperCase();

  if (cleaned === "SINGLECHOICE" || cleaned === "SINGLE") return "SINGLE_CHOICE";
  if (cleaned === "MULTIPLECHOICE" || cleaned === "MULTIPLESELECT" || cleaned === "MULTIPLE") return "MULTIPLE_CHOICE";
  if (cleaned === "TRUEFALSE" || cleaned === "YESNO") return "TRUE_FALSE";
  if (cleaned === "SHORTANSWER" || cleaned === "SHORT") return "SHORT_ANSWER";
  if (cleaned === "COMPOSITE") return "COMPOSITE";

  return type;
}

export const QUESTION_TYPE_LABELS = {
  SINGLE_CHOICE: "Trắc nghiệm một đáp án",
  MULTIPLE_CHOICE: "Trắc nghiệm nhiều đáp án",
  TRUE_FALSE: "Đúng / Sai",
  SHORT_ANSWER: "Trả lời ngắn",
  COMPOSITE: "Câu hỏi nhiều mệnh đề",
};

export function normalizeQuestionPartType(type) {
  if (!type) return "";
  const cleaned = String(type).trim().replace(/[\s_-]+/g, "").toUpperCase();

  if (cleaned === "TRUEFALSE" || cleaned === "YESNO") return "TRUE_FALSE";
  if (cleaned === "SHORTANSWER" || cleaned === "SHORT") return "SHORT_ANSWER";
  if (cleaned === "NUMERICANSWER" || cleaned === "NUMERIC") return "NUMERIC_ANSWER";

  return type;
}

export const QUESTION_PART_TYPE_LABELS = {
  TRUE_FALSE: "Đúng / Sai",
  SHORT_ANSWER: "Trả lời ngắn",
  NUMERIC_ANSWER: "Điền kết quả số",
};

export function getQuestionTypeLabel(type) {
  const norm = normalizeQuestionType(type);
  return QUESTION_TYPE_LABELS[norm] || type || "Chưa xác định";
}

export function getQuestionPartTypeLabel(type) {
  const norm = normalizeQuestionPartType(type);
  return QUESTION_PART_TYPE_LABELS[norm] || type || "Chưa xác định";
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
  const norm = normalizeQuestionType(type);
  return QUESTION_TYPE_SHORT_LABELS[norm] || getQuestionTypeLabel(norm);
}
