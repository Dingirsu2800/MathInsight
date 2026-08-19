import { cleanup, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import WeakTopicsCard from './WeakTopicsCard';
import { getWeakTags } from '../../../services/recommenderApi';
import { testGeneratorApi } from '../../../services/testGeneratorApi';

vi.mock('../../../services/recommenderApi', () => ({
  getWeakTags: vi.fn(),
}));

vi.mock('../../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getTopicPracticeOptions: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('WeakTopicsCard', () => {
  it('renders weak topics with score and topic details', async () => {
    getWeakTags.mockResolvedValue([
      {
        tagId: 101,
        tagName: 'Khảo sát hàm số',
        officialPoint: 4.5,
      },
    ]);
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValue({
      data: {
        topics: [
          {
            tagId: 101,
            recommendedDifficultyLevel: 2,
            canGenerate: true,
          },
        ],
      },
    });

    render(
      <BrowserRouter>
        <WeakTopicsCard />
      </BrowserRouter>
    );

    expect(await screen.findByText('Khảo sát hàm số')).toBeVisible();
    expect(screen.getByText('Điểm chủ đề')).toBeVisible();
    expect(screen.getByText('4.5/10')).toBeVisible();
    expect(screen.getByText('Cần cải thiện')).toBeVisible();
    expect(screen.getByText('Mức: Thông hiểu')).toBeVisible();
  });

  it('renders empty state when there are no weak topics', async () => {
    getWeakTags.mockResolvedValue([]);
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValue({
      data: { topics: [] },
    });

    render(
      <BrowserRouter>
        <WeakTopicsCard />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Tuyệt vời! Bạn chưa có chủ đề nào cần cải thiện.')
    ).toBeVisible();
  });

  it('renders error state when fetching fails', async () => {
    getWeakTags.mockRejectedValue(new Error('Network error'));
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValue({
      data: { topics: [] },
    });

    render(
      <BrowserRouter>
        <WeakTopicsCard />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Không thể tải dữ liệu. Vui lòng thử lại sau.')
    ).toBeVisible();
  });
});
