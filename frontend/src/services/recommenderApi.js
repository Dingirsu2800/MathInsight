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
 * UC-53: Lấy bài giảng đề xuất dựa theo chủ đề yếu.
 * @returns {Promise<RecommendedLectureResponse[]>}
 * Array of { lectureId, title, description, tagId, tagName, officialPoint, isRemedial }
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
