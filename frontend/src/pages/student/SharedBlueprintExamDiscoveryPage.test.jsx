import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import SharedBlueprintExamDiscoveryPage from './SharedBlueprintExamDiscoveryPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getSharedBlueprintExams: vi.fn(),
    resolveTestCode: vi.fn(),
    getTopicPracticeOptions: vi.fn(),
  },
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

describe('SharedBlueprintExamDiscoveryPage - Student Exam Catalog Separation & Adaptive Command', () => {
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
      testName: 'Đề ngẫu nhiên số 1',
      testCode: 'RND01',
      generationType: 'Random',
      grade: 12,
      durationMinutes: 90,
      totalQuestions: 50,
      maxScore: 10,
      createdTime: '2026-08-15T00:00:00Z',
    },
  ];

  it('renders command button "Tạo đề theo năng lực" with auto_awesome icon outside catalog tablist and opens dialog on click', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValue({
      data: { items: fixedExams, totalCount: 1, totalPages: 1 },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    const commandBtn = screen.getByRole('button', { name: /Tạo đề theo năng lực/i });
    expect(commandBtn).toBeInTheDocument();
    expect(commandBtn).toHaveTextContent('auto_awesome');

    // Verify catalog tablist does NOT contain the create command
    const tablist = screen.getByRole('tablist', { name: 'Kho đề thi' });
    expect(tablist).not.toContainElement(commandBtn);

    // Clicking command opens adaptive dialog
    fireEvent.click(commandBtn);
    expect(await screen.findByTestId('adaptive-blueprint-exam-dialog')).toBeInTheDocument();
  });

  it('requests generationType=Fixed by default on initial render', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValue({
      data: { items: fixedExams, totalCount: 1, totalPages: 1 },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    expect(testGeneratorApi.getSharedBlueprintExams).toHaveBeenCalledWith(
      expect.objectContaining({
        pageIndex: 1,
        generationType: 'Fixed',
      })
    );

    expect(await screen.findByText('Đề cố định số 1')).toBeInTheDocument();
  });

  it('switches to Random tab ("Đề theo cấu trúc"), requests generationType=Random, and resets pageIndex to 1', async () => {
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

    expect(await screen.findByText('Đề ngẫu nhiên số 1')).toBeInTheDocument();
    expect(screen.queryByText('Đề cố định số 1')).not.toBeInTheDocument();
  });

  it('displays correct empty state specific to the active catalog tab', async () => {
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

  it('opens and closes compact TestCode dialog, submits via Enter, and opens start dialog on resolve', async () => {
    testGeneratorApi.getSharedBlueprintExams.mockResolvedValue({
      data: { items: fixedExams, totalCount: 1, totalPages: 1 },
    });
    testGeneratorApi.resolveTestCode.mockResolvedValue({
      data: { testId: 'RESOLVED-1', testName: 'Đề thi nhập mã' },
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    // Initial state: TestCode dialog is closed
    expect(screen.queryByPlaceholderText(/MATH7K2P/i)).not.toBeInTheDocument();

    // Click "Nhập mã đề" command button
    const openCodeBtn = screen.getByRole('button', { name: /Nhập mã đề/i });
    fireEvent.click(openCodeBtn);

    // Dialog opens with natural copy
    expect(screen.getByText('Nhập mã đề để tìm bài thi')).toBeInTheDocument();
    const input = screen.getByPlaceholderText(/MATH7K2P/i);
    expect(input).toBeInTheDocument();

    // Test Cancel / Close
    const cancelBtn = screen.getByRole('button', { name: /Hủy/i });
    fireEvent.click(cancelBtn);
    expect(screen.queryByPlaceholderText(/MATH7K2P/i)).not.toBeInTheDocument();

    // Reopen dialog and type code
    fireEvent.click(openCodeBtn);
    const reopenedInput = screen.getByPlaceholderText(/MATH7K2P/i);
    fireEvent.change(reopenedInput, { target: { value: 'RESOLVED-1' } });

    // Submit via form Enter or "Tìm đề" button
    const submitBtn = screen.getByRole('button', { name: /Tìm đề/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.resolveTestCode).toHaveBeenCalledWith('RESOLVED-1');
    });

    // TestCode dialog closes and start-test-dialog opens
    expect(screen.queryByText('Nhập mã đề để tìm bài thi')).not.toBeInTheDocument();
    expect(await screen.findByTestId('start-test-dialog')).toHaveTextContent('Đề thi nhập mã');
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

    expect(await screen.findByText('Đề ngẫu nhiên số 1')).toBeInTheDocument();
    expect(
      screen.queryByText('Không thể tải danh sách bài thi. Vui lòng thử lại sau.')
    ).not.toBeInTheDocument();
  });

  it('guards against responses containing mismatched generationType', async () => {
    const mixedPayload = {
      items: [
        {
          testId: 'TEST-FIXED-1',
          testName: 'Đề cố định hợp lệ',
          generationType: 'Fixed',
          grade: 12,
          durationMinutes: 90,
          totalQuestions: 50,
          maxScore: 10,
        },
        {
          testId: 'TEST-RANDOM-ACCIDENTAL',
          testName: 'Đề ngẫu nhiên lọt lưới',
          generationType: 'Random',
          grade: 12,
          durationMinutes: 90,
          totalQuestions: 50,
          maxScore: 10,
        },
      ],
      totalCount: 2,
      totalPages: 1,
    };

    testGeneratorApi.getSharedBlueprintExams.mockResolvedValueOnce({
      data: mixedPayload,
    });

    render(
      <BrowserRouter>
        <SharedBlueprintExamDiscoveryPage />
      </BrowserRouter>
    );

    // Only "Đề cố định hợp lệ" should be rendered
    expect(await screen.findByText('Đề cố định hợp lệ')).toBeInTheDocument();
    expect(screen.queryByText('Đề ngẫu nhiên lọt lưới')).not.toBeInTheDocument();
  });
});
