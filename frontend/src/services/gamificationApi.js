/**
 * Centralized API client for the Gamification module.
 * Backend: GET/POST/PUT /api/v1/gamification/*
 * Requires: Bearer token (Student role) in Authorization header (handled by api.js interceptor).
 */
import api from './api';

/**
 * UC-81: Lấy chuỗi ngày học hiện tại của học sinh.
 * @returns {Promise<{currentStreak, longestStreak, lastActivityDate, isActive}>}
 */
export async function getStreak() {
  const response = await api.get('/gamification/streak');
  return response.data;
}

/**
 * UC-82: Lấy toàn bộ danh sách huy hiệu, kèm trạng thái đã đạt được hay chưa.
 * @returns {Promise<Array<{badgeId, badgeName, description, iconUrl, isEarned, earnedTime}>>}
 */
export async function getBadges() {
  const response = await api.get('/gamification/badges');
  return response.data;
}

/**
 * UC-84: Lấy tiến độ hướng tới các huy hiệu chưa đạt được.
 * @returns {Promise<Array<{badgeId, badgeName, requiredValue, currentValue, progressPercentage}>>}
 */
export async function getBadgeProgress() {
  const response = await api.get('/gamification/badges/progress');
  return response.data;
}

/**
 * UC-87: Lấy danh sách mục tiêu điểm theo từng chủ đề, kèm điểm hiện tại.
 * @returns {Promise<Array<{targetId, tagId, tagName, targetPoint, currentPoint, isAchieved, createdTime}>>}
 */
export async function getTargets() {
  const response = await api.get('/gamification/targets');
  return response.data;
}

/** UC-85: Đặt mục tiêu điểm mới cho một chủ đề. */
export async function createTarget(tagId, targetPoint) {
  const response = await api.post('/gamification/targets', { tagId, targetPoint });
  return response.data;
}

/** UC-86: Cập nhật mục tiêu điểm đã đặt. */
export async function updateTarget(targetId, targetPoint) {
  const response = await api.put(`/gamification/targets/${targetId}`, { targetPoint });
  return response.data;
}
