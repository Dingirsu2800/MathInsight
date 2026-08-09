import axios from 'axios';
import { getAccessToken } from './authStorage';

const RAW_API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  import.meta.env.VITE_API_URL ||
  'http://localhost:8080';

function normalizeApiBaseUrl(value) {
  const baseUrl = value.replace(/\/+$/, '');
  if (baseUrl.endsWith('/api/v1')) return baseUrl;
  if (baseUrl.endsWith('/api')) return `${baseUrl}/v1`;
  return `${baseUrl}/api/v1`;
}

export const API_BASE_URL = normalizeApiBaseUrl(RAW_API_BASE_URL);

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
}, (error) => {
  return Promise.reject(error);
});

export default api;
