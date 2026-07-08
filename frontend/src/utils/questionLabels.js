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
