import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import PracticeSetupPanel from './PracticeSetupPanel';
import { testGeneratorApi } from '../../services/testGeneratorApi';

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getTopicPracticeOptions: vi.fn(),
    generateTopicPractice: vi.fn(),
  },
}));

vi.mock('../../services/testingApi', () => ({
  startSession: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('PracticeSetupPanel - Grade Filtering & Stale Option Sync', () => {
  const sampleData = {
    grade: 12,
    topics: [
      {
        tagId: 'TOPIC-G12-1',
        tagName: 'Hàm số 12',
        grade: 12,
        parentTagName: 'Giải tích 12',
        canGenerate: true,
        availableQuestionCount: 15,
        difficultyAvailability: [],
      },
      {
        tagId: 'TOPIC-G11-1',
        tagName: 'Lượng giác 11',
        grade: 11,
        parentTagName: 'Lượng giác 11',
        canGenerate: true,
        availableQuestionCount: 12,
        difficultyAvailability: [],
      },
    ],
  };

  it('renders Grade filtering bar and filters topics when selecting a grade', async () => {
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValue({ data: sampleData });

    render(
      <BrowserRouter>
        <PracticeSetupPanel />
      </BrowserRouter>
    );

    // Default grade is 12 -> "Hàm số 12" should be visible, "Lượng giác 11" should not
    expect(await screen.findByText('Hàm số 12')).toBeInTheDocument();
    expect(screen.queryByText('Lượng giác 11')).not.toBeInTheDocument();

    // Click "Khối 11" filter button
    const grade11Btn = screen.getByRole('button', { name: /Khối 11/i });
    fireEvent.click(grade11Btn);

    // "Lượng giác 11" practice button should now be visible, "Hàm số 12" should not
    expect(await screen.findByRole('button', { name: /Luyện tập chủ đề Lượng giác 11/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Luyện tập chủ đề Hàm số 12/i })).not.toBeInTheDocument();

    // Click "Tất cả khối" filter button
    const allGradeBtn = screen.getByRole('button', { name: /Tất cả khối/i });
    fireEvent.click(allGradeBtn);

    expect(screen.getByRole('button', { name: /Luyện tập chủ đề Hàm số 12/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Luyện tập chủ đề Lượng giác 11/i })).toBeInTheDocument();
  });

  it('closes dialog and displays notice when stale topic becomes unavailable after refresh', async () => {
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValueOnce({ data: sampleData });

    render(
      <BrowserRouter>
        <PracticeSetupPanel />
      </BrowserRouter>
    );

    // Select "Hàm số 12" to open confirm dialog
    const practiceBtn = await screen.findByRole('button', { name: /Luyện tập chủ đề Hàm số 12/i });
    fireEvent.click(practiceBtn);

    expect(screen.getByText('Tạo bài luyện tập chủ đề')).toBeInTheDocument();

    // Mock API refresh where "Hàm số 12" now has canGenerate = false
    const staleData = {
      grade: 12,
      topics: [
        {
          ...sampleData.topics[0],
          canGenerate: false,
          availableQuestionCount: 5,
        },
      ],
    };

    // Trigger generate error that causes refresh
    testGeneratorApi.generateTopicPractice.mockRejectedValueOnce({
      response: { data: { code: 'TOPIC_PRACTICE_DIFFICULTY_UNAVAILABLE' } },
    });
    testGeneratorApi.getTopicPracticeOptions.mockResolvedValueOnce({ data: staleData });

    const submitBtn = screen.getByRole('button', { name: /Bắt đầu làm bài/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      // Dialog should be closed because topic is no longer valid
      expect(screen.queryByText('Tạo bài luyện tập chủ đề')).not.toBeInTheDocument();
      expect(screen.getByText('Chủ đề đã chọn hiện không còn đủ câu hỏi để luyện tập. Vui lòng chọn chủ đề khác.')).toBeInTheDocument();
    });
  });
});
