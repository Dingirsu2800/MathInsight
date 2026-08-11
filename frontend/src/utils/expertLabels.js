export const ROLE_LABELS = {
  Expert: "Chuyên gia",
  Teacher: "Giáo viên",
  Student: "Học sinh",
  Admin: "Quản trị viên",
  EXPERT: "Chuyên gia",
  TEACHER: "Giáo viên",
  STUDENT: "Học sinh",
  ADMIN: "Quản trị viên",
};

export function getRoleLabel(role) {
  if (!role) return "Chưa xác định";
  return ROLE_LABELS[role] || role;
}

export const SCORING_RULE_LABELS = {
  AllOrNothing: "Tất cả hoặc không",
  TieredTrueFalse: "Phân bậc Đúng / Sai",
  WeightedParts: "Theo trọng số phần",
  ALL_OR_NOTHING: "Tất cả hoặc không",
  TIERED_TRUE_FALSE: "Phân bậc Đúng / Sai",
  WEIGHTED_PARTS: "Theo trọng số phần",
};

export function getScoringRuleLabel(rule) {
  if (!rule) return "Chưa thiết lập";
  return SCORING_RULE_LABELS[rule] || rule;
}

export const GENERATION_TYPE_LABELS = {
  Random: "Ngẫu nhiên",
  Fixed: "Cố định",
  RANDOM: "Ngẫu nhiên",
  FIXED: "Cố định",
};

export function getGenerationTypeLabel(type) {
  if (!type) return "Ngẫu nhiên";
  return GENERATION_TYPE_LABELS[type] || type;
}

export const EXPERT_BLUEPRINT_STATUS_LABELS = {
  Draft: "Bản nháp",
  PendingReview: "Chờ phản biện",
  Approved: "Đã thông qua",
  Rejected: "Cần chỉnh sửa",
  Active: "Đang sử dụng",
  Archived: "Đã lưu trữ",
  Deactivated: "Đã lưu trữ",

  DRAFT: "Bản nháp",
  PENDINGREVIEW: "Chờ phản biện",
  PENDING_REVIEW: "Chờ phản biện",
  APPROVED: "Đã thông qua",
  REJECTED: "Cần chỉnh sửa",
  ACTIVE: "Đang sử dụng",
  ARCHIVED: "Đã lưu trữ",
  DEACTIVATED: "Đã lưu trữ",
};

export function getExpertBlueprintStatusLabel(status) {
  if (!status) return "Chưa xác định";
  return EXPERT_BLUEPRINT_STATUS_LABELS[status] || status;
}

export const TEST_STATUS_LABELS = {
  Active: "Đang sử dụng",
  Archived: "Đã lưu trữ",
  Deactivated: "Đã lưu trữ",
  ACTIVE: "Đang sử dụng",
  ARCHIVED: "Đã lưu trữ",
  DEACTIVATED: "Đã lưu trữ",
};

export function getTestStatusLabel(status) {
  if (!status) return "Chưa xác định";
  return TEST_STATUS_LABELS[status] || status;
}
