/**
 * Centralized API client for the Notification module, plus a SignalR helper for the real-time
 * /hubs/notification push channel.
 * Backend: GET/PUT /api/v1/notifications/*, hub at {API_BASE}/hubs/notification.
 */
import * as signalR from '@microsoft/signalr';
import api from './api';
import { getAccessToken } from './authStorage';

// Same base-URL resolution as api.js/questionBankApiClient.js, but WITHOUT the /api/v1 suffix —
// the SignalR hub is mounted at the host root, not under the REST API version prefix.
const RAW_API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ||
  import.meta.env.VITE_API_URL ||
  'http://localhost:8080';

const HUB_URL = `${RAW_API_BASE_URL.replace(/\/+$/, '')}/hubs/notification`;

/**
 * Lấy danh sách thông báo (phân trang).
 * @param {{unreadOnly?: boolean, pageIndex?: number, pageSize?: number}} params
 * @returns {Promise<{items, pageIndex, pageSize, totalCount, totalPages}>}
 */
export async function getNotifications(params = {}) {
  const response = await api.get('/notifications', { params });
  return response.data;
}

/** Đánh dấu một thông báo là đã đọc. */
export async function markNotificationRead(notificationId) {
  const response = await api.put(`/notifications/${notificationId}/read`);
  return response.data;
}

/**
 * Mở kết nối SignalR tới /hubs/notification và gọi `onReceive(notification)` cho mỗi thông báo
 * mới ("ReceiveNotification"). Trả về hàm để đóng kết nối khi component unmount.
 * @param {(notification: object) => void} onReceive
 * @returns {Promise<() => void>} stop function
 */
export async function connectNotificationHub(onReceive) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, { accessTokenFactory: () => getAccessToken() })
    .withAutomaticReconnect()
    .build();

  connection.on('ReceiveNotification', onReceive);

  await connection.start();

  return () => connection.stop();
}
