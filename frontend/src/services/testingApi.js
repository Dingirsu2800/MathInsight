/**
 * Centralized API client for the Testing module (003).
 * Backend: /api/v1/testing/*
 * Covers UC-47 (session lifecycle), UC-49 (submit), UC-50 (solution).
 * Requires: Bearer token (Student role) in Authorization header (handled by api.js interceptor).
 */
import api from './api';

/**
 * UC-47: Start a new test session.
 * Creates TestSession + TestAnswer stubs for all questions.
 * @param {string} testId - UUID of the test
 * @returns {Promise<StartSessionResponse>}
 *   { sessionId, testId, status, testFormat, totalQuestions, durationMinutes,
 *     startTime, endTime, questions[] }
 *   questions: { questionId, questionNo, questionContent, questionType, options[], parts[] }
 */
export async function startSession(testId) {
  const response = await api.post('/testing/sessions/start', { testId });
  return response.data;
}

/**
 * UC-47: Auto-save student answers.
 * Persists answer selections + sets FirstChoiceTime / UpdateChoiceTime.
 * @param {string} sessionId - UUID of the session
 * @param {AutoSaveAnswerDto[]} answers - Array of answer updates
 * @returns {Promise<AutoSaveResponse>} { savedAt, remainingSeconds }
 */
export async function autoSaveAnswers(sessionId, answers) {
  const response = await api.post(`/testing/sessions/${sessionId}/auto-save`, { answers });
  return response.data;
}

/**
 * UC-47: Record a proctoring incident (tab switch, focus loss).
 * After 5 incidents the backend will auto force-submit (BR-10).
 * @param {string} sessionId - UUID of the session
 * @param {string} incidentType - 'TAB_SWITCH' | 'FOCUS_LOSS'
 * @returns {Promise<RecordIncidentResponse>} { totalIncidents, forceSubmitted }
 */
export async function recordIncident(sessionId, incidentType) {
  const response = await api.post(`/testing/sessions/${sessionId}/incidents`, { incidentType });
  return response.data;
}

/**
 * UC-49: Submit the test session for grading.
 * For Practice mode, grading is synchronous; for Exam mode, it may be async.
 * @param {string} sessionId - UUID of the session
 * @returns {Promise<SubmitSessionResponse>}
 *   { sessionId, status, submissionType, score, numCorrect, numIncorrect, numAbandoned }
 */
export async function submitSession(sessionId) {
  const response = await api.post(`/testing/sessions/${sessionId}/submit`);
  return response.data;
}

/**
 * UC-50: Get detailed solution after session is Graded.
 * Returns full question + answer data with correctness info.
 * @param {string} sessionId - UUID of the session
 * @returns {Promise<DetailedSolutionResponse>}
 *   { sessionId, testName, questions[] }
 *   questions: { questionId, questionNo, questionContent, selectedAnswerId, isCorrect, ... }
 */
export async function getDetailedSolution(sessionId) {
  const response = await api.get(`/testing/sessions/${sessionId}/solution`);
  return response.data;
}
