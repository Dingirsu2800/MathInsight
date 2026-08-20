import { cleanup, render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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

describe('AdaptiveBlueprintExamDialog - Task 8 Scaled Discovery & Pagination', () => {
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

  it('lazily fetches blueprint options with default pageIndex=1&pageSize=20 only when opened', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
    });

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

    expect(testGeneratorApi.getBlueprintExamOptions).toHaveBeenCalledWith(
      expect.objectContaining({ pageIndex: 1, pageSize: 20 }),
      expect.anything()
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();
    expect(screen.getByText('Đề thử nghiệm Tốt nghiệp THPT')).toBeInTheDocument();
  });

  it('debounces search input by 300ms, resets pageIndex to 1, and requests search term', async () => {
    testGeneratorApi.getBlueprintExamOptions
      .mockResolvedValueOnce({
        data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
      })
      .mockResolvedValueOnce({
        data: {
          items: [sampleOptions[0]],
          totalCount: 1,
          pageIndex: 1,
          pageSize: 20,
        },
      });

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    const searchInput = screen.getByPlaceholderText(/Tìm kiếm theo tên cấu trúc đề/i);

    // Type in search
    fireEvent.change(searchInput, { target: { value: 'HK1' } });

    // Fast-forward debounce timer
    await waitFor(() => {
      expect(testGeneratorApi.getBlueprintExamOptions).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: 'HK1', pageIndex: 1, pageSize: 20 }),
        expect.anything()
      );
    });
  });

  it('prevents a second generation request when search debounce fires while generation is unresolved', async () => {
    vi.useFakeTimers();
    try {
      testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
        data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
      });

      let resolveGenerate;
      testGeneratorApi.generateBlueprintExam.mockImplementation(
        () => new Promise((resolve) => { resolveGenerate = resolve; })
      );

      render(
        <BrowserRouter>
          <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
        </BrowserRouter>
      );

      await act(async () => {
        vi.advanceTimersByTime(50);
      });

      // 1. Type a search term
      const searchInput = screen.getByPlaceholderText(/Tìm kiếm theo tên cấu trúc đề/i);
      fireEvent.change(searchInput, { target: { value: 'Toán 12' } });

      // 2. Select blueprint and click create BEFORE the 300ms debounce completes (e.g. at 100ms)
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const bp1Button = screen.getByRole('button', { name: /Cấu trúc đề ôn tập HK1 Toán 12/i });
      fireEvent.click(bp1Button);

      const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
      fireEvent.click(submitBtn);

      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);

      // 3. Let the debounce fire while generateBlueprintExam is unresolved
      await act(async () => {
        vi.advanceTimersByTime(300);
      });

      // 4. Click the action again
      fireEvent.click(submitBtn);

      // 5. Assert generateBlueprintExam was called exactly once
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);

      // Clean up the pending promise
      if (resolveGenerate) {
        await act(async () => {
          resolveGenerate({ data: { testId: 'TEST-DEBOUNCE-GUARD' } });
        });
      }
    } finally {
      vi.useRealTimers();
    }
  });

  it('guarantees rapid double-clicks trigger generateBlueprintExam at most once', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
    });
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'TEST-RAPID-001' },
    });
    startSession.mockResolvedValue({
      sessionId: 'SESSION-RAPID-001',
    });

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    const bp1Button = await screen.findByRole('button', { name: /Cấu trúc đề ôn tập HK1 Toán 12/i });
    fireEvent.click(bp1Button);

    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    // Rapid double click
    fireEvent.click(submitBtn);
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
    });
  });

  it('renders large catalog metadata (e.g. 2,000 items) and navigates pages', async () => {
    testGeneratorApi.getBlueprintExamOptions
      .mockResolvedValueOnce({
        data: { items: sampleOptions, totalCount: 2000, pageIndex: 1, pageSize: 20 },
      })
      .mockResolvedValueOnce({
        data: {
          items: [
            {
              blueprintId: 'BP-12-PAGE2',
              blueprintName: 'Cấu trúc đề trang 2',
              grade: 12,
              totalQuestions: 50,
              totalScore: 10,
              durationMinutes: 90,
              status: 'Active',
              sectionCount: 3,
            },
          ],
          totalCount: 2000,
          pageIndex: 2,
          pageSize: 20,
        },
      });

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText(/Tổng cộng: 2000 cấu trúc đề/i)).toBeInTheDocument();
    expect(screen.getByText(/\(Trang 1\/100\)/i)).toBeInTheDocument();

    // Previous button should be disabled on first page
    const prevBtn = screen.getByRole('button', { name: /Trước/i });
    expect(prevBtn).toBeDisabled();

    // Next button should be enabled
    const nextBtn = screen.getByRole('button', { name: /Tiếp/i });
    expect(nextBtn).not.toBeDisabled();

    fireEvent.click(nextBtn);

    await waitFor(() => {
      expect(testGeneratorApi.getBlueprintExamOptions).toHaveBeenLastCalledWith(
        expect.objectContaining({ pageIndex: 2, pageSize: 20 }),
        expect.anything()
      );
    });

    expect(await screen.findByText('Cấu trúc đề trang 2')).toBeInTheDocument();
    expect(screen.getByText(/\(Trang 2\/100\)/i)).toBeInTheDocument();
  });

  it('ignores aborted/canceled requests without setting error banner', async () => {
    const canceledError = new Error('canceled');
    canceledError.name = 'CanceledError';
    canceledError.code = 'ERR_CANCELED';

    testGeneratorApi.getBlueprintExamOptions.mockRejectedValueOnce(canceledError);

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    // Should NOT show any error alert
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText(/Không thể tải danh sách cấu trúc đề thi/i)).not.toBeInTheDocument();
  });

  it('retains selected option across rerenders when it remains in result', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
    });

    const { rerender } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    // Click to select BP-12-02
    const bp2Button = screen.getByRole('button', { name: /Đề thử nghiệm Tốt nghiệp THPT/i });
    fireEvent.click(bp2Button);

    // Selected blueprint details appear
    expect(screen.getByText('Quy định đề thi theo năng lực')).toBeInTheDocument();

    // Rerender with same options
    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    // Selection should still be retained
    expect(screen.getByText('Quy định đề thi theo năng lực')).toBeInTheDocument();
  });

  it('preserves generated TestID across dialog close/reopen and allows retry without regenerating', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
    });
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'TEST-GEN-RETAINED-01' },
    });
    // First startSession fails
    startSession.mockRejectedValueOnce(new Error('Session start failed'));

    const { rerender } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    // Select blueprint
    const bp1Button = screen.getByRole('button', { name: /Cấu trúc đề ôn tập HK1 Toán 12/i });
    fireEvent.click(bp1Button);

    // Generate and start
    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    fireEvent.click(submitBtn);

    // Retry button appears
    const retryBtn = await screen.findByRole('button', { name: /Thử bắt đầu lại/i });
    expect(retryBtn).toBeInTheDocument();
    expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);

    // Close dialog
    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={false} onClose={vi.fn()} />
      </BrowserRouter>
    );

    // Reopen dialog
    rerender(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    // Because TestID is preserved, clicking retry calls startSession with retained TestID without calling generate again
    startSession.mockResolvedValueOnce({ sessionId: 'SESSION-SUCCESS-01' });

    const retryBtnAfterReopen = await screen.findByRole('button', { name: /Thử bắt đầu lại/i });
    fireEvent.click(retryBtnAfterReopen);

    await waitFor(() => {
      // Must NOT regenerate!
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
      // startSession called with retained testId
      expect(startSession).toHaveBeenLastCalledWith('TEST-GEN-RETAINED-01');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-SUCCESS-01');
    });
  });

  it('renders blueprint metadata and natural Vietnamese explanation without forbidden terms', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: sampleOptions, totalCount: 2, pageIndex: 1, pageSize: 20 },
    });

    const { container } = render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề ôn tập HK1 Toán 12')).toBeInTheDocument();

    // Select blueprint
    const bp1Button = screen.getByRole('button', { name: /Cấu trúc đề ôn tập HK1 Toán 12/i });
    fireEvent.click(bp1Button);

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

  it('renders empty state when no eligible blueprints match', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: [], totalCount: 0, pageIndex: 1, pageSize: 20 },
    });

    render(
      <BrowserRouter>
        <AdaptiveBlueprintExamDialog isOpen={true} onClose={vi.fn()} />
      </BrowserRouter>
    );

    expect(
      await screen.findByText(/Chưa có cấu trúc đề thi nào phù hợp với khối lớp của bạn/i)
    ).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /Tạo và bắt đầu/i });
    expect(submitBtn).toBeDisabled();
  });
});
