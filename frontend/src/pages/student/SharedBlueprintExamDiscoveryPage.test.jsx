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

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('SharedBlueprintExamDiscoveryPage - Student Exam Catalog Separation', () => {
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

  it('switches to Random tab, requests generationType=Random, and resets pageIndex to 1', async () => {
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

    const randomTab = screen.getByRole('tab', { name: /Đề tạo ngẫu nhiên/i });
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

    const randomTab = screen.getByRole('tab', { name: /Đề tạo ngẫu nhiên/i });
    fireEvent.click(randomTab);

    expect(
      await screen.findByText('Chưa có đề tạo ngẫu nhiên phù hợp với khối lớp của bạn.')
    ).toBeInTheDocument();
  });

  it('keeps test-code resolution input available above catalog tabs', async () => {
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

    expect(screen.getByRole('heading', { name: 'Nhập mã đề' })).toBeInTheDocument();
    const input = screen.getByPlaceholderText(/MATH7K2P/i);
    fireEvent.change(input, { target: { value: 'RESOLVED-1' } });

    const submitBtn = screen.getByRole('button', { name: /Tìm đề/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.resolveTestCode).toHaveBeenCalledWith('RESOLVED-1');
    });

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

    const randomTab = screen.getByRole('tab', { name: /Đề tạo ngẫu nhiên/i });
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
