import axios from 'axios';
import {
  getRefreshToken,
  updateTokens,
  clearAuthSession,
} from './authStorage';

const RAW_API_BASE_URL =
  import.meta.env?.VITE_API_BASE_URL ||
  import.meta.env?.VITE_API_URL ||
  'http://localhost:8080';

export function getAuthRefreshUrl(rawBaseUrl = RAW_API_BASE_URL) {
  const base = (rawBaseUrl || 'http://localhost:8080').replace(/\/+$/, '');
  const root = base.replace(/\/api(\/v1)?$/, '');
  return `${root}/api/v1/auth/refresh`;
}

export const AUTH_PATH_PREFIX = '/api/v1/auth/';

export function isAuthEndpoint(url) {
  if (typeof url !== 'string') return false;
  return (
    url.includes(AUTH_PATH_PREFIX) ||
    url.includes('/auth/') ||
    url.endsWith('/auth/login') ||
    url.endsWith('/auth/refresh')
  );
}

export function defaultRedirectToLogin() {
  clearAuthSession();
  if (typeof window !== 'undefined' && window.location) {
    // If student is actively taking a test, do not instantly navigate away
    // so they don't lose sight of their current test session and unsubmitted work,
    // allowing TestSession to display the error badge and banner.
    const isTestSession = Boolean(
      window.location.pathname &&
      (window.location.pathname.includes('/student/test/') || window.location.pathname.startsWith('/student/test/'))
    );
    if (!isTestSession && window.location.pathname !== '/login') {
      window.location.href = '/login';
    }
  }
}

// Single in-flight refresh state shared across all axios clients
let isRefreshing = false;
let pendingQueue = [];
let refreshPromise = null;
let currentAttemptId = 0;

export function getRefreshState() {
  return { isRefreshing, pendingQueueLength: pendingQueue.length, currentAttemptId };
}

export function resetRefreshStateForTesting() {
  isRefreshing = false;
  pendingQueue = [];
  refreshPromise = null;
  currentAttemptId += 1;
}

function flushQueue(error, token) {
  const queueToFlush = pendingQueue;
  pendingQueue = [];
  queueToFlush.forEach(({ resolve, reject }) => {
    if (error) {
      reject(error);
    } else {
      resolve(token);
    }
  });
}

export const DEFAULT_REFRESH_TIMEOUT_MS = 10000;

/**
 * Executes or awaits the single active token refresh call.
 * Concurrent callers receive the same Promise and wait for the single HTTP call to settle.
 */
export function refreshAuthTokens(options = {}) {
  const {
    redirectToLogin = defaultRedirectToLogin,
    refreshUrl = getAuthRefreshUrl(),
    axiosInstance = axios,
    timeoutMs = DEFAULT_REFRESH_TIMEOUT_MS,
  } = options;

  if (isRefreshing && refreshPromise) {
    return new Promise((resolve, reject) => {
      pendingQueue.push({ resolve, reject });
    });
  }

  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    redirectToLogin();
    return Promise.reject(new Error('No refresh token available'));
  }

  isRefreshing = true;
  const attemptId = ++currentAttemptId;
  const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;

  refreshPromise = new Promise((resolve, reject) => {
    let timer = null;
    let isSettled = false;

    const handleFailure = (err) => {
      if (timer) {
        clearTimeout(timer);
        timer = null;
      }
      if (isSettled || attemptId !== currentAttemptId) {
        return;
      }
      isSettled = true;
      isRefreshing = false;
      refreshPromise = null;
      flushQueue(err, null);
      redirectToLogin();
      reject(err);
    };

    const handleSuccess = (data) => {
      if (timer) {
        clearTimeout(timer);
        timer = null;
      }
      if (isSettled || attemptId !== currentAttemptId) {
        return;
      }
      isSettled = true;
      const newAccessToken = data?.accessToken || data?.AccessToken;
      const newRefreshToken = data?.refreshToken || data?.RefreshToken;

      if (!newAccessToken) {
        isSettled = false;
        handleFailure(new Error('Refresh response missing access token'));
        return;
      }

      isRefreshing = false;
      refreshPromise = null;
      updateTokens({ accessToken: newAccessToken, refreshToken: newRefreshToken });
      flushQueue(null, newAccessToken);
      resolve(newAccessToken);
    };

    if (timeoutMs && timeoutMs > 0) {
      timer = setTimeout(() => {
        if (attemptId === currentAttemptId && isRefreshing && !isSettled) {
          const timeoutErr = new Error(`Token refresh timed out after ${timeoutMs}ms`);
          timeoutErr.code = 'ECONNABORTED';
          timeoutErr.isTimeout = true;
          if (controller && !controller.signal.aborted) {
            try {
              controller.abort(timeoutErr);
            } catch {
              controller.abort();
            }
          }
          handleFailure(timeoutErr);
        }
      }, timeoutMs);
    }

    axiosInstance
      .post(
        refreshUrl,
        { refreshToken },
        {
          headers: { 'Content-Type': 'application/json' },
          timeout: timeoutMs,
          signal: controller?.signal,
        },
      )
      .then(({ data }) => {
        handleSuccess(data);
      })
      .catch((err) => {
        handleFailure(err);
      });
  });

  return refreshPromise;
}

/**
 * Attaches the shared 401 token-refresh interceptor to any Axios instance.
 */
export function attachTokenRefreshInterceptor(client, options = {}) {
  const {
    redirectToLogin = defaultRedirectToLogin,
    refreshUrl = getAuthRefreshUrl(),
    axiosInstance = axios,
    timeoutMs = DEFAULT_REFRESH_TIMEOUT_MS,
  } = options;

  return client.interceptors.response.use(
    (response) => response,
    async (error) => {
      const originalRequest = error.config;
      const status = error.response?.status;

      // Only 401s that have a request we can replay are candidates for refresh.
      if (status !== 401 || !originalRequest) {
        return Promise.reject(error);
      }

      // Auth endpoints must never trigger token refresh
      if (isAuthEndpoint(originalRequest.url)) {
        return Promise.reject(error);
      }

      // Prevent loops: only retry once
      if (originalRequest._retry) {
        redirectToLogin();
        return Promise.reject(error);
      }

      originalRequest._retry = true;

      try {
        const newAccessToken = await refreshAuthTokens({
          redirectToLogin,
          refreshUrl,
          axiosInstance,
          timeoutMs,
        });

        if (client.defaults?.headers?.common) {
          client.defaults.headers.common.Authorization = `Bearer ${newAccessToken}`;
        }
        originalRequest.headers = originalRequest.headers || {};
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;

        return client(originalRequest);
      } catch (refreshError) {
        return Promise.reject(refreshError);
      }
    },
  );
}
