import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionBankListPage from './QuestionBankListPage';
import { questionBankApi } from '../../services/questionBankApi';

vi.mock('../../services/questionBankApi', () => ({
  questionBankApi: {
    getQuestions: vi.fn(),
    getQuestionDetail: vi.fn(),
    deleteQuestion: vi.fn(),
    reportQuestion: vi.fn(),
    getTags: vi.fn().mockResolvedValue({ data: [] }),
    getDifficulties: vi.fn().mockResolvedValue({ data: [] }),
    getTopicTags: vi.fn().mockResolvedValue({ data: [] }),
  },
}));

vi.mock('../../components/layout/DashboardLayout', () => ({
  default: ({ children }) => <div data-testid="dashboard-layout">{children}</div>,
}));

vi.mock('../../context/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'expert-1', role: 'Expert' },
    isAuthenticated: true,
  }),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('QuestionBankListPage report modal', () => {
  const sampleQuestion = {
    id: 101,
    questionId: 'q-101',
    content: 'Tìm giá trị x thỏa mãn phương trình...',
    topic: 'Đại số 10',
    grade: '10',
    difficulty: 'Thông hiểu',
    difficultyLevel: 'medium',
    type: 'SINGLE_CHOICE',
    status: 'APPROVED',
    expertId: 'expert-other',
    answers: [{ content: 'x = 1', isCorrect: true }],
  };

  it('selects report reasons and only shows free text after choosing Other', async () => {
    questionBankApi.reportQuestion.mockResolvedValue({ data: {} });
    questionBankApi.getQuestions.mockResolvedValue({
      data: {
        items: [sampleQuestion],
        totalCount: 1,
        pageIndex: 1,
        pageSize: 10,
        totalPages: 1,
      },
    });

    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleQuestion,
        expertId: 'expert-other',
      },
    });

    render(
      <BrowserRouter>
        <QuestionBankListPage />
      </BrowserRouter>
    );

    // Click "Xem chi tiết" on the question row
    const viewBtn = await screen.findByTitle('Xem chi tiết');
    fireEvent.click(viewBtn);

    // Inside preview dialog, click "Báo cáo câu hỏi"
    const reportBtn = await screen.findByRole('button', { name: /Báo cáo câu hỏi/i });
    fireEvent.click(reportBtn);

    // Verify chips are rendered
    const chip = await screen.findByRole('button', { name: 'Công thức hoặc hình ảnh bị lỗi' });
    expect(chip).toBeInTheDocument();
    fireEvent.click(chip);

    expect(chip).toHaveAttribute('aria-pressed', 'true');
    expect(screen.queryByLabelText('Mô tả chi tiết')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Khác / bổ sung chi tiết' }));
    expect(chip).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('Mô tả chi tiết')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Mô tả chi tiết'), { target: { value: 'Hình vẽ mờ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Gửi báo cáo' }));

    await waitFor(() => expect(questionBankApi.reportQuestion).toHaveBeenCalledWith('q-101', {
      reportReason: '• Công thức hoặc hình ảnh bị lỗi\nChi tiết: Hình vẽ mờ'
    }));
  });
});
