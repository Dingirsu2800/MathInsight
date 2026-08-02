/**
 * Centralized API client for the Notification-Report module's report endpoints.
 * Backend: GET /api/v1/reports/*
 * Requires: Bearer token (Student role) in Authorization header (handled by api.js interceptor).
 */
import api from './api';

/**
 * UC-58: Lấy bảng xếp hạng theo khối lớp.
 * @param {number} grade - 10, 11, hoặc 12.
 * @returns {Promise<Array<{rank, studentId, studentName, grade, point}>>}
 */
export async function getLeaderboard(grade) {
  const response = await api.get('/reports/leaderboard', { params: { grade } });
  return response.data;
}
