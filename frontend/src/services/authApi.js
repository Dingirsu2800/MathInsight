/**
 * Centralized API client for the Identity_Access / Auth module.
 * Backend: POST /api/v1/auth/login, POST /api/v1/auth/logout
 *
 * Handles token + user-info persistence in localStorage so that
 * other components can simply call getStoredUser().
 */
import api from './api';

const STORAGE_KEYS = {
  token: 'token',
  accountId: 'AccountId',
  roleName: 'RoleName',
  username: 'Username',
  email: 'Email',
};

/**
 * Login and persist session data.
 * @param {string} usernameOrEmail
 * @param {string} password
 * @returns {Promise<{ accessToken: string, accountId: string, roleName: string, username: string, email: string, expiresAt: string }>}
 */
export async function login(usernameOrEmail, password) {
  const response = await api.post('/auth/login', { usernameOrEmail, password });
  const data = response.data || {};

  const token = data.accessToken || data.AccessToken;
  if (!token) {
    throw new Error('Không nhận được mã xác thực (Token) từ hệ thống.');
  }

  const roleName = data.roleName || data.RoleName || '';
  const accountId = data.accountId || data.AccountId || '';
  const username = data.username || data.Username || '';
  const email = data.email || data.Email || '';

  // Persist to localStorage
  localStorage.setItem(STORAGE_KEYS.token, token);
  localStorage.setItem(STORAGE_KEYS.accountId, accountId);
  localStorage.setItem(STORAGE_KEYS.roleName, roleName);
  localStorage.setItem(STORAGE_KEYS.username, username);
  localStorage.setItem(STORAGE_KEYS.email, email);

  return { accessToken: token, accountId, roleName, username, email, expiresAt: data.expiresAt || data.ExpiresAt };
}

/**
 * Logout: call backend to blacklist JWT, then clear localStorage.
 * Always clears local state even if the API call fails (network error, etc.).
 */
export async function logout() {
  try {
    await api.post('/auth/logout');
  } catch {
    // Silently ignore — we still want to clear local session
  }
  clearStoredSession();
}

/**
 * Retrieve persisted user info (set during login).
 * @returns {{ accountId: string, roleName: string, username: string, email: string } | null}
 */
export function getStoredUser() {
  const token = localStorage.getItem(STORAGE_KEYS.token);
  if (!token) return null;

  return {
    accountId: localStorage.getItem(STORAGE_KEYS.accountId) || '',
    roleName: localStorage.getItem(STORAGE_KEYS.roleName) || '',
    username: localStorage.getItem(STORAGE_KEYS.username) || '',
    email: localStorage.getItem(STORAGE_KEYS.email) || '',
  };
}

/**
 * Quick check whether we have a persisted token.
 * @returns {boolean}
 */
export function isAuthenticated() {
  return !!localStorage.getItem(STORAGE_KEYS.token);
}

/** Internal: remove all auth-related localStorage entries. */
function clearStoredSession() {
  Object.values(STORAGE_KEYS).forEach((key) => localStorage.removeItem(key));
  // Also clear legacy key used by older code
  localStorage.removeItem('access_token');
}
