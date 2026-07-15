import client from './questionBankApiClient';

// ── Lectures ──────────────────────────────────────────
export const getLectures = (params) =>
  client.get('/api/v1/lectures', { params });

export const getLecture = (id) =>
  client.get(`/api/v1/lectures/${id}`);

export const createLecture = (data) =>
  client.post('/api/v1/lectures', data);

export const updateLecture = (id, data) =>
  client.put(`/api/v1/lectures/${id}`, data);

export const publishLecture = (id) =>
  client.post(`/api/v1/lectures/${id}/publish`);

export const deactivateLecture = (id) =>
  client.post(`/api/v1/lectures/${id}/deactivate`);

export const likeLecture = (id) =>
  client.post(`/api/v1/lectures/${id}/like`);

export const unlikeLecture = (id) =>
  client.delete(`/api/v1/lectures/${id}/like`);

// ── Materials ─────────────────────────────────────────
export const getMaterials = (params) =>
  client.get('/api/v1/materials', { params });

export const uploadMaterial = (formData) =>
  client.post('/api/v1/materials', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });

export const updateMaterial = (id, data) =>
  client.put(`/api/v1/materials/${id}`, data);

export const deactivateMaterial = (id) =>
  client.post(`/api/v1/materials/${id}/deactivate`);

export const attachMaterial = (materialId, lectureId) =>
  client.post(`/api/v1/materials/${materialId}/attach`, { lectureId });

// ── Discussions ───────────────────────────────────────
export const getDiscussions = (lectureId, params) =>
  client.get(`/api/v1/discussions/lectures/${lectureId}`, { params });

export const askQuestion = (data) =>
  client.post('/api/v1/discussions/questions', data);

export const answerQuestion = (questionId, data) =>
  client.post(`/api/v1/discussions/questions/${questionId}/answers`, data);

export const reportDiscussion = (data) =>
  client.post('/api/v1/discussions/reports', data);

// ── Moderation ────────────────────────────────────────
export const getModerationQueue = (params) =>
  client.get('/api/v1/discussions/moderation-queue', { params });

export const resolveReport = (reportId, isDismissed) =>
  client.post(`/api/v1/discussions/reports/${reportId}/resolve`, { isDismissed });

export const hideComment = (id, isQuestion) =>
  client.post(`/api/v1/discussions/comments/${id}/hide`, { isQuestion });

export const updateComment = (id, isQuestion, content) =>
  client.put(`/api/v1/discussions/comments/${id}`, { isQuestion, content });

export const deleteComment = (id, isQuestion) =>
  client.delete(`/api/v1/discussions/comments/${id}`, { params: { isQuestion } });

// ── Topics ────────────────────────────────────────────
export const getTopics = (grade) =>
  client.get('/api/v1/topics', { params: { grade } });
