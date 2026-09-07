import { cleanup, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ExpertLayout from './ExpertLayout';
import { NavigationGuardProvider } from '../../contexts/NavigationGuardContext';

vi.mock('../../hooks/useCurrentUser', () => ({
  default: () => ({
    displayName: 'Nguyễn Văn Chuyên Gia',
    initials: 'CG',
    profile: {
      avatarUrl: null,
      roleName: 'Expert',
    },
    loading: false,
  }),
}));

vi.mock('../../services/notificationApi', () => ({
  getNotifications: vi.fn().mockResolvedValue({ notifications: [], unreadCount: 0 }),
  markNotificationRead: vi.fn().mockResolvedValue({}),
  connectNotificationHub: vi.fn().mockReturnValue(() => {}),
}));

afterEach(() => {
  cleanup();
});

describe('ExpertLayout topbar restoration & notification bell', () => {
  it('renders sidebar, children, dashboard topbar, and notification bell', () => {
    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <ExpertLayout>
            <div data-testid="expert-child-content">Nội dung trang chuyên gia</div>
          </ExpertLayout>
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    // Child content is rendered
    expect(screen.getByTestId('expert-child-content')).toBeInTheDocument();

    // Sidebar navigation is rendered
    expect(screen.getByText('MathInsight')).toBeInTheDocument();
    expect(screen.getByText('Chuyên gia nội dung')).toBeInTheDocument();

    // DashboardTopbar is rendered with app title and NotificationBell
    expect(screen.getByText('Hệ thống Quản lý Toán học')).toBeInTheDocument();
    expect(screen.getByLabelText(/Thông báo/i)).toBeInTheDocument();

    // Theme toggle in topbar is disabled for Expert
    expect(screen.queryByLabelText(/Giao diện/i)).not.toBeInTheDocument();
  });
});
