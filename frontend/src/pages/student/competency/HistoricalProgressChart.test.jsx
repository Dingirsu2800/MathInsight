import { cleanup, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import HistoricalProgressChart from './HistoricalProgressChart';
import { getSessionHistory } from '../../../services/gradingApi';

vi.mock('../../../services/gradingApi', () => ({
  getSessionHistory: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

function renderWithRouter(component) {
  return render(<BrowserRouter>{component}</BrowserRouter>);
}

describe('HistoricalProgressChart', () => {
  it('renders empty state when no sessions exist in the last 7 days', async () => {
    getSessionHistory.mockResolvedValue({ items: [] });

    renderWithRouter(<HistoricalProgressChart />);

    expect(await screen.findByText('Chưa có bài kiểm tra nào trong 1 tuần qua')).toBeVisible();
    expect(screen.getByText('Điểm các bài kiểm tra trong 1 tuần qua (7 ngày gần nhất)')).toBeVisible();
  });

  it('filters out sessions older than 7 days and shows sessions within 1 week', async () => {
    const now = new Date();
    const twoDaysAgo = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000).toISOString();
    const tenDaysAgo = new Date(now.getTime() - 10 * 24 * 60 * 60 * 1000).toISOString();

    getSessionHistory.mockResolvedValue({
      items: [
        {
          sessionId: 'session-old',
          testName: 'Bài kiểm tra cũ',
          testFormat: 'Exam',
          score: 4.2,
          numCorrect: 10,
          totalQuestion: 20,
          submittedAt: tenDaysAgo,
        },
        {
          sessionId: 'session-recent',
          testName: 'Bài kiểm tra gần đây',
          testFormat: 'Exam',
          score: 8.5,
          numCorrect: 17,
          totalQuestion: 20,
          submittedAt: twoDaysAgo,
        },
      ],
    });

    renderWithRouter(<HistoricalProgressChart />);

    expect(await screen.findByText('8.5')).toBeVisible();
    expect(screen.queryByText('4.2')).not.toBeInTheDocument();
    expect(screen.getByText('Tổng số bài:')).toBeVisible();
    expect(screen.getByText('Điểm TB: 8.5/10')).toBeVisible();
  });

  it('displays error state when getSessionHistory fails', async () => {
    getSessionHistory.mockRejectedValue(new Error('Network error'));

    renderWithRouter(<HistoricalProgressChart />);

    expect(
      await screen.findByText('Không thể tải dữ liệu lịch sử bài kiểm tra. Vui lòng thử lại sau.')
    ).toBeVisible();
  });
});
