import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SharedBlueprintExamDiscoveryPage from './SharedBlueprintExamDiscoveryPage';
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
    getSharedBlueprintExams: vi.fn(),
    resolveTestCode: vi.fn(),
    getBlueprintExamOptions: vi.fn(),
    generateBlueprintExam: vi.fn(),
    getTopicPracticeOptions: vi.fn(),
  },
}));

vi.mock('../../services/testingApi', () => ({
  startSession: vi.fn(),
}));

vi.mock('../../components/layout/StudentLayout', () => ({
  default: ({ children }) => <div data-testid="student-layout">{children}</div>,
}));

vi.mock('../../components/student/StartTestDialog', () => ({
  default: ({ isOpen, test }) => (isOpen ? <div data-testid="start-test-dialog">{test?.testName}</div> : null),
}));

vi.mock('../../components/student/AdaptiveBlueprintExamDialog', () => ({
  default: ({ isOpen }) => (isOpen ? <div data-testid="adaptive-blueprint-exam-dialog">Dialog Adaptive Exam</div> : null),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('SharedBlueprintExamDiscoveryPage - Redesign & Featured Adaptive Panel', () => {
  const sampleBlueprint = {
    blueprintId: 'BP-FEAT-1',
    blueprintName: 'Cấu trúc đề thi thử HK1 Toán 12',
    grade: 12,
    totalQuestions: 50,
    totalScore: 10,
    durationMinutes: 90,
    status: 'Approved',
    sectionCount: 3,
  };

  const fixedExams = [
    {
      testId: 'TEST-FIXED-1',
      testName: 'Đề cố định số 1',
      testCode: 'FIX01',
      generationType: 'Fixed',
      grade: 12,
      durationMinutes: 90,
      totalQuestions: 50,
      maxScore: 10,
      createdTime: '2026-08-15T00:00:00Z',
    },
  ];

  const randomExams = [
    {
      testId: 'TEST-RANDOM-1',
      testName: 'Đề theo cấu trúc số 1',
      testCode: 'RND01',
      generationType: 'Random',
      grade: 12,
      durationMinutes: 90,
      totalQuestions: 50,
      maxScore: 10,
      createdTime: '2026-08-15T00:00:00Z',
    },
  ];

  beforeEach(() => {
    testGeneratorApi.getBlueprintExamOptions.mockResolvedValue({
      data: { items: [sampleBlueprint], totalCount: 1, pageIndex: 1, pageSize: 1 },
    });
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValue({
      data: { items: fixedExams, totalCount: 1, totalPages: 1 },
    });
  });

  it('renders featured recommendation panel with first eligible blueprint and supporting copy', async () => {
    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(testGeneratorApi.getBlueprintExamOptions).toHaveBeenCalledWith(
      expect.objectContaining({ pageIndex: 1, pageSize: 1 })
    );

    const featuredPanel = await screen.findByTestId('featured-recommendation-panel');
    expect(featuredPanel).toBeInTheDocument();
    expect(featuredPanel).toHaveTextContent('Cấu trúc đề thi thử HK1 Toán 12');
    expect(featuredPanel).toHaveTextContent(/3 phần/i);
    expect(featuredPanel).toHaveTextContent(/50 câu/i);
    expect(featuredPanel).toHaveTextContent(/90 phút/i);
    expect(featuredPanel).toHaveTextContent(/10 điểm/i);
    expect(featuredPanel).toHaveTextContent(
      'Độ khó câu hỏi được điều chỉnh dựa trên kết quả làm bài gần đây của em.'
    );
    expect(screen.getByRole('button', { name: /Tạo đề ngay/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Chọn cấu trúc khác/i })).toBeInTheDocument();
  });

  it('clicking "Chọn cấu trúc khác" opens the adaptive blueprint dialog', async () => {
    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const openOtherBtn = await screen.findByRole('button', { name: /Chọn cấu trúc khác/i });
    fireEvent.click(openOtherBtn);

    expect(await screen.findByTestId('adaptive-blueprint-exam-dialog')).toBeInTheDocument();
  });

  it('handles empty and error states in featured panel without breaking shared catalog', async () => {
    testGeneratorApi.getBlueprintExamOptions.mockRejectedValueOnce(new Error('Network error'));
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: { items: fixedExams, totalCount: 1, totalPages: 1 },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    // Featured panel shows error & retry
    expect(await screen.findByText('Không thể tải cấu trúc đề thi đề xuất.')).toBeInTheDocument();
    const retryBtn = screen.getByRole('button', { name: /Thử lại/i });
    expect(retryBtn).toBeInTheDocument();

    // Catalog still loads successfully!
    expect(await screen.findByText('Đề cố định số 1')).toBeInTheDocument();
  });

  it('generates test, retains testId, starts session and navigates on "Tạo đề ngay" click', async () => {
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'GEN-TEST-001' },
    });
    startSession.mockResolvedValue({
      sessionId: 'SESSION-001',
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const createBtn = await screen.findByRole('button', { name: /Tạo đề ngay/i });
    fireEvent.click(createBtn);

    await waitFor(() => {
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledWith('BP-FEAT-1');
      expect(startSession).toHaveBeenCalledWith('GEN-TEST-001');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-001');
    });
  });

  it('guarantees rapid double-clicks on featured panel trigger generateBlueprintExam at most once', async () => {
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'GEN-TEST-RAPID' },
    });
    startSession.mockResolvedValue({
      sessionId: 'SESSION-RAPID',
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const createBtn = await screen.findByRole('button', { name: /Tạo đề ngay/i });
    // Rapid double click
    fireEvent.click(createBtn);
    fireEvent.click(createBtn);

    await waitFor(() => {
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
    });
  });

  it('handles startSession failure, retains testId, and retries start without regenerating', async () => {
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'GEN-TEST-002' },
    });
    startSession.mockRejectedValueOnce(new Error('Start session failed'));

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const createBtn = await screen.findByRole('button', { name: /Tạo đề ngay/i });
    fireEvent.click(createBtn);

    // Retry button appears
    const retryBtn = await screen.findByRole('button', { name: /Thử bắt đầu lại/i });
    expect(retryBtn).toBeInTheDocument();
    expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);

    // Second click on retry succeeds startSession
    startSession.mockResolvedValueOnce({
      sessionId: 'SESSION-002',
    });

    fireEvent.click(retryBtn);

    await waitFor(() => {
      // MUST NOT call generateBlueprintExam again!
      expect(testGeneratorApi.generateBlueprintExam).toHaveBeenCalledTimes(1);
      expect(startSession).toHaveBeenLastCalledWith('GEN-TEST-002');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-002');
    });
  });

  it('handles TESTING_SESSION_ALREADY_IN_PROGRESS with resume session flow', async () => {
    testGeneratorApi.generateBlueprintExam.mockResolvedValue({
      data: { testId: 'GEN-TEST-003' },
    });
    const sessionInProgressError = {
      response: {
        data: {
          code: 'TESTING_SESSION_ALREADY_IN_PROGRESS',
          existingSessionId: 'SESSION-EXISTING-123',
        },
      },
    };
    startSession.mockRejectedValueOnce(sessionInProgressError);

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const createBtn = await screen.findByRole('button', { name: /Tạo đề ngay/i });
    fireEvent.click(createBtn);

    const resumeBtn = await screen.findByRole('button', { name: /Tiếp tục bài đang làm/i });
    expect(resumeBtn).toBeInTheDocument();
    expect(screen.getByText(/Bạn đang có một phiên làm bài chưa hoàn thành/i)).toBeInTheDocument();

    fireEvent.click(resumeBtn);
    expect(mockNavigate).toHaveBeenCalledWith('/student/test/SESSION-EXISTING-123');
  });

  it('preserves Fixed and Random catalog separation and tab switching', async () => {
    testGeneratorApi.getSharedBlueprintExams
      .mockResolvedValueOnce({
        data: { items: fixedExams, totalCount: 1, totalPages: 1 },
      })
      .mockResolvedValueOnce({
        data: { items: randomExams, totalCount: 1, totalPages: 1 },
      });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(await screen.findByText('Đề cố định số 1')).toBeInTheDocument();

    const randomTab = screen.getByRole('tab', { name: /Đề theo cấu trúc/i });
    fireEvent.click(randomTab);

    await waitFor(() => {
      expect(testGeneratorApi.getSharedBlueprintExams).toHaveBeenLastCalledWith(
        expect.objectContaining({
          pageIndex: 1,
          generationType: 'Random',
        })
      );
    });

    expect(await screen.findByText('Đề theo cấu trúc số 1')).toBeInTheDocument();
    expect(screen.queryByText('Đề cố định số 1')).not.toBeInTheDocument();
  });

  it('displays correct empty state specific to Fixed and Random catalog tabs', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: { items: [], totalCount: 0, totalPages: 1 },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Chưa có đề cố định phù hợp với khối lớp của bạn.')
    ).toBeInTheDocument();

    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: { items: [], totalCount: 0, totalPages: 1 },
    });

    const randomTab = screen.getByRole('tab', { name: /Đề theo cấu trúc/i });
    fireEvent.click(randomTab);

    expect(
      await screen.findByText('Chưa có đề theo cấu trúc phù hợp với khối lớp của bạn.')
    ).toBeInTheDocument();
  });

  it('isolates error state to active tab and recovers on tab switch', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockRejectedValueOnce(new Error('Server error'));

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Không thể tải danh sách bài thi. Vui lòng thử lại sau.')
    ).toBeInTheDocument();

    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: { items: randomExams, totalCount: 1, totalPages: 1 },
    });

    const randomTab = screen.getByRole('tab', { name: /Đề theo cấu trúc/i });
    fireEvent.click(randomTab);

    expect(await screen.findByText('Đề theo cấu trúc số 1')).toBeInTheDocument();
    expect(
      screen.queryByText('Không thể tải danh sách bài thi. Vui lòng thử lại sau.')
    ).not.toBeInTheDocument();
  });

  it('filters out mismatched generationType items from the shared catalog', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: {
        items: [
          ...fixedExams,
          {
            testId: 'TEST-MISMATCHED',
            testName: 'Đề lẫn loại',
            testCode: 'MIS01',
            generationType: 'Random',
            grade: 12,
            durationMinutes: 60,
            totalQuestions: 40,
            maxScore: 10,
          },
        ],
        totalCount: 2,
        totalPages: 1,
      },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(await screen.findByText('Đề cố định số 1')).toBeInTheDocument();
    expect(screen.queryByText('Đề lẫn loại')).not.toBeInTheDocument();
  });

  it('opens compact TestCode dialog beside catalog, submits via button and form submit (Enter)', async () => {
    testGeneratorApi.resolveTestCode.mockResolvedValue({
      data: { testId: 'RESOLVED-1', testName: 'Đề thi tra mã test' },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const openCodeBtn = await screen.findByRole('button', { name: /Nhập mã đề/i });
    fireEvent.click(openCodeBtn);

    expect(screen.getByText('Nhập mã đề để tìm bài thi')).toBeInTheDocument();
    const input = screen.getByPlaceholderText(/MATH7K2P/i);

    // Enter code and submit form (Enter key interaction)
    fireEvent.change(input, { target: { value: 'RESOLVED-1' } });
    const form = input.closest('form');
    fireEvent.submit(form);

    await waitFor(() => {
      expect(testGeneratorApi.resolveTestCode).toHaveBeenCalledWith('RESOLVED-1');
    });

    expect(await screen.findByTestId('start-test-dialog')).toHaveTextContent('Đề thi tra mã test');
  });
});
