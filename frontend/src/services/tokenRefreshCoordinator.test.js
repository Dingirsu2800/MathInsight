import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import axios from 'axios';
import {
  refreshAuthTokens,
  attachTokenRefreshInterceptor,
  resetRefreshStateForTesting,
  isAuthEndpoint,
  getAuthRefreshUrl,
  defaultRedirectToLogin,
} from './tokenRefreshCoordinator';
import * as authStorage from './authStorage';

describe('tokenRefreshCoordinator', () => {
  let mockAxiosInstance;
  let mockRedirectToLogin;

  beforeEach(() => {
    resetRefreshStateForTesting();
    mockRedirectToLogin = vi.fn();
    mockAxiosInstance = {
      post: vi.fn(),
    };

    vi.spyOn(authStorage, 'getAccessToken').mockReturnValue('stale-access-token');
    vi.spyOn(authStorage, 'getRefreshToken').mockReturnValue('valid-refresh-token');
    vi.spyOn(authStorage, 'updateTokens').mockImplementation(() => {});
    vi.spyOn(authStorage, 'clearAuthSession').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('isAuthEndpoint & getAuthRefreshUrl', () => {
    it('detects auth endpoints correctly', () => {
      expect(isAuthEndpoint('/api/v1/auth/login')).toBe(true);
      expect(isAuthEndpoint('/api/v1/auth/refresh')).toBe(true);
      expect(isAuthEndpoint('/auth/login')).toBe(true);
      expect(isAuthEndpoint('/api/v1/tests/sessions/start')).toBe(false);
      expect(isAuthEndpoint('/api/questions/101')).toBe(false);
      expect(isAuthEndpoint(null)).toBe(false);
    });

    it('derives auth refresh URL correctly regardless of trailing slashes or /api/v1 suffix', () => {
      expect(getAuthRefreshUrl('http://localhost:8080')).toBe('http://localhost:8080/api/v1/auth/refresh');
      expect(getAuthRefreshUrl('http://localhost:8080/api/v1')).toBe('http://localhost:8080/api/v1/auth/refresh');
      expect(getAuthRefreshUrl('http://localhost:8080/api')).toBe('http://localhost:8080/api/v1/auth/refresh');
    });
  });

  describe('refreshAuthTokens concurrent execution', () => {
    it('fires only ONE refresh HTTP request when called concurrently from multiple callers', async () => {
      mockAxiosInstance.post.mockImplementation(async () => {
        // Simulate network delay
        await new Promise((r) => setTimeout(r, 20));
        return {
          data: {
            accessToken: 'fresh-access-token-999',
            refreshToken: 'fresh-refresh-token-888',
          },
        };
      });

      // Three concurrent refresh calls
      const call1 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });
      const call2 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });
      const call3 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      const [res1, res2, res3] = await Promise.all([call1, call2, call3]);

      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);
      expect(mockAxiosInstance.post).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/auth/refresh'),
        { refreshToken: 'valid-refresh-token' },
        expect.any(Object),
      );

      expect(res1).toBe('fresh-access-token-999');
      expect(res2).toBe('fresh-access-token-999');
      expect(res3).toBe('fresh-access-token-999');

      expect(authStorage.updateTokens).toHaveBeenCalledWith({
        accessToken: 'fresh-access-token-999',
        refreshToken: 'fresh-refresh-token-888',
      });
      expect(mockRedirectToLogin).not.toHaveBeenCalled();
    });

    it('rejects and calls redirectToLogin when refresh token is missing', async () => {
      vi.spyOn(authStorage, 'getRefreshToken').mockReturnValue(null);

      await expect(
        refreshAuthTokens({
          axiosInstance: mockAxiosInstance,
          redirectToLogin: mockRedirectToLogin,
        }),
      ).rejects.toThrow('No refresh token available');

      expect(mockAxiosInstance.post).not.toHaveBeenCalled();
      expect(mockRedirectToLogin).toHaveBeenCalled();
    });

    it('rejects all queued waiters and cleans session when refresh HTTP call fails', async () => {
      const refreshError = new Error('Refresh token revoked');
      refreshError.response = { status: 401 };

      mockAxiosInstance.post.mockImplementation(async () => {
        await new Promise((r) => setTimeout(r, 10));
        throw refreshError;
      });

      const call1 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });
      const call2 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      await expect(call1).rejects.toThrow('Refresh token revoked');
      await expect(call2).rejects.toThrow('Refresh token revoked');

      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);
      expect(mockRedirectToLogin).toHaveBeenCalled();
    });

    it('times out when refresh HTTP call exceeds timeoutMs, rejects all waiters, and resets coordinator state', async () => {
      // Mock an axios post that hangs longer than timeoutMs
      mockAxiosInstance.post.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({ data: { accessToken: 'late' } }), 100))
      );

      const call1 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 30,
      });
      const call2 = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 30,
      });

      await expect(call1).rejects.toThrow(/timed out/i);
      await expect(call2).rejects.toThrow(/timed out/i);

      expect(mockRedirectToLogin).toHaveBeenCalled();
      // Verify coordinator has reset
      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);

      // Subsequent call can now execute without being blocked
      mockAxiosInstance.post.mockResolvedValueOnce({
        data: { accessToken: 'fresh-after-timeout', refreshToken: 'refresh-after-timeout' },
      });
      const subsequentCall = await refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 50,
      });
      expect(subsequentCall).toBe('fresh-after-timeout');
    });

    it('discards late response from timed-out attempt A: does not write token A and does not settle attempt B queue', async () => {
      let resolveA;
      const promiseA = new Promise((resolve) => { resolveA = resolve; });

      let resolveB;
      const promiseB = new Promise((resolve) => { resolveB = resolve; });

      let postCallCount = 0;
      mockAxiosInstance.post.mockImplementation(() => {
        postCallCount += 1;
        if (postCallCount === 1) return promiseA;
        if (postCallCount === 2) return promiseB;
        return Promise.resolve({ data: { accessToken: 'default' } });
      });

      // 1. Refresh A starts with short timeout (30ms)
      const callA = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 30,
      });

      // Attempt A times out
      await expect(callA).rejects.toThrow(/timed out/i);
      expect(mockRedirectToLogin).toHaveBeenCalledTimes(1);

      // Auth was cleared after timeout. Set active refresh token for attempt B:
      authStorage.getRefreshToken.mockReturnValue('new-valid-refresh-token-B');
      authStorage.updateTokens.mockClear();

      // 2. Start Attempt B (timeout 1000ms)
      const callB = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 1000,
      });

      // Caller 2 joins Attempt B's queue
      const waiterB = refreshAuthTokens({
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
        timeoutMs: 1000,
      });

      // 3. Response A arrives late!
      resolveA({
        data: {
          accessToken: 'stale-token-A',
          refreshToken: 'stale-refresh-A',
        },
      });

      // Allow any microtasks / promises for A to run
      await new Promise((r) => setTimeout(r, 20));

      // Assert Attempt A's late response was completely ignored:
      // - It must NOT write token A
      expect(authStorage.updateTokens).not.toHaveBeenCalled();

      // - It must NOT settle callB or waiterB (they must still be pending)
      let callBSettled = false;
      let waiterBSettled = false;
      callB.then(() => { callBSettled = true; }).catch(() => { callBSettled = true; });
      waiterB.then(() => { waiterBSettled = true; }).catch(() => { waiterBSettled = true; });
      await new Promise((r) => setTimeout(r, 10));
      expect(callBSettled).toBe(false);
      expect(waiterBSettled).toBe(false);

      // 4. Now Attempt B completes successfully
      resolveB({
        data: {
          accessToken: 'fresh-token-B',
          refreshToken: 'fresh-refresh-B',
        },
      });

      const resultB = await callB;
      const resultWaiterB = await waiterB;

      expect(resultB).toBe('fresh-token-B');
      expect(resultWaiterB).toBe('fresh-token-B');
      expect(authStorage.updateTokens).toHaveBeenCalledTimes(1);
      expect(authStorage.updateTokens).toHaveBeenCalledWith({
        accessToken: 'fresh-token-B',
        refreshToken: 'fresh-refresh-B',
      });
    });
  });

  describe('attachTokenRefreshInterceptor on Axios instances', () => {
    it('retries 401 request with the newly refreshed access token', async () => {
      const client = axios.create();

      let attempt = 0;
      client.defaults.adapter = vi.fn(async (config) => {
        attempt++;
        if (attempt === 1) {
          const err = new Error('Unauthorized');
          err.response = { status: 401 };
          err.config = config;
          throw err;
        }
        return {
          data: { success: true, attempt },
          status: 200,
          statusText: 'OK',
          headers: {},
          config,
        };
      });

      mockAxiosInstance.post.mockResolvedValue({
        data: {
          accessToken: 'new-token-111',
          refreshToken: 'new-refresh-222',
        },
      });

      attachTokenRefreshInterceptor(client, {
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      const res = await client.post('/tests/sessions/123/auto-save', { answers: [] });
      expect(res.data.success).toBe(true);
      expect(res.config.headers.Authorization).toBe('Bearer new-token-111');
      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);
      expect(mockRedirectToLogin).not.toHaveBeenCalled();
    });

    it('shares single refresh across two separate clients that hit 401 concurrently', async () => {
      const clientA = axios.create({ baseURL: 'http://localhost:8080/api/v1' });
      const clientB = axios.create({ baseURL: 'http://localhost:8080' });

      attachTokenRefreshInterceptor(clientA, {
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });
      attachTokenRefreshInterceptor(clientB, {
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      let attemptsA = 0;
      clientA.defaults.adapter = vi.fn(async (config) => {
        attemptsA++;
        if (attemptsA === 1) {
          const err = new Error('Unauthorized A');
          err.response = { status: 401 };
          err.config = config;
          throw err;
        }
        return {
          data: { client: 'A', attempts: attemptsA },
          status: 200,
          statusText: 'OK',
          headers: {},
          config,
        };
      });

      let attemptsB = 0;
      clientB.defaults.adapter = vi.fn(async (config) => {
        attemptsB++;
        if (attemptsB === 1) {
          const err = new Error('Unauthorized B');
          err.response = { status: 401 };
          err.config = config;
          throw err;
        }
        return {
          data: { client: 'B', attempts: attemptsB },
          status: 200,
          statusText: 'OK',
          headers: {},
          config,
        };
      });

      mockAxiosInstance.post.mockImplementation(async () => {
        await new Promise((r) => setTimeout(r, 20));
        return {
          data: {
            accessToken: 'unified-token-777',
            refreshToken: 'unified-refresh-777',
          },
        };
      });

      // Fire concurrent requests on both clients
      const reqA = clientA.post('/tests/sessions/s-1/auto-save');
      const reqB = clientB.get('/api/questions/q-1');

      const [resA, resB] = await Promise.all([reqA, reqB]);

      expect(resA.data.client).toBe('A');
      expect(resB.data.client).toBe('B');
      expect(resA.config.headers.Authorization).toBe('Bearer unified-token-777');
      expect(resB.config.headers.Authorization).toBe('Bearer unified-token-777');

      // CRITICAL: Only 1 refresh HTTP call for both clients!
      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);
    });

    it('prevents infinite loops when retried request also returns 401', async () => {
      const client = axios.create();

      client.defaults.adapter = vi.fn(async (config) => {
        const err = new Error('Unauthorized persistent');
        err.response = { status: 401 };
        err.config = config;
        throw err;
      });

      mockAxiosInstance.post.mockResolvedValue({
        data: {
          accessToken: 'fresh-token',
          refreshToken: 'fresh-refresh',
        },
      });

      attachTokenRefreshInterceptor(client, {
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      await expect(client.get('/some/protected/endpoint')).rejects.toThrow('Unauthorized persistent');

      // Refresh was attempted once, but after replay failed with 401, it stopped and redirected
      expect(mockAxiosInstance.post).toHaveBeenCalledTimes(1);
      expect(mockRedirectToLogin).toHaveBeenCalled();
    });

    it('never triggers refresh on auth endpoint 401s', async () => {
      const client = axios.create();

      client.defaults.adapter = vi.fn(async (config) => {
        const err = new Error('Bad credentials');
        err.response = { status: 401 };
        err.config = config;
        throw err;
      });

      attachTokenRefreshInterceptor(client, {
        axiosInstance: mockAxiosInstance,
        redirectToLogin: mockRedirectToLogin,
      });

      await expect(client.post('/api/v1/auth/login', { username: 'foo' })).rejects.toThrow('Bad credentials');

      expect(mockAxiosInstance.post).not.toHaveBeenCalled();
      expect(mockRedirectToLogin).not.toHaveBeenCalled();
    });
  });

  describe('defaultRedirectToLogin', () => {
    const originalLocation = window.location;

    beforeEach(() => {
      delete window.location;
      window.location = {
        pathname: '/student/dashboard',
        href: 'http://localhost:3000/student/dashboard',
      };
    });

    afterEach(() => {
      window.location = originalLocation;
    });

    it('clears session and redirects to /login when outside test session', () => {
      window.location.pathname = '/student/dashboard';
      defaultRedirectToLogin();
      expect(authStorage.clearAuthSession).toHaveBeenCalled();
      expect(window.location.href).toBe('/login');
    });

    it('clears session but does NOT redirect away when student is on test session route', () => {
      window.location.pathname = '/student/test/session-xyz-123';
      window.location.href = 'http://localhost:3000/student/test/session-xyz-123';
      defaultRedirectToLogin();
      expect(authStorage.clearAuthSession).toHaveBeenCalled();
      // href must NOT be changed to /login to protect unsaved draft
      expect(window.location.href).toBe('http://localhost:3000/student/test/session-xyz-123');
    });

    it('does not redirect if already on /login', () => {
      window.location.pathname = '/login';
      window.location.href = 'http://localhost:3000/login';
      defaultRedirectToLogin();
      expect(authStorage.clearAuthSession).toHaveBeenCalled();
      expect(window.location.href).toBe('http://localhost:3000/login');
    });
  });
});
