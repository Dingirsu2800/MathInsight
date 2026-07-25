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
};

// Keys used by older builds. Cleared on logout so stale tokens never linger.
const LEGACY_KEYS = ["token", "AccountId", "RoleName"];

export function setAuthSession({ accessToken, refreshToken, roleName, accountId }) {
  if (accessToken != null) localStorage.setItem(STORAGE_KEYS.accessToken, accessToken);
  if (refreshToken != null) localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken);
  if (roleName != null) localStorage.setItem(STORAGE_KEYS.roleName, roleName);
  if (accountId != null) localStorage.setItem(STORAGE_KEYS.accountId, accountId);
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

export function clearAuthSession() {
  Object.values(STORAGE_KEYS).forEach((key) => localStorage.removeItem(key));
  LEGACY_KEYS.forEach((key) => localStorage.removeItem(key));
}
