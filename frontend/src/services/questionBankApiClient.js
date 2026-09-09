import axios from 'axios';
import { getAccessToken } from './authStorage';
import { attachTokenRefreshInterceptor } from './tokenRefreshCoordinator';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  import.meta.env.VITE_API_URL ||
  'http://localhost:8080';

const REFRESH_URL = '/api/v1/auth/refresh';

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

attachTokenRefreshInterceptor(client, {
  refreshUrl: `${API_BASE_URL}${REFRESH_URL}`,
});

export default client;
