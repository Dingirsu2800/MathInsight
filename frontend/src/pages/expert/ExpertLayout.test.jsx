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

afterEach(() => {
  cleanup();
});

describe('ExpertLayout topbar removal & sidebar persistence', () => {
  it('renders sidebar and children without rendering the dashboard topbar', () => {
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

    // DashboardTopbar features (e.g. notifications, theme toggle in topbar) are NOT rendered
    expect(screen.queryByLabelText(/Thông báo/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Giao diện/i)).not.toBeInTheDocument();
  });
});
