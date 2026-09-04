import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import QuestionEditorPage from './QuestionEditorPage';
import { questionBankApi } from '../../services/questionBankApi';
import { NavigationGuardProvider } from '../../contexts/NavigationGuardContext';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ id: '101' }),
    useLocation: () => ({ search: '?from=reported' }),
  };
});

vi.mock('../../services/questionBankApi', () => ({
  questionBankApi: {
    getDifficulties: vi.fn().mockResolvedValue({ data: [{ difficultyId: 'diff-1', difficultyName: 'Nhận biết' }] }),
    getTopicTags: vi.fn().mockResolvedValue({ data: [{ tagId: 'tag-1', name: 'Đại số', depth: 1 }] }),
    getQuestionDetail: vi.fn(),
    updateQuestion: vi.fn(),
    createQuestion: vi.fn(),
    getQuestionReports: vi.fn(),
    updateQuestionReportStatus: vi.fn(),
    submitQuestionReportReview: vi.fn(),
  },
}));

vi.mock('./ExpertLayout', () => ({
  default: ({ children }) => <div data-testid="expert-layout">{children}</div>,
}));

vi.mock('../../components/layout/DashboardLayout', () => ({
  default: ({ children }) => <div data-testid="dashboard-layout">{children}</div>,
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('QuestionEditorPage reported question workflow', () => {
  const sampleDetail = {
    id: 101,
    questionContent: '1 + 1 = 2',
    solutionContent: 'Lời giải',
    pictureUrl: '',
    grade: 12,
    questionType: 'SINGLE_CHOICE',
    difficultyId: 'diff-1',
    defaultWeight: 1,
    topics: [{ tagId: 'tag-1', isPrimary: true, name: 'Đại số' }],
    answers: [
      { answerContent: '2', isCorrect: true },
      { answerContent: '3', isCorrect: false },
    ],
  };

  const studentReport = {
    id: 501,
    reportId: 501,
    reporterRole: 'Student',
    status: 'Pending',
    reportReason: 'Đáp án sai',
    createdTime: '2026-08-25T00:00:00Z',
  };

  const expertReport = {
    id: 502,
    reportId: 502,
    reporterRole: 'Expert',
    status: 'Pending',
    reportReason: 'Độ khó chưa đúng',
    createdTime: '2026-08-25T00:00:00Z',
  };

  const adminPendingFixReport = {
    id: 503,
    reportId: 503,
    reporterRole: 'Admin',
    status: 'PendingFix',
    reportReason: 'Yêu cầu sửa công thức LaTeX',
    reviewNote: 'Công thức bị lỗi ký tự',
    createdTime: '2026-08-25T00:00:00Z',
  };

  beforeEach(() => {
    questionBankApi.getDifficulties.mockResolvedValue({ data: [{ difficultyId: 'diff-1', difficultyName: 'Nhận biết' }] });
    questionBankApi.getTopicTags.mockResolvedValue({ data: [{ tagId: 'tag-1', name: 'Đại số', depth: 1 }] });
    questionBankApi.getQuestionDetail.mockResolvedValue({ data: sampleDetail });
  });

  it('save alone does not resolve Student/Expert reports, and reports remain individually actionable', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [studentReport, expertReport],
    });
    questionBankApi.updateQuestion.mockResolvedValue({ data: { success: true } });
    questionBankApi.updateQuestionReportStatus.mockResolvedValue({ data: { success: true } });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/BÁO CÁO ĐANG CHỜ XỬ LÝ \(2\)/i)).toBeInTheDocument();
    expect(await screen.findByDisplayValue('1 + 1 = 2')).toBeInTheDocument();

    // Click Save button in header
    const saveBtn = screen.getByRole('button', { name: /Cập nhật câu hỏi/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(questionBankApi.updateQuestion).toHaveBeenCalled();
    });

    // Neither report should have been auto-resolved!
    expect(questionBankApi.updateQuestionReportStatus).not.toHaveBeenCalled();

    // Resolving Student report
    questionBankApi.getQuestionReports.mockResolvedValueOnce({
      data: [expertReport],
    });

    const resolveButtons = screen.getAllByRole('button', { name: /Đã khắc phục/i });
    fireEvent.click(resolveButtons[0]);

    await waitFor(() => {
      expect(questionBankApi.updateQuestionReportStatus).toHaveBeenCalledWith(501, {
        status: 'Resolved',
        resolutionAction: 'InvalidateAndAwardFull',
      });
    });

    // Expert report remains active
    expect(await screen.findByText(/Độ khó chưa đúng/i)).toBeInTheDocument();
  });

  it('does not save when the detailed solution is blank', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({ data: [] });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    const solutionInput = await screen.findByPlaceholderText(/Nhập lời giải chi tiết/i);
    fireEvent.change(solutionInput, { target: { value: '   ' } });
    fireEvent.click(screen.getByRole('button', { name: /Cập nhật câu hỏi/i }));

    expect(await screen.findByText(/Vui lòng nhập lời giải chi tiết/i)).toBeInTheDocument();
    expect(questionBankApi.updateQuestion).not.toHaveBeenCalled();
  });

  it('Admin PendingFix primary action calls save then submit-review sequentially', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [adminPendingFixReport],
    });
    questionBankApi.updateQuestion.mockResolvedValue({ data: { success: true } });
    questionBankApi.submitQuestionReportReview.mockResolvedValue({ data: { success: true } });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Admin yêu cầu chỉnh sửa/i)).toBeInTheDocument();
    expect(await screen.findByDisplayValue('1 + 1 = 2')).toBeInTheDocument();

    // Primary action button
    const actionBtns = await screen.findAllByRole('button', { name: /Cập nhật và gửi Admin xét duyệt/i });
    expect(actionBtns.length).toBeGreaterThan(0);
    fireEvent.click(actionBtns[0]);

    await waitFor(() => {
      expect(questionBankApi.updateQuestion).toHaveBeenCalled();
      expect(questionBankApi.submitQuestionReportReview).toHaveBeenCalledWith(503);
    });
  });

  it('update failure prevents submit-review', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [adminPendingFixReport],
    });
    questionBankApi.updateQuestion.mockRejectedValueOnce(new Error('Update failed'));

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByDisplayValue('1 + 1 = 2')).toBeInTheDocument();

    const actionBtns = await screen.findAllByRole('button', { name: /Cập nhật và gửi Admin xét duyệt/i });
    fireEvent.click(actionBtns[0]);

    await waitFor(() => {
      expect(questionBankApi.updateQuestion).toHaveBeenCalled();
      expect(questionBankApi.submitQuestionReportReview).not.toHaveBeenCalled();
    });
  });

  it('submit-review failure shows retryable state: "Nội dung đã được lưu nhưng chưa gửi Admin xét duyệt" and retry calls only submit-review', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [adminPendingFixReport],
    });
    questionBankApi.updateQuestion.mockResolvedValue({ data: { success: true } });
    questionBankApi.submitQuestionReportReview.mockRejectedValueOnce(new Error('Submit review failed'));

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByDisplayValue('1 + 1 = 2')).toBeInTheDocument();

    const actionBtns = await screen.findAllByRole('button', { name: /Cập nhật và gửi Admin xét duyệt/i });
    fireEvent.click(actionBtns[0]);

    expect(
      await screen.findByText(/Nội dung đã được lưu nhưng chưa gửi Admin xét duyệt/i)
    ).toBeInTheDocument();

    // Retry action button should now appear
    const retryBtns = await screen.findAllByRole('button', { name: /Gửi lại Admin xét duyệt/i });
    expect(retryBtns.length).toBeGreaterThan(0);

    // Clicking retry only calls submit-review, NOT updateQuestion again
    questionBankApi.submitQuestionReportReview.mockResolvedValueOnce({ data: { success: true } });
    fireEvent.click(retryBtns[0]);

    await waitFor(() => {
      // updateQuestion should still have been called only once from the initial click
      expect(questionBankApi.updateQuestion).toHaveBeenCalledTimes(1);
      expect(questionBankApi.submitQuestionReportReview).toHaveBeenCalledTimes(2);
    });
  });

  it('asks for confirmation when leaving with unresolved reports', async () => {
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [studentReport],
    });
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/BÁO CÁO ĐANG CHỜ XỬ LÝ \(1\)/i)).toBeInTheDocument();

    const cancelBtn = screen.getByRole('button', { name: /Hủy/i });
    fireEvent.click(cancelBtn);

    expect(confirmSpy).toHaveBeenCalledWith('Bạn vẫn còn báo cáo chưa xử lý. Bạn có chắc chắn muốn rời khỏi trang này?');
    confirmSpy.mockRestore();
  });

  it('does not trigger unresolved work warning when reports are in PendingReview status', async () => {
    const pendingReviewReport = {
      reportId: 'rep-admin-reviewing',
      questionId: 'q-101',
      reporterRole: 'Admin',
      reason: 'Yêu cầu kiểm tra',
      status: 'PendingReview',
      submittedTime: '2026-08-20T10:00:00Z',
    };
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [pendingReviewReport],
    });
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText('Đang chờ Admin xét duyệt')).toBeInTheDocument();

    const cancelBtn = screen.getByRole('button', { name: /Hủy/i });
    fireEvent.click(cancelBtn);

    // Should NOT trigger the "Bạn vẫn còn báo cáo chưa xử lý" warning
    expect(confirmSpy).not.toHaveBeenCalledWith('Bạn vẫn còn báo cáo chưa xử lý. Bạn có chắc chắn muốn rời khỏi trang này?');
    confirmSpy.mockRestore();
  });
});
