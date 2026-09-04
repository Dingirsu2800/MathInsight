import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import TopicMasteryGrid from './TopicMasteryGrid';
import { getAllTagsMastery } from '../../../services/recommenderApi';
import useCurrentUser from '../../../hooks/useCurrentUser';

vi.mock('../../../services/recommenderApi', () => ({
  getAllTagsMastery: vi.fn(),
}));

vi.mock('../../../hooks/useCurrentUser', () => ({
  default: vi.fn(),
}));

const mockTopics = [
  {
    tagId: 't1',
    tagName: 'Lớp 10 - Mệnh đề, tập hợp',
    grade: 10,
    officialPoint: 3.5,
    numberDone: 3,
    masteryStatus: 'Learning',
  },
  {
    tagId: 't2',
    tagName: 'Lớp 11 - Lượng giác',
    grade: 11,
    officialPoint: 8.0,
    numberDone: 5,
    masteryStatus: 'Mastered',
  },
  {
    tagId: 't3',
    tagName: 'Lớp 12 - Ứng dụng đạo hàm',
    grade: 12,
    officialPoint: 7.2,
    numberDone: 4,
    masteryStatus: 'Mastered',
  },
  {
    tagId: 't4',
    tagName: 'Lớp 12 - Khối đa diện',
    grade: 12,
    officialPoint: 4.0,
    numberDone: 2,
    masteryStatus: 'Learning',
  },
];

describe('TopicMasteryGrid', () => {
  beforeEach(() => {
    getAllTagsMastery.mockResolvedValue(mockTopics);
    useCurrentUser.mockReturnValue({
      profile: {
        student: {
          currentGrade: 12,
        },
      },
      loading: false,
    });
  });

  afterEach(() => {
    cleanup();
    vi.resetAllMocks();
  });

  function renderGrid() {
    return render(
      <BrowserRouter>
        <TopicMasteryGrid />
      </BrowserRouter>
    );
  }

  it('filters topics by student currentGrade by default', async () => {
    renderGrid();

    // Default grade is 12, so only Grade 12 topics should be visible
    expect(await screen.findByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
    expect(screen.getByText('Lớp 12 - Khối đa diện')).toBeVisible();
    expect(screen.queryByText('Lớp 10 - Mệnh đề, tập hợp')).not.toBeInTheDocument();
    expect(screen.queryByText('Lớp 11 - Lượng giác')).not.toBeInTheDocument();

    // Counter badge shows 2 topics
    expect(screen.getByText('2 chủ đề')).toBeVisible();
  });

  it('toggles between only current grade and all topics', async () => {
    renderGrid();

    expect(await screen.findByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();

    // Click "Hiển thị toàn bộ"
    const showAllBtn = screen.getByRole('button', { name: /hiển thị toàn bộ/i });
    fireEvent.click(showAllBtn);

    // Now all 4 topics should be visible
    expect(screen.getByText('Lớp 10 - Mệnh đề, tập hợp')).toBeVisible();
    expect(screen.getByText('Lớp 11 - Lượng giác')).toBeVisible();
    expect(screen.getByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
    expect(screen.getByText('Lớp 12 - Khối đa diện')).toBeVisible();
    expect(screen.getByText('4 chủ đề')).toBeVisible();

    // Click back to "Lớp 12"
    const gradeBtn = screen.getByRole('button', { name: /lớp 12/i });
    fireEvent.click(gradeBtn);

    expect(screen.queryByText('Lớp 10 - Mệnh đề, tập hợp')).not.toBeInTheDocument();
    expect(screen.getByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
    expect(screen.getByText('2 chủ đề')).toBeVisible();
  });

  it('defaults to showing all topics when student has no currentGrade set', async () => {
    useCurrentUser.mockReturnValue({
      profile: {
        student: {
          currentGrade: null,
        },
      },
      loading: false,
    });

    renderGrid();

    // All topics should be rendered because grade is unknown
    expect(await screen.findByText('Lớp 10 - Mệnh đề, tập hợp')).toBeVisible();
    expect(screen.getByText('Lớp 11 - Lượng giác')).toBeVisible();
    expect(screen.getByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
    expect(screen.getByText('4 chủ đề')).toBeVisible();

    // "Lớp hiện tại" button should be disabled
    const currentGradeBtn = screen.getByRole('button', { name: /lớp hiện tại/i });
    expect(currentGradeBtn).toBeDisabled();
  });

  it('shows empty filter state and allows resetting when current grade has no topics', async () => {
    useCurrentUser.mockReturnValue({
      profile: {
        student: {
          currentGrade: 10,
        },
      },
      loading: false,
    });

    // Mock only Grade 12 topics
    getAllTagsMastery.mockResolvedValue([
      {
        tagId: 't3',
        tagName: 'Lớp 12 - Ứng dụng đạo hàm',
        grade: 12,
        officialPoint: 7.2,
        numberDone: 4,
        masteryStatus: 'Mastered',
      },
    ]);

    renderGrid();

    // Should display empty state for Grade 10
    expect(await screen.findByText('Không có chủ đề nào thuộc Lớp 10')).toBeVisible();

    // Click reset button in empty state
    const resetBtn = screen.getByRole('button', { name: /hiển thị toàn bộ chủ đề/i });
    fireEvent.click(resetBtn);

    // Grade 12 topic is now visible
    expect(await screen.findByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
  });

  it('sorts topics by progress and score properly', async () => {
    renderGrid();

    // In Grade 12, default sort is 'progress': weak (<5) comes first
    expect(await screen.findByText('Lớp 12 - Ứng dụng đạo hàm')).toBeVisible();
    const cardsBefore = screen.getAllByRole('heading', { level: 4 });
    expect(cardsBefore[0]).toHaveTextContent('Lớp 12 - Khối đa diện'); // score 4.0 (weak)
    expect(cardsBefore[1]).toHaveTextContent('Lớp 12 - Ứng dụng đạo hàm'); // score 7.2

    // Switch to sort by score
    const scoreSortBtn = screen.getByRole('button', { name: /theo điểm số/i });
    fireEvent.click(scoreSortBtn);

    const cardsAfter = screen.getAllByRole('heading', { level: 4 });
    expect(cardsAfter[0]).toHaveTextContent('Lớp 12 - Ứng dụng đạo hàm'); // score 7.2
    expect(cardsAfter[1]).toHaveTextContent('Lớp 12 - Khối đa diện'); // score 4.0
  });
});
