import axios from 'axios';
import {
  getAccessToken,
  getRefreshToken,
  updateTokens,
  clearAuthSession,
} from './authStorage';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  import.meta.env.VITE_API_URL ||
  'http://localhost:8080';

const REFRESH_URL = '/api/v1/auth/refresh';

// Every backend auth route lives under this prefix. Requests to it must NEVER trigger the
// token-refresh flow (see the response interceptor for the reasoning).
const AUTH_PATH_PREFIX = '/api/v1/auth/';

function isAuthEndpoint(url) {
  return typeof url === 'string' && url.includes(AUTH_PATH_PREFIX);
}

const client = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

client.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
}, (error) => {
  return Promise.reject(error);
});

// --- Token refresh on 401 -------------------------------------------------
// A single refresh runs at a time. Requests that hit 401 while a refresh is
// in flight are parked in `pendingQueue` and replayed once it settles, so we
// never fire N concurrent refresh calls (single-use rotation would reject the
// losers). Loops are prevented by `_retry` and by never refreshing the
// refresh call itself.
let isRefreshing = false;
let pendingQueue = [];

function flushQueue(error, token) {
  pendingQueue.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token);
    }
  });
  pendingQueue = [];
}

function redirectToLogin() {
  clearAuthSession();
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

client.interceptors.response.use(
  (response) => response,
  (error) => {
    const originalRequest = error.config;
    const status = error.response?.status;

    // Only 401s that have a request we can replay are candidates for refresh.
    if (status !== 401 || !originalRequest) {
      return Promise.reject(error);
    }

    // Auth endpoints (login, refresh, reset-password, register, confirm-*) are
    // unauthenticated or session-establishing: a 401 from them is a genuine auth error
    // that the caller must surface — NOT an expired-access-token signal. Running the
    // refresh flow here would (a) fire a bogus /refresh using a stale localStorage token
    // and (b) replace the endpoint's real error (e.g. AUTH_INVALID_CREDENTIALS on login)
    // with the refresh error (AUTH_TOKEN_INVALID). Propagate the original error untouched,
    // without clearing storage or redirecting.
    if (isAuthEndpoint(originalRequest.url)) {
      return Promise.reject(error);
    }

    // Retry a protected request only once.
    if (originalRequest._retry) {
      redirectToLogin();
      return Promise.reject(error);
    }

    const refreshToken = getRefreshToken();
    if (!refreshToken) {
      redirectToLogin();
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    // A refresh is already running: wait for it, then replay with the new token.
    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        pendingQueue.push({ resolve, reject });
      }).then((token) => {
        originalRequest.headers.Authorization = `Bearer ${token}`;
        return client(originalRequest);
      });
    }

    isRefreshing = true;

    return new Promise((resolve, reject) => {
      // Bare axios (not `client`) so the refresh call bypasses this interceptor.
      axios
        .post(
          `${API_BASE_URL}${REFRESH_URL}`,
          { refreshToken },
          { headers: { 'Content-Type': 'application/json' } },
        )
        .then(({ data }) => {
          const newAccessToken = data.accessToken || data.AccessToken;
          const newRefreshToken = data.refreshToken || data.RefreshToken;

          updateTokens({ accessToken: newAccessToken, refreshToken: newRefreshToken });
          client.defaults.headers.common.Authorization = `Bearer ${newAccessToken}`;
          originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;

          flushQueue(null, newAccessToken);
          resolve(client(originalRequest));
        })
        .catch((refreshError) => {
          flushQueue(refreshError, null);
          redirectToLogin();
          reject(refreshError);
        })
        .finally(() => {
          isRefreshing = false;
        });
    });
  },
);

export default client;
