import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AdaptiveBlueprintExamDialog from './AdaptiveBlueprintExamDialog';
import { testGeneratorApi } from '../../services/testGeneratorApi';
import { startSession } from '../../services/testingApi';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getBlueprintExamOptions: vi.fn(),
    generateBlueprintExam: vi.fn(),
  },
}));

vi.mock('../../services/testingApi', () => ({
  startSession: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('AdaptiveBlueprintExamDialog - Checkpoint 6B Student UI', () => {
  const sampleOptions = [
    {
      blueprintId: 'BP-12-01',
      blueprintName: 'Cấu trúc đề ôn tập HK1 Toán 12',
      grade: 12,
      totalQuestions: 50,
      totalScore: 10,
      durationMinutes: 90,
      status: 'Active',
      sectionCount: 3,
    },
    {
      blueprintId: 'BP-12-02',
      blueprintName: 'Đề thử nghiệm Tốt nghiệp THPT',
      grade: 12,
      totalQuestions: 40,
      totalScore: 10,
      durationMinutes: 60,
      status: 'Approved',
      sectionCount: 2,
    },
  ];

  it('lazily fetches blueprint options only when dialog is opened', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: sampleOptions });

    const { rerender } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={false} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(testGeneratorApi.getBlueprintExamOptions).not.toHaveBeenCalled();

    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(testGeneratorApi.getBlueprintExamOptions).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();
    expect(screen.getByText('Đề thử nghiệm Tốt nghiệp THPT')).toBeInTheDocument();
  });

  it('renders blueprint metadata and natural Vietnamese explanation without forbidden terms', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: sampleOptions });

    const { container } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    // Check metadata display
    expect(screen.getByText(/3 phần/i)).toBeInTheDocument();
    expect(screen.getByText(/50 câu/i)).toBeInTheDocument();
    expect(screen.getByText(/90 phút/i)).toBeInTheDocument();
    expect(screen.getAllByText(/10 điểm/i).length).toBeGreaterThanOrEqual(1);

    // Check explanation copy
    expect(
      screen.getByText(/Cấu trúc đề thi.*được giữ nguyên/i)
    ).toBeInTheDocument();
    expect(
      screen.getByText(/kết quả học tập gần đây/i)
    ).toBeInTheDocument();

    // Verify forbidden technical terms are NOT in the UI
    const renderedText = container.textContent;
    const forbiddenTerms = ['WeakTag', 'Ptag', 'adaptive', 'baseline', 'recommender', 'ma trận'];
    forbiddenTerms.forEach((term) => {
      expect(renderedText.toLowerCase()).not.toContain(term.toLowerCase());
    });
  });

  it('generates test once and immediately starts session upon clicking "Tạo và bắt đầu"', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: sampleOptions });
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'TEST-GEN-001' },
    });
    startSession.mockResolvedValue({
      sessionId: 'SESSION-001',
    });

    const handleClose = vi.fn();
    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={handleClose} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledWith('BP-12-01');
      expect(startSession).toHaveBeenCalledTimes(1);
      expect(startSession).toHaveBeenCalledWith('TEST-GEN-001');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-001');
      expect(handleClose).toHaveBeenCalled();
    });
  });

  it('retains TestID on startSession failure and allows retry without regenerating', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: sampleOptions });
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'TEST-GEN-002' },
    });
    // First startSession fails
    startSession.mockRejectedValueOnce(new Error('Network failure on session start'));

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    fireEvent.click(submitBtn);

    // After failure, error banner and retry button appear
    const retryBtn = await screen.findByRole('button', { name: /Thử bắt đầu lại/i });
    expect(retryBtn).toBeInTheDocument();
    expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
    expect(startSession).toHaveBeenCalledTimes(1);

    // Next startSession succeeds
    startSession.mockResolvedValueOnce({
      sessionId: 'SESSION-RETRY-002',
    });

    fireEvent.click(retryBtn);

    await waitFor(() => {
      // Must NOT regenerate! generateBlueprintExam remains called only 1 time
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
      // startSession called second time with the same TestID
      expect(startSession).toHaveBeenCalledTimes(2);
      expect(startSession).toHaveBeenLastCalledWith('TEST-GEN-002');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-RETRY-002');
    });
  });

  it('retains TestID when the dialog is closed and reopened after start failure', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: sampleOptions });
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'TEST-GEN-CLOSE-REOPEN' },
    });
    startSession.mockRejectedValueOnce(new Error('Network failure on session start'));

    const onClose = vi.fn();
    const { rerender } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={onClose} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Tạo và bắt đầu/i }));
    await screen.findByRole('button', { name: /Thử bắt đầu lại/i });

    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={false} onClose={onClose} />
      </BrowserRouter>
    );
    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={onClose} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();
    startSession.mockResolvedValueOnce({ sessionId: 'SESSION-CLOSE-REOPEN' });
    fireEvent.click(screen.getByRole('button', { name: /Tạo và bắt đầu/i }));

    await waitFor(() => {
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
      expect(startSession).toHaveBeenCalledTimes(2);
      expect(startSession).toHaveBeenLastCalledWith('TEST-GEN-CLOSE-REOPEN');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-CLOSE-REOPEN');
    });
  });

  it('renders empty state when no eligible blueprints are returned', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({ data: [] });

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(
      await screen.findByText(/Chưa có cấu trúc đề thi nào phù hợp với khối lớp của bạn/i)
    ).toBeInTheDocument();

    // Final action button should be disabled when no options exist
    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    expect(submitBtn).toBeDisabled();
  });

  it('renders error state and retry button when fetching blueprint options fails', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockRejectedValueOnce(new Error('Fetch options failed'));

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(
      await screen.findByText(/Không thể tải danh sách cấu trúc đề thi/i)
    ).toBeInTheDocument();

    testGeneratorApi.getBlueprintExamOptions.mockResolvedValueOnce({ data: sampleOptions });
    const reloadBtn = screen.getByRole('button', { name: /Thử lại/i });
    fireEvent.click(reloadBtn);

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();
  });
});
