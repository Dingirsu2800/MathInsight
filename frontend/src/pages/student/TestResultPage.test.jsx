import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import TestResultPage from './TestResultPage';
import { getSessionResult, reportSessionQuestion } from '../../services/gradingApi';

vi.mock('../../services/gradingApi', () => ({
  getSessionResult: vi.fn(),
  reportSessionQuestion: vi.fn(),
}));

vi.mock('../../components/layout/StudentLayout', () => ({
  default: ({ children }) => <div data-testid="student-layout">{children}</div>,
}));

vi.mock('../../components/student/ChatbotWidget', () => ({
  default: () => null,
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useParams: () => ({ sessionId: 'sess-123' }),
  };
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('TestResultPage score invalidation and reporting', () => {
  const mockResultData = {
    sessionId: 'sess-123',
    testName: 'Đề kiểm tra 15 phút',
    status: 'Graded',
    totalScore: 10,
    maxScore: 10,
    answers: [
      {
        questionId: 'q-1',
        questionNo: 1,
        questionContent: '1 + 1 = ?',
        questionType: 'SINGLE_CHOICE',
        difficulty: 1,
        isCorrect: false,
        machinePointsEarned: 0,
        effectivePoints: 2,
        maxPoints: 2,
        isScoreInvalidated: true,
        reportReason: 'Đáp án câu hỏi bị sai trong ngân hàng đề.',
        options: [
          { optionId: 'opt-1', label: 'A', text: '2', isCorrect: true, isSelected: false },
          { optionId: 'opt-2', label: 'B', text: '3', isCorrect: false, isSelected: true },
        ],
      },
      {
        questionId: 'q-2',
        questionNo: 2,
        questionContent: '2 + 2 = ?',
        questionType: 'SINGLE_CHOICE',
        difficulty: 1,
        isCorrect: true,
        machinePointsEarned: 2,
        effectivePoints: 2,
        maxPoints: 2,
        isScoreInvalidated: false,
        options: [
          { optionId: 'opt-3', label: 'A', text: '4', isCorrect: true, isSelected: true },
        ],
      },
    ],
  };

  it('renders adjusted score banner with exact approved title and score detail for invalidated question', async () => {
    getSessionResult.mockResolvedValue(mockResultData);

    render(
      <BrowserRouter>
        <TestResultPage />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Phiên bản câu hỏi đã được xác nhận có lỗi')
    ).toBeInTheDocument();

    expect(
      screen.getByText(/Điểm ban đầu:\s*0(\.00)?\s*·\s*Điểm sau điều chỉnh:\s*2(\.00)?\s*\/\s*2(\.00)?/i)
    ).toBeInTheDocument();

    // The invalidated question (q-1) should NOT show "Báo cáo câu hỏi"
    // The regular question (q-2) SHOULD show "Báo cáo câu hỏi"
    const reportButtons = screen.getAllByRole('button', { name: /Báo cáo câu hỏi/i });
    expect(reportButtons).toHaveLength(1);
  });

  it('opens report dialog with quick-reason chips on reportable question', async () => {
    getSessionResult.mockResolvedValue(mockResultData);

    render(
      <BrowserRouter>
        <TestResultPage />
      </BrowserRouter>
    );

    const reportButton = await screen.findByRole('button', { name: /Báo cáo câu hỏi/i });
    fireEvent.click(reportButton);

    expect(screen.getByText('Báo cáo câu hỏi 2')).toBeInTheDocument();

    // Check that chips are present
    const chip = screen.getByRole('button', { name: 'Đáp án chưa chính xác' });
    expect(chip).toBeInTheDocument();
    fireEvent.click(chip);

    const textarea = screen.getByLabelText(/Lý do báo cáo/i);
    expect(textarea.value).toContain('Đáp án chưa chính xác');
  });

  it('renders adjusted score banner with effectivePoints on invalidated COMPOSITE question', async () => {
    const compositeResultData = {
      sessionId: 'sess-123',
      testName: 'Đề kiểm tra nhiều mệnh đề',
      status: 'Graded',
      totalScore: 10,
      maxScore: 10,
      answers: [
        {
          questionId: 'q-composite-1',
          questionNo: 1,
          questionContent: 'Cho hình hộp chữ nhật ABCD.A\'B\'C\'D\'...',
          questionType: 'COMPOSITE',
          difficulty: 2,
          isCorrect: false,
          machinePointsEarned: 0.25,
          effectivePoints: 1.0,
          maxPoints: 1.0,
          isScoreInvalidated: true,
          reportReason: 'Mệnh đề c bị lỗi ký hiệu hình học.',
          answerParts: [
            {
              questionPartId: 'qp-1',
              partOrder: 1,
              partLabel: 'a',
              partType: 'TRUE_FALSE',
              partContent: 'AC vuông góc BD',
              correctAnswer: 'Sai',
              studentAnswer: 'Đúng',
              isCorrect: false,
              pointsEarned: 0,
              defaultWeight: 1,
            },
          ],
        },
      ],
    };

    getSessionResult.mockResolvedValue(compositeResultData);

    render(
      <BrowserRouter>
        <TestResultPage />
      </BrowserRouter>
    );

    expect(
      await screen.findByText('Câu hỏi đã bị vô hiệu hóa sau khi chấm')
    ).toBeInTheDocument();

    expect(
      screen.getByText(/Điểm máy chấm:\s*0\.25\s*·\s*Điểm hiệu lực:\s*1\.00\s*\/\s*1\.00/i)
    ).toBeInTheDocument();
  });
});
