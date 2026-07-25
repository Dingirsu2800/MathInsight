import client from "./questionBankApiClient";

export const questionBankApi = {
  getQuestions(params) {
    return client.get("/api/question-bank/questions", { params });
  },

  getQuestionDetail(questionId) {
    return client.get(`/api/question-bank/questions/${questionId}`);
  },

  createQuestion(payload) {
    return client.post("/api/question-bank/questions", payload);
  },

  updateQuestion(questionId, payload) {
    return client.put(`/api/question-bank/questions/${questionId}`, payload);
  },

  deleteQuestion(questionId) {
    return client.delete(`/api/question-bank/questions/${questionId}`);
  },

  getQuestionVersions(questionId) {
    return client.get(`/api/question-bank/questions/${questionId}/versions`);
  },

  getTopicTags(params) {
    const queryParams = typeof params === "object" ? params : { grade: params };
    return client.get("/api/question-bank/tags/topics", { params: queryParams });
  },

  createTopic(payload) {
    return client.post("/api/question-bank/tags/topics", payload);
  },

  updateTopic(tagId, payload) {
    return client.put(`/api/question-bank/tags/topics/${tagId}`, payload);
  },

  getDifficulties(params) {
    return client.get("/api/question-bank/tags/difficulties", { params });
  },

  createDifficulty(payload) {
    return client.post("/api/question-bank/tags/difficulties", payload);
  },

  updateDifficulty(difficultyId, payload) {
    return client.put(`/api/question-bank/tags/difficulties/${difficultyId}`, payload);
  },

  reportQuestion(questionId, payload) {
    return client.post(`/api/question-bank/questions/${questionId}/reports`, payload);
  },

  getMyReportedQuestions(params) {
    return client.get("/api/question-bank/reports/mine", { params });
  },

  getQuestionReports(questionId, params) {
    return client.get(`/api/question-bank/questions/${questionId}/reports`, { params });
  },

  updateQuestionReportStatus(reportId, payload) {
    return client.patch(`/api/question-bank/reports/${reportId}`, payload);
  },

  retryQuestionReportScoreAdjustment(reportId) {
    return client.post(`/api/question-bank/reports/${reportId}/retry-score-adjustment`);
  },

  uploadQuestionImage(file) {
    const formData = new FormData();
    formData.append("file", file);
    return client.post("/api/question-bank/questions/image-upload", formData, {
      headers: {
        "Content-Type": undefined
      }
    });
  },

  submitQuestionReportReview(reportId) {
    return client.post(`/api/question-bank/reports/${reportId}/submit-review`);
  },

  extractQuestionOcrDraft(file) {
    const formData = new FormData();
    formData.append("file", file);
    return client.post("/api/question-bank/questions/ocr-draft", formData, {
      headers: {
        "Content-Type": undefined
      }
    });
  },

  getQuestionImportTemplate() {
    return client.get("/api/question-bank/questions/import-template", {
      responseType: "blob"
    });
  },

  previewQuestionImport(file) {
    const formData = new FormData();
    formData.append("file", file);
    return client.post("/api/question-bank/questions/import-preview", formData, {
      headers: {
        "Content-Type": undefined
      }
    });
  },

  confirmQuestionImport(payload) {
    return client.post("/api/question-bank/questions/import-confirm", payload);
  }
};
