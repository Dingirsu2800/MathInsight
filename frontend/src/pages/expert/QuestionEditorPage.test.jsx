import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import QuestionEditorPage from './QuestionEditorPage';
import { questionBankApi } from '../../services/questionBankApi';
import { NavigationGuardProvider } from '../../contexts/NavigationGuardContext';

const mockNavigate = vi.fn();
let locationSearch = '?from=reported';
let locationPathname = '/expert/questions/101/edit';
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ id: '101' }),
    useLocation: () => ({ search: locationSearch, pathname: locationPathname }),
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
    getMyReportedQuestions: vi.fn().mockResolvedValue({ data: { items: [] } }),
    updateQuestionReportStatus: vi.fn(),
    submitQuestionReportReview: vi.fn(),
    submitQuestionReportIncident: vi.fn(),
    getQuestionReportIncident: vi.fn(),
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
  locationSearch = '?from=reported';
  locationPathname = '/expert/questions/101/edit';
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
    locationSearch = '?from=reported';
    locationPathname = '/expert/questions/101/edit';
    questionBankApi.getMyReportedQuestions.mockResolvedValue({ data: { items: [] } });
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

  it('submits atomic incident payload and preserves submissionKey on retry when incidentId is present', async () => {
    const adminIncidentReport = {
      ...adminPendingFixReport,
      incidentId: 'incident-101',
      incidentRevision: 0,
      questionVersionId: 'ver-101',
    };
    locationSearch = '?from=reported&incidentId=incident-101';
    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-101',
        revision: 0,
        status: 'Open',
        originalVersion: { versionId: 'ver-101' },
        reports: [adminIncidentReport],
      },
    });
    questionBankApi.submitQuestionReportIncident
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce({ data: { success: true } });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByDisplayValue('1 + 1 = 2')).toBeInTheDocument();

    await screen.findByText(/Quyết định xử lý sự cố/i);
    const reportDecisionSelect = document.querySelectorAll('select')[1];
    fireEvent.change(reportDecisionSelect, { target: { value: 'Resolved' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi quyết định xử lý/i }));

    await waitFor(() => {
      expect(questionBankApi.submitQuestionReportIncident).toHaveBeenCalledTimes(1);
    });

    const firstCallPayload = questionBankApi.submitQuestionReportIncident.mock.calls[0][1];
    expect(firstCallPayload.expectedRevision).toBe(0);
    expect(firstCallPayload.expectedQuestionVersionId).toBe('ver-101');
    expect(firstCallPayload.resolutionAction).toBe('NoScoreChange');
    expect(firstCallPayload.submissionKey).toBeDefined();
    expect(firstCallPayload.reportDecisions[0].reportId).toBe('503');

    // Retry button appears
    const retryButton = await screen.findByRole('button', { name: /Gửi lại quyết định xử lý/i });
    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(questionBankApi.submitQuestionReportIncident).toHaveBeenCalledTimes(2);
    });

    const secondCallPayload = questionBankApi.submitQuestionReportIncident.mock.calls[1][1];
    expect(secondCallPayload.submissionKey).toBe(firstCallPayload.submissionKey);
  });

  it('submits a no-edit dismissal without creating a correction payload', async () => {
    const incidentReport = {
      ...studentReport,
      incidentId: 'incident-dismiss',
      questionVersionId: 'ver-101',
    };
    locationSearch = '?from=reported&incidentId=incident-dismiss';
    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-dismiss',
        revision: 3,
        status: 'Open',
        originalVersion: { versionId: 'ver-101' },
        reports: [incidentReport],
      },
    });
    questionBankApi.submitQuestionReportIncident.mockResolvedValue({ data: { success: true } });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    await screen.findByText(/Quyết định xử lý sự cố/i);
    const reportDecisionSelect = document.querySelectorAll('select')[1];
    fireEvent.change(reportDecisionSelect, { target: { value: 'Dismissed' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi quyết định xử lý/i }));

    await waitFor(() => {
      expect(questionBankApi.submitQuestionReportIncident).toHaveBeenCalledTimes(1);
    });

    const [, payload] = questionBankApi.submitQuestionReportIncident.mock.calls[0];
    expect(payload.correction).toBeNull();
    expect(payload.reportDecisions).toEqual([
      { reportId: '501', disposition: 'Dismissed', reviewNote: null },
    ]);
    expect(questionBankApi.updateQuestion).not.toHaveBeenCalled();
  });

  it('displays reports[].reviewNote under "Lý do từ chối" with preserved line breaks and formatting when Admin rejects incident', async () => {
    const multiLineRejection = 'Công thức phần b bị lỗi ký tự.\nCần giải thích chi tiết các bước tính tích phân.\nVui lòng cập nhật lại trước ngày mai.';
    const adminIncidentReport = {
      ...adminPendingFixReport,
      incidentId: 'incident-review-note',
      questionVersionId: 'ver-101',
      reviewNote: multiLineRejection,
    };
    locationSearch = '?from=reported&incidentId=incident-review-note';
    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-review-note',
        revision: 1,
        status: 'Open',
        originalVersion: { versionId: 'ver-101' },
        reports: [adminIncidentReport],
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Quyết định xử lý sự cố/i)).toBeInTheDocument();
    expect(screen.getByText(/Lý do từ chối:/i)).toBeInTheDocument();

    const noteElement = screen.getByText((content) => content.includes('Công thức phần b bị lỗi ký tự.'));
    expect(noteElement).toBeInTheDocument();
    expect(noteElement.textContent).toBe(multiLineRejection);
    expect(noteElement).toHaveClass('whitespace-pre-wrap');
  });

  it('resolves incident and displays rejection reason when opened from notification route without search params', async () => {
    const multiLineRejection = 'Đề xuất phương án vô hiệu câu hỏi không hợp lệ.\nVui lòng sửa đáp án.';
    const adminIncidentReport = {
      ...adminPendingFixReport,
      incidentId: 'incident-from-notif',
      questionVersionId: 'ver-101',
      reviewNote: multiLineRejection,
    };

    locationPathname = '/expert/questions/101/reports';
    locationSearch = '';

    questionBankApi.getMyReportedQuestions.mockResolvedValue({
      data: {
        items: [
          {
            questionId: '101',
            incidentId: 'incident-from-notif',
          },
        ],
      },
    });

    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-from-notif',
        revision: 2,
        status: 'Open',
        originalVersion: { versionId: 'ver-101' },
        reports: [adminIncidentReport],
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(questionBankApi.getMyReportedQuestions).toHaveBeenCalled();
      expect(questionBankApi.getQuestionReportIncident).toHaveBeenCalledWith('incident-from-notif');
    });

    expect(await screen.findByText(/BÁO CÁO ĐANG CHỜ XỬ LÝ/i)).toBeInTheDocument();
    expect(screen.getByText(/Lý do từ chối:/i)).toBeInTheDocument();

    const noteElement = screen.getByText((content) => content.includes('Đề xuất phương án vô hiệu'));
    expect(noteElement.textContent).toBe(multiLineRejection);
    expect(noteElement).toHaveClass('whitespace-pre-wrap');
  });

  it('displays reviewNote under "Lý do từ chối" preserving line breaks in legacy admin report mode', async () => {
    const legacyRejection = 'Độ khó câu hỏi chưa phù hợp lớp 12.\nĐề nghị chỉnh sửa lại.';
    const adminReportWithNote = {
      ...adminPendingFixReport,
      reviewNote: legacyRejection,
    };
    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [adminReportWithNote],
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Admin yêu cầu chỉnh sửa/i)).toBeInTheDocument();
    expect(screen.getByText(/Lý do từ chối:/i)).toBeInTheDocument();

    const noteElement = screen.getByText((content) => content.includes('Độ khó câu hỏi chưa phù hợp'));
    expect(noteElement.textContent).toBe(legacyRejection);
    expect(noteElement).toHaveClass('whitespace-pre-wrap');
  });
});
