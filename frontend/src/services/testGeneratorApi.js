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
  }
};
