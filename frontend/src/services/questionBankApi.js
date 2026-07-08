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

  getQuestionVersions(questionId) {
    return client.get(`/api/question-bank/questions/${questionId}/versions`);
  },

  getTopicTags(grade) {
    return client.get("/api/question-bank/tags/topics", {
      params: grade ? { grade } : undefined,
    });
  },

  getDifficulties() {
    return client.get("/api/question-bank/tags/difficulties");
  },
};
