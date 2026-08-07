/**
 * Centralized API client for the Chatbot (UC-51) feature.
 * Backend: POST /api/v1/chatbot/assist
 * Requires: Bearer token (Student role) — handled by api.js interceptor.
 *
 * Rate limit (server-side): 1 request per (studentId, sessionId) pair.
 * Returns HTTP 429 when limit is exceeded, 503 on Gemini timeout.
 */
import api from './api';

/**
 * UC-51: Gửi câu hỏi và đáp án tới AI để nhận giải thích từng bước.
 *
 * @param {object} params
 * @param {string} params.sessionId       - UUID của phiên làm bài
 * @param {string} params.questionId      - UUID của câu hỏi
 * @param {string} params.questionContent - Nội dung câu hỏi (markdown)
 * @param {string} params.studentAnswer   - Đáp án đúng dạng text để AI giải thích
 * @param {string} params.userMessage     - Câu hỏi thêm của học sinh
 * @returns {Promise<{ explanation: string }>}
 */
export async function askChatbot({ sessionId, questionId, questionContent, studentAnswer, userMessage }) {
  // Gộp context câu hỏi + câu hỏi của học sinh vào studentAnswer để AI có đủ ngữ cảnh
  const contextualAnswer = userMessage
    ? `${studentAnswer}\n\n[Câu hỏi thêm của học sinh]: ${userMessage}`
    : studentAnswer;

  const response = await api.post('/chatbot/assist', {
    sessionId,
    questionId,
    questionContent,
    studentAnswer: contextualAnswer,
  });
  return response.data;
}
