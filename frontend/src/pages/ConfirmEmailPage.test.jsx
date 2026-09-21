import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ConfirmEmailPage from './ConfirmEmailPage';
import client from '../services/questionBankApiClient';

vi.mock('../services/questionBankApiClient', () => ({
  default: {
    post: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('ConfirmEmailPage', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  it('renders without confirming automatically', () => {
    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: 'Xác nhận email' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xác nhận email' })).toBeInTheDocument();
    expect(client.post).not.toHaveBeenCalled();
  });

  it('confirms with the URL token only after the button is clicked', async () => {
    client.post.mockResolvedValue({ data: { message: 'Email confirmed.' } });

    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận email' }));

    await waitFor(() => {
      expect(client.post).toHaveBeenCalledTimes(1);
    });
    expect(client.post).toHaveBeenCalledWith('/api/v1/auth/confirm-email', { token: 'abc' });
  });

  it('does not submit duplicate requests while confirmation is in progress', async () => {
    let resolveRequest;
    const request = new Promise((resolve) => {
      resolveRequest = resolve;
    });
    client.post.mockReturnValue(request);

    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    const button = screen.getByRole('button', { name: 'Xác nhận email' });
    fireEvent.click(button);
    fireEvent.click(button);

    expect(client.post).toHaveBeenCalledTimes(1);
    resolveRequest({ data: { message: 'Email confirmed.' } });
    expect(await screen.findByText('Xác nhận thành công!')).toBeInTheDocument();
  });

  it('shows the success UI after confirmation', async () => {
    client.post.mockResolvedValue({ data: { message: 'Email confirmed.' } });

    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận email' }));

    expect(await screen.findByText(/Tài khoản của bạn đã được kích hoạt\./)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Đăng nhập' })).toBeInTheDocument();
  });

  it('shows the existing expired-link message for HTTP 410', async () => {
    client.post.mockRejectedValue({ response: { status: 410 } });

    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận email' }));

    expect(await screen.findByText('Liên kết đã hết hạn. Vui lòng đăng ký lại.')).toBeInTheDocument();
  });

  it('does not consume the token when the page is refreshed', () => {
    const { unmount } = render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    unmount();
    render(
      <MemoryRouter initialEntries={['/confirm-email?token=abc']}>
        <ConfirmEmailPage />
      </MemoryRouter>,
    );

    expect(client.post).not.toHaveBeenCalled();
  });
});
