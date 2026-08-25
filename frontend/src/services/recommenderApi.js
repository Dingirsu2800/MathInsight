/**
 * Centralized API client for the Recommender module.
 * Backend: GET /api/v1/recommender/*
 * Requires: Bearer token (Student role) in Authorization header (handled by api.js interceptor).
 */
import api from './api';

/**
 * UC-52: Lấy danh sách chủ đề yếu của học sinh.
 * @returns {Promise<WeakTagDto[]>} Array of { tagId, tagName, officialPoint }
 */
export async function getWeakTags() {
  const response = await api.get('/recommender/weak-tags');
  return response.data;
}

/**
 * UC-55: Lấy toàn bộ TagsMastery của học sinh (bao gồm cả tag đang ổn và thành thạo).
 * Dùng cho Competency page — TopicMasteryGrid, CompetencySummaryCard, RadarChartCard.
 * @returns {Promise<TagMasteryDto[]>} Array of { tagId, tagName, officialPoint, numberDone, masteryStatus, recommendedDifficultyLevel }
 */
export async function getAllTagsMastery() {
  const response = await api.get('/recommender/topic-mastery');
  return response.data;
}

/**
 * Tính điểm năng lực tổng quát từ danh sách topic mastery.
 * Chỉ tính trung bình các chủ đề đã được làm bài (numberDone > 0).
 * @param {TagMasteryDto[]} tagsMastery
 * @returns {number|null} Điểm trung bình làm tròn 1 chữ số thập phân (0-10), hoặc null nếu chưa có bài làm.
 */
export function calculateOverallCompetencyScore(tagsMastery) {
  if (!Array.isArray(tagsMastery) || tagsMastery.length === 0) return null;
  const practiced = tagsMastery.filter((t) => t.numberDone > 0);
  if (practiced.length === 0) return null;
  const avg = practiced.reduce((sum, t) => sum + Number(t.officialPoint || 0), 0) / practiced.length;
  return Math.round(avg * 10) / 10;
}

/**
 * UC-53: Lấy bài giảng đề xuất dựa theo chủ đề yếu.
 * @returns {Promise<RecommendedLectureResponse[]>}
 * Array of { lectureId, title, description, tagId, tagName, officialPoint, isRemedial, difficultyLevel }
 */
export async function getRecommendedLectures() {
  const response = await api.get('/recommender/lectures');
  return response.data;
}

/**
 * UC-54: Lấy tài liệu đề xuất dựa theo chủ đề yếu.
 * @returns {Promise<RecommendedMaterialResponse[]>}
 * Array of { materialId, title, description, fileUrl, materialType, tagId, tagName, officialPoint, isRemedial }
 */
export async function getRecommendedMaterials() {
  const response = await api.get('/recommender/materials');
  return response.data;
}

export const RECOMMENDER_ERROR_MAP = {
  LECTURE_DIFFICULTY_REQUIRED: "Vui lòng chọn độ khó cho bài giảng.",
  LECTURE_DIFFICULTY_NOT_FOUND: "Độ khó bài giảng không tồn tại.",
  LECTURE_DIFFICULTY_INACTIVE: "Độ khó bài giảng hiện đang tạm ngưng.",
  LECTURE_TOPIC_INACTIVE: "Chủ đề học tập hiện đang tạm ngưng.",
  LECTURE_RECOMMENDATION_UNAVAILABLE: "Hệ thống gợi ý bài giảng hiện chưa sẵn sàng. Vui lòng thử lại sau.",
  AUTH_INVALID_TOKEN: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
};

export function getRecommenderErrorMessage(err, defaultMsg = "Không thể tải bài giảng đề xuất. Vui lòng thử lại sau.") {
  if (!err) return defaultMsg;
  const code = err.response?.data?.code || err.code;
  if (code && RECOMMENDER_ERROR_MAP[code]) {
    return RECOMMENDER_ERROR_MAP[code];
  }
  if (err.response?.status === 401) {
    return RECOMMENDER_ERROR_MAP.AUTH_INVALID_TOKEN;
  }
  if (err.response?.status === 503) {
    return RECOMMENDER_ERROR_MAP.LECTURE_RECOMMENDATION_UNAVAILABLE;
  }
  return defaultMsg;
}
