import { cleanup, render, screen, fireEvent } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import TopicPracticeConfirmDialog from './TopicPracticeConfirmDialog';

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('TopicPracticeConfirmDialog - Contract 6E Manual Difficulty', () => {
  const sampleTopic = {
    tagId: 'TOPIC-G12-COMPLEX',
    tagName: 'Số phức cơ bản',
    canGenerate: true,
    difficultyAvailability: [
      {
        difficultyId: 'DIFF-1',
        difficultyName: 'Nhận biết',
        levelValue: 1,
        availableQuestionCount: 14,
        canGenerate: true,
      },
      {
        difficultyId: 'DIFF-2',
        difficultyName: 'Thông hiểu',
        levelValue: 2,
        availableQuestionCount: 12,
        canGenerate: true,
      },
      {
        difficultyId: 'DIFF-3',
        difficultyName: 'Vận dụng',
        levelValue: 3,
        availableQuestionCount: 8,
        canGenerate: false,
      },
      {
        difficultyId: 'DIFF-4',
        difficultyName: 'Vận dụng cao',
        levelValue: 4,
        availableQuestionCount: 0,
        canGenerate: false,
      },
    ],
  };

  it('submits payload without difficultyId in recommended mode', () => {
    const handleConfirm = vi.fn();
    render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={sampleTopic}
        onConfirm={handleConfirm}
        submitting={false}
        errorMessage=""
      />
    );

    const submitBtn = screen.getByRole('button', { name: /Bắt đầu làm bài/i });
    fireEvent.click(submitBtn);

    expect(handleConfirm).toHaveBeenCalledTimes(1);
    expect(handleConfirm).toHaveBeenCalledWith({ tagId: 'TOPIC-G12-COMPLEX' });
  });

  it('switches to manual mode, disables unavailable levels, and submits with selected difficultyId', () => {
    const handleConfirm = vi.fn();
    render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={sampleTopic}
        onConfirm={handleConfirm}
        submitting={false}
        errorMessage=""
      />
    );

    // Switch tab to manual difficulty mode
    const manualTab = screen.getByRole('button', { name: /Tự chọn độ khó/i });
    fireEvent.click(manualTab);

    // Level 3 (DIFF-3) has only 8 questions, so its button must be disabled
    const level3Btn = screen.getByRole('button', { name: /Mức 3: Vận dụng/i });
    expect(level3Btn).toBeDisabled();
    expect(screen.getByText(/Chưa đủ 10 câu \(8\/10\)/i)).toBeInTheDocument();

    // Level 2 (DIFF-2) has 12 questions, select it
    const level2Btn = screen.getByRole('button', { name: /Mức 2: Thông hiểu/i });
    expect(level2Btn).not.toBeDisabled();
    fireEvent.click(level2Btn);

    // Click confirm button
    const submitBtn = screen.getByRole('button', { name: /Bắt đầu làm bài/i });
    fireEvent.click(submitBtn);

    expect(handleConfirm).toHaveBeenCalledTimes(1);
    expect(handleConfirm).toHaveBeenCalledWith({
      tagId: 'TOPIC-G12-COMPLEX',
      difficultyId: 'DIFF-2',
    });
  });
});
