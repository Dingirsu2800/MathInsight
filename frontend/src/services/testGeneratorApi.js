import client from "./questionBankApiClient";

export const testGeneratorApi = {
  getBlueprints(params) {
    const queryParams = {};
    if (params) {
      Object.keys(params).forEach((key) => {
        if (params[key] !== undefined && params[key] !== null && params[key] !== "") {
          queryParams[key] = params[key];
        }
      });
    }
    return client.get("/api/test-generator/blueprints", { params: queryParams });
  },

  getPendingBlueprints(params) {
    const queryParams = {};
    if (params) {
      Object.keys(params).forEach((key) => {
        if (params[key] !== undefined && params[key] !== null && params[key] !== "") {
          queryParams[key] = params[key];
        }
      });
    }
    return client.get("/api/test-generator/blueprints/pending", { params: queryParams });
  },

  getBlueprintDetail(blueprintId) {
    return client.get(`/api/test-generator/blueprints/${blueprintId}`);
  },

  createBlueprint(payload) {
    return client.post("/api/test-generator/blueprints", payload);
  },

  updateBlueprint(blueprintId, payload) {
    return client.put(`/api/test-generator/blueprints/${blueprintId}`, payload);
  },

  submitBlueprintForReview(blueprintId) {
    return client.post(`/api/test-generator/blueprints/${blueprintId}/submit`);
  },

  reviewBlueprint(blueprintId, payload) {
    return client.post(`/api/test-generator/blueprints/${blueprintId}/review`, payload);
  },

  cloneBlueprint(blueprintId) {
    return client.post(`/api/test-generator/blueprints/${blueprintId}/clone`);
  },

  deleteBlueprint(blueprintId) {
    return client.delete(`/api/test-generator/blueprints/${blueprintId}`);
  },

  generateSharedBlueprintExam(blueprintId, payload) {
    return client.post(`/api/test-generator/blueprints/${blueprintId}/tests`, payload);
  },

  getExpertTestPreview(testId) {
    return client.get(`/api/test-generator/tests/${testId}/expert-preview`);
  },

  archiveSharedBlueprintExam(testId) {
    return client.patch(`/api/test-generator/tests/${testId}/status`, { status: "Archived" });
  },

  getSharedBlueprintExams(params) {
    const queryParams = {};
    if (params) {
      Object.keys(params).forEach((key) => {
        if (params[key] !== undefined && params[key] !== null && params[key] !== "") {
          queryParams[key] = params[key];
        }
      });
    }
    return client.get("/api/test-generator/tests/shared-blueprint-exams", { params: queryParams });
  },

  resolveTestCode(testCode) {
    return client.post("/api/test-generator/tests/resolve-code", { testCode });
  },

  startSession(testId) {
    return client.post("/api/v1/tests/sessions/start", { testId });
  },

  getSessionContent(sessionId) {
    return client.get(`/api/v1/tests/sessions/${sessionId}`);
  },

  recordIncident(sessionId, type = "TAB_SWITCH") {
    return client.post(`/api/v1/tests/sessions/${sessionId}/incident`, { type });
  },

  submitSession(sessionId) {
    return client.post(`/api/v1/tests/sessions/${sessionId}/submit`);
  },

  autoSaveSession(sessionId, answers) {
    return client.post(`/api/v1/tests/sessions/${sessionId}/auto-save`, { answers });
  }
};
