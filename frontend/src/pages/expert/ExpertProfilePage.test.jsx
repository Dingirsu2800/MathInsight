import { cleanup, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ExpertProfilePage from './ExpertProfilePage';
import client from '../../services/questionBankApiClient';
import { NavigationGuardProvider } from '../../contexts/NavigationGuardContext';

vi.mock('../../services/questionBankApiClient', () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
  },
}));

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
  vi.resetAllMocks();
});

describe('ExpertProfilePage full profile form integration', () => {
  it('renders page header and loads complete user profile details with edit mode', async () => {
    client.get.mockResolvedValue({
      data: {
        accountId: 'acc-123',
        username: 'chuyengia01',
        email: 'chuyengia@mathinsight.vn',
        firstName: 'Chuyên Gia',
        lastName: 'Nguyễn Văn',
        phoneNumber: '0987654321',
        dateOfBirth: '1990-05-15',
        roleName: 'Expert',
        expert: {
          specialty: 'Hình học không gian',
        },
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <ExpertProfilePage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(screen.getByRole('heading', { level: 1, name: /Hồ sơ cá nhân/i })).toBeInTheDocument();

    // Verify user profile fields loaded
    expect(await screen.findByText('chuyengia01')).toBeInTheDocument();
    expect(screen.getByText('chuyengia@mathinsight.vn')).toBeInTheDocument();
    expect(screen.getByText('0987654321')).toBeInTheDocument();
    expect(screen.getByText('15/05/1990')).toBeInTheDocument();
    expect(screen.getByText('Hình học không gian')).toBeInTheDocument();

    // Verify edit button is available
    expect(screen.getByRole('button', { name: /Chỉnh sửa/i })).toBeInTheDocument();

    // Verify change password section is available
    expect(screen.getByRole('heading', { level: 2, name: /Đổi mật khẩu/i })).toBeInTheDocument();
  });
});
