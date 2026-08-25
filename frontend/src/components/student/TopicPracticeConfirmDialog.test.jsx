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

  it('does not display stale-difficulty warning when refreshed in recommended mode', () => {
    const { rerender } = render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={sampleTopic}
        onConfirm={vi.fn()}
        submitting={false}
        errorMessage=""
      />
    );

    const updatedTopic = {
      ...sampleTopic,
      difficultyAvailability: sampleTopic.difficultyAvailability.map((d) => ({
        ...d,
        canGenerate: false,
        availableQuestionCount: 0,
      })),
    };

    rerender(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={updatedTopic}
        onConfirm={vi.fn()}
        submitting={false}
        errorMessage=""
      />
    );

    expect(screen.queryByText(/Mức độ khó đã chọn không còn khả dụng/i)).not.toBeInTheDocument();
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

  it('in manual mode: clears stale selected difficulty on refresh, keeps dialog open, shows warning, and disables start', () => {
    const handleConfirm = vi.fn();
    const { rerender } = render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={sampleTopic}
        onConfirm={handleConfirm}
        submitting={false}
        errorMessage=""
      />
    );

    // Switch to manual mode and select DIFF-2
    const manualTab = screen.getByRole('button', { name: /Tự chọn độ khó/i });
    fireEvent.click(manualTab);

    const level2Btn = screen.getByRole('button', { name: /Mức 2: Thông hiểu/i });
    fireEvent.click(level2Btn);

    const submitBtn = screen.getByRole('button', { name: /Bắt đầu làm bài/i });
    expect(submitBtn).not.toBeDisabled();

    // Simulate options refresh where DIFF-2 is no longer canGenerate
    const refreshedTopic = {
      ...sampleTopic,
      difficultyAvailability: [
        { ...sampleTopic.difficultyAvailability[0] }, // DIFF-1 still canGenerate
        { ...sampleTopic.difficultyAvailability[1], canGenerate: false, availableQuestionCount: 3 }, // DIFF-2 unavailable!
        { ...sampleTopic.difficultyAvailability[2] },
        { ...sampleTopic.difficultyAvailability[3] },
      ],
    };

    rerender(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={refreshedTopic}
        onConfirm={handleConfirm}
        submitting={false}
        errorMessage=""
      />
    );

    // Dialog remains open
    expect(screen.getByText('Tạo bài luyện tập chủ đề')).toBeInTheDocument();

    // Inline warning is shown
    expect(
      screen.getByText('Mức độ khó đã chọn không còn khả dụng. Danh sách các mức độ khó đã được cập nhật.')
    ).toBeInTheDocument();

    // Start button is disabled because selection was cleared
    expect(screen.getByRole('button', { name: /Bắt đầu làm bài/i })).toBeDisabled();

    // Select DIFF-1 (which is valid)
    const level1Btn = screen.getByRole('button', { name: /Mức 1: Nhận biết/i });
    fireEvent.click(level1Btn);

    // Warning is cleared and start button is enabled
    expect(screen.queryByText('Mức độ khó đã chọn không còn khả dụng. Danh sách các mức độ khó đã được cập nhật.')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Bắt đầu làm bài/i })).not.toBeDisabled();
  });

  it('renders qualified mastery copy when mastery data exists, and baseline copy otherwise', () => {
    const masteryTopic = {
      ...sampleTopic,
      isWeakRecommended: true,
      weakTagName: 'Số phức',
      recommendedDifficultyLevel: 2,
      officialPoint: 4.5,
    };

    const { rerender } = render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={masteryTopic}
        onConfirm={vi.fn()}
        submitting={false}
        errorMessage=""
      />
    );

    // Auto mode label
    expect(screen.getByText('Mức phù hợp')).toBeInTheDocument();
    expect(screen.getByText('Khuyến nghị')).toBeInTheDocument();

    // Qualified mastery copy
    expect(
      screen.getByText('Hệ thống phân bổ câu hỏi dựa trên kết quả gần đây của em ở chủ đề này.')
    ).toBeInTheDocument();

    // Re-render with baseline topic (no prior mastery results)
    const baselineTopic = {
      ...sampleTopic,
      isWeakRecommended: false,
      recommendedDifficultyLevel: null,
      officialPoint: null,
    };

    rerender(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={baselineTopic}
        onConfirm={vi.fn()}
        submitting={false}
        errorMessage=""
      />
    );

    // Baseline copy
    expect(
      screen.getByText('Em chưa có đủ kết quả ở chủ đề này nên hệ thống sử dụng mức độ tổng hợp.')
    ).toBeInTheDocument();
  });

  it('contains no internal technical jargon in UI text', () => {
    const { container } = render(
      <TopicPracticeConfirmDialog
        isOpen={true}
        onClose={vi.fn()}
        topic={{
          ...sampleTopic,
          isWeakRecommended: true,
          weakTagName: 'Số phức',
          recommendedDifficultyLevel: 2,
          officialPoint: 4.5,
        }}
        onConfirm={vi.fn()}
        submitting={false}
        errorMessage=""
      />
    );

    const renderedText = container.textContent;
    const forbiddenTerms = ['WeakTag', 'OfficialPoint', 'EvidenceCount', 'adaptive', 'baseline', 'recommender'];
    forbiddenTerms.forEach((term) => {
      expect(renderedText).not.toContain(term);
    });
  });
});
