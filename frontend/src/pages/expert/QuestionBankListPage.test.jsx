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

  it('renders report reason chips and populates reason textarea when reporting a question', async () => {
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

    const textarea = screen.getByPlaceholderText(/Ví dụ: Công thức Toán học hiển thị lỗi/i);
    expect(textarea.value).toBe('Công thức hoặc hình ảnh bị lỗi');
  });
});
