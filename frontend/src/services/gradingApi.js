/**
 * Centralized API client for the Grading Analytics module.
 * Backend: /api/v1/grading/*
 * Requires: Bearer token (Student role) in Authorization header (handled by api.js interceptor).
 */
import api from './api';

/**
 * UC-55: Lấy kết quả chi tiết của một phiên thi.
 * @param {string} sessionId - UUID của session
 * @returns {Promise<SessionResultDto>}
 *   { sessionId, testId, testFormat, status, score, numCorrect, numIncorrect,
 *     numAbandoned, totalQuestion, durationMinutes, submittedAt, answers[] }
 */
export async function getSessionResult(sessionId) {
  const response = await api.get(`/grading/sessions/${sessionId}`);
  return response.data;
}

/**
 * UC-56: Lấy lịch sử làm bài (phân trang, có lọc).
 * @param {{ page?: number, pageSize?: number, testFormat?: string, fromDate?: string, toDate?: string }} params
 * @returns {Promise<PagedResult<SessionHistoryDto>>}
 *   { page, pageSize, totalCount, totalPages, items[] }
 *   items: { sessionId, testId, testFormat, status, score, numCorrect, numIncorrect,
 *            numAbandoned, totalQuestion, durationMinutes, submittedAt, submissionType }
 */
export async function getSessionHistory(params = {}) {
  const response = await api.get('/grading/student/history', { params });
  return response.data;
}

/**
 * UC-56: Lấy thống kê tổng hợp lịch sử làm bài của học sinh.
 * @returns {Promise<StudentHistoryStatsDto>}
 *   { totalSessions, sessionsLast30Days, averageScore, accuracyPercent }
 */
export async function getStudentHistoryStats() {
  const response = await api.get('/grading/student/stats');
  return response.data;
}

/** Báo cáo một câu hỏi đúng theo version đã xuất hiện trong phiên làm bài. */
export async function reportSessionQuestion(sessionId, questionId, reportReason) {
  const response = await api.post(
    `/tests/sessions/${sessionId}/questions/${questionId}/report`,
    { reportReason }
  );
  return response.data;
}
