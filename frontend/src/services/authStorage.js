// Centralized auth-session storage.
//
// This is the SINGLE source of truth for the localStorage keys used by
// authentication. Every reader (axios clients, ProtectedRoute, feature pages)
// must go through these helpers so the key set stays consistent across the app.

export const STORAGE_KEYS = {
  accessToken: "access_token",
  refreshToken: "refresh_token",
  roleName: "role_name",
  accountId: "account_id",
  // BR-06. Teacher only: "pending" | "rejected" | "approved" | "none". Advisory — it decides
  // where the client routes, never what the caller may do; the backend re-checks approval on
  // every request via the "TeacherApproved" policy.
  applicationStatus: "application_status",
};

// Keys used by older builds. Cleared on logout so stale tokens never linger.
const LEGACY_KEYS = ["token", "AccountId", "RoleName"];

export function setAuthSession({ accessToken, refreshToken, roleName, accountId, applicationStatus }) {
  if (accessToken != null) localStorage.setItem(STORAGE_KEYS.accessToken, accessToken);
  if (refreshToken != null) localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken);
  if (roleName != null) localStorage.setItem(STORAGE_KEYS.roleName, roleName);
  if (accountId != null) localStorage.setItem(STORAGE_KEYS.accountId, accountId);

  // Absent for non-teachers: clear any value left by a previous session on this browser.
  if (applicationStatus != null) {
    localStorage.setItem(STORAGE_KEYS.applicationStatus, applicationStatus);
  } else {
    localStorage.removeItem(STORAGE_KEYS.applicationStatus);
  }
}

// Called after a resubmission flips the application back to pending, so the stored value does
// not keep saying "rejected" until the next login.
export function setApplicationStatus(applicationStatus) {
  if (applicationStatus == null) {
    localStorage.removeItem(STORAGE_KEYS.applicationStatus);
    return;
  }
  localStorage.setItem(STORAGE_KEYS.applicationStatus, applicationStatus);
}

// Overwrite only the token pair (used after a refresh rotation).
export function updateTokens({ accessToken, refreshToken }) {
  if (accessToken != null) localStorage.setItem(STORAGE_KEYS.accessToken, accessToken);
  if (refreshToken != null) localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken);
}

export function getAccessToken() {
  return localStorage.getItem(STORAGE_KEYS.accessToken);
}

export function getRefreshToken() {
  return localStorage.getItem(STORAGE_KEYS.refreshToken);
}

export function getRoleName() {
  return localStorage.getItem(STORAGE_KEYS.roleName);
}

export function getAccountId() {
  return localStorage.getItem(STORAGE_KEYS.accountId);
}

export function getApplicationStatus() {
  return localStorage.getItem(STORAGE_KEYS.applicationStatus);
}

export function clearAuthSession() {
  Object.values(STORAGE_KEYS).forEach((key) => localStorage.removeItem(key));
  LEGACY_KEYS.forEach((key) => localStorage.removeItem(key));
}
