import client from "./questionBankApiClient";

export const adminApi = {
  getAccounts(params) {
    return client.get("/api/v1/admin/accounts", { params });
  },

  createAccountManually(payload) {
    return client.post("/api/v1/admin/accounts/manual", payload);
  },

  importAccounts(file) {
    const formData = new FormData();
    formData.append("file", file);
    return client.post("/api/v1/admin/accounts/import", formData, {
      headers: {
        "Content-Type": undefined
      }
    });
  },

  updateAccount(accountId, payload) {
    return client.put(`/api/v1/admin/accounts/${accountId}`, payload);
  },

  toggleAccountStatus(accountId, isActive) {
    return client.put(`/api/v1/admin/accounts/${accountId}/status`, { isActive });
  },

  getApplications(params) {
    return client.get("/api/v1/admin/applications", { params });
  },

  getApplicationDetail(applicationId) {
    return client.get(`/api/v1/admin/applications/${applicationId}`);
  },

  resolveApplication(applicationId, payload) {
    return client.post(`/api/v1/admin/applications/${applicationId}/resolve`, payload);
  },

  getRoles() {
    return client.get("/api/v1/admin/roles");
  },

  updateRole(roleId, payload) {
    return client.put(`/api/v1/admin/roles/${roleId}`, payload);
  },

  adjustPermissions(roleId, payload) {
    return client.put(`/api/v1/admin/roles/${roleId}/permissions`, payload);
  }
};
