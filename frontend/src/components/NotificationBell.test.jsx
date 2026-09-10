import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, useLocation } from 'react-router-dom';
import NotificationBell from './NotificationBell';
import { getNotifications, connectNotificationHub, markNotificationRead } from '../services/notificationApi';

vi.mock('../services/authStorage', () => ({
  getAccessToken: () => 'test-token'
}));

vi.mock('../services/notificationApi', () => ({
  getNotifications: vi.fn(),
  connectNotificationHub: vi.fn(),
  markNotificationRead: vi.fn()
}));

function LocationProbe() {
  const location = useLocation();
  return <span data-testid="location">{location.pathname}</span>;
}

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('NotificationBell notification behavior', () => {
  it('opens the Admin incident review route from an Admin notification', async () => {
    getNotifications.mockResolvedValue({
      items: [{
        notificationId: 'notification-1',
        title: 'Cần duyệt xử lý báo cáo câu hỏi',
        content: 'Một bản sửa câu hỏi đã được gửi để bạn duyệt.',
        link: '/admin/reports/questions?incidentId=incident-101',
        isRead: false,
        createdTime: '2026-09-10T00:00:00Z'
      }]
    });
    connectNotificationHub.mockResolvedValue(() => {});
    markNotificationRead.mockResolvedValue({});

    render(<MemoryRouter><NotificationBell /><LocationProbe /></MemoryRouter>);

    fireEvent.click(await screen.findByRole('button', { name: /Thông báo/i }));
    fireEvent.click(await screen.findByText('Cần duyệt xử lý báo cáo câu hỏi'));

    await waitFor(() => {
      expect(screen.getByTestId('location')).toHaveTextContent('/admin/reports/questions');
    });
  });

  it('renders a processed question report as informational without navigation', async () => {
    getNotifications.mockResolvedValue({
      items: [{
        notificationId: 'notification-2',
        title: 'Báo cáo câu hỏi đã được xử lý',
        content: 'Báo cáo của bạn đã được xử lý.',
        link: '/questions/question-101',
        isRead: false,
        createdTime: '2026-09-10T00:00:00Z'
      }]
    });
    connectNotificationHub.mockResolvedValue(() => {});
    markNotificationRead.mockResolvedValue({});

    render(<MemoryRouter><NotificationBell /><LocationProbe /></MemoryRouter>);

    fireEvent.click(await screen.findByRole('button', { name: /Thông báo/i }));
    expect(screen.queryByRole('button', { name: /Báo cáo câu hỏi đã được xử lý/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('Báo cáo câu hỏi đã được xử lý'));

    expect(screen.getByTestId('location')).toHaveTextContent('/');
  });

  it('keeps notifications without links clickable as detail dialogs', async () => {
    getNotifications.mockResolvedValue({
      items: [{
        notificationId: 'notification-3',
        title: 'Thông báo khác',
        content: 'Nội dung thông báo khác.',
        link: null,
        isRead: false,
        createdTime: '2026-09-10T00:00:00Z'
      }]
    });
    connectNotificationHub.mockResolvedValue(() => {});
    markNotificationRead.mockResolvedValue({});

    render(<MemoryRouter><NotificationBell /><LocationProbe /></MemoryRouter>);

    fireEvent.click(await screen.findByRole('button', { name: /Thông báo/i }));
    fireEvent.click(screen.getByRole('button', { name: /Thông báo khác/i }));

    expect(await screen.findByText('Nội dung thông báo khác.')).toBeInTheDocument();
    expect(screen.getByTestId('location')).toHaveTextContent('/');
  });
});
