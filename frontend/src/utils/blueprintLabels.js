export const statusLabels = {
  Draft: "Bản nháp",
  PendingReview: "Chờ phản biện",
  Approved: "Đã duyệt",
  Rejected: "Bị từ chối",
  Active: "Đang sử dụng",
  Deactivated: "Ngừng sử dụng",

  DRAFT: "Bản nháp",
  PENDINGREVIEW: "Chờ phản biện",
  PENDING_REVIEW: "Chờ phản biện",
  APPROVED: "Đã duyệt",
  REJECTED: "Bị từ chối",
  ACTIVE: "Đang sử dụng",
  DEACTIVATED: "Ngừng sử dụng",
};

export const statusBadgeVariants = {
  Draft: "DEFAULT",
  PendingReview: "WARNING",
  Approved: "SUCCESS",
  Rejected: "ERROR",
  Active: "PRIMARY",
  Deactivated: "SECONDARY",

  DRAFT: "DEFAULT",
  PENDINGREVIEW: "WARNING",
  PENDING_REVIEW: "WARNING",
  APPROVED: "SUCCESS",
  REJECTED: "ERROR",
  ACTIVE: "PRIMARY",
  DEACTIVATED: "SECONDARY",
};

export function getStatusLabel(status) {
  if (!status) return "Chưa xác định";
  return statusLabels[status] || status;
}

export function getStatusBadgeVariant(status) {
  if (!status) return "DEFAULT";
  return statusBadgeVariants[status] || "DEFAULT";
}

const questionTypeLabels = {
  SingleChoice: "Trắc nghiệm một đáp án",
  MultipleChoice: "Trắc nghiệm nhiều đáp án",
  TrueFalse: "Đúng / Sai",
  ShortAnswer: "Trả lời ngắn",
  Composite: "Câu hỏi nhiều mệnh đề"
};

export function getQuestionTypeLabel(questionType) {
  return questionTypeLabels[questionType] || questionType || "Chưa xác định";
}
