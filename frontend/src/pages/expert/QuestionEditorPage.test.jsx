import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import QuestionEditorPage from './QuestionEditorPage';
import { questionBankApi } from '../../services/questionBankApi';
import { NavigationGuardProvider } from '../../contexts/NavigationGuardContext';

const mockNavigate = vi.fn();
let locationSearch = '?from=reported';
let locationPathname = '/expert/questions/101/edit';
let mockParams = { id: '101' };
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => mockParams,
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

vi.mock('../../components/ui/custom-select', () => ({
  CustomSelect: ({ value, onValueChange, items, id, 'aria-label': ariaLabel, disabled }) => (
    <select
      id={id}
      aria-label={ariaLabel}
      value={value}
      onChange={(e) => onValueChange?.(e.target.value)}
      disabled={disabled}
      role="combobox"
    >
      {items?.map((it) => (
        <option key={it.value} value={it.value}>{it.label}</option>
      ))}
    </select>
  ),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
  mockParams = { id: '101' };
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

    expect(await screen.findByText(/Báo cáo của phiên bản này/i)).toBeInTheDocument();
    expect(screen.getByText(/Tổng số: 2/i)).toBeInTheDocument();
    const contentInput = await screen.findByDisplayValue('1 + 1 = 2');
    expect(contentInput).toBeInTheDocument();

    // Button is disabled when form is not dirty
    const saveBtn = screen.getByRole('button', { name: /Cập nhật câu hỏi/i });
    expect(saveBtn).toBeDisabled();

    // Make dirty
    fireEvent.change(contentInput, { target: { value: '1 + 1 = 2 (fixed)' } });
    expect(saveBtn).toBeEnabled();

    // Click Save button in header
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(questionBankApi.updateQuestion).toHaveBeenCalled();
    });
    expect(saveBtn).toBeDisabled();

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

    expect(await screen.findByText(/Báo cáo của phiên bản này/i)).toBeInTheDocument();
    expect(screen.getByText(/Tổng số: 1/i)).toBeInTheDocument();

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

    expect(await screen.findByTestId('group-header-admin')).toBeInTheDocument();

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
    const reportDecisionSelect = screen.getByLabelText(/Quyết định cho báo cáo/i);
    expect(reportDecisionSelect.value).toBe('Resolved');
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
    const reportDecisionSelect = screen.getByLabelText(/Quyết định cho báo cáo/i);
    fireEvent.change(reportDecisionSelect, { target: { value: 'Dismissed' } });

    const reasonTextarea = await screen.findByLabelText(/Lý do không chấp nhận/i);
    fireEvent.change(reasonTextarea, { target: { value: 'Báo cáo không đúng thực tế' } });

    fireEvent.click(screen.getByRole('button', { name: /Gửi quyết định xử lý/i }));

    await waitFor(() => {
      expect(questionBankApi.submitQuestionReportIncident).toHaveBeenCalledTimes(1);
    });

    const [, payload] = questionBankApi.submitQuestionReportIncident.mock.calls[0];
    expect(payload.correction).toBeNull();
    expect(payload.reportDecisions).toEqual([
      { reportId: '501', disposition: 'Dismissed', reviewNote: 'Báo cáo không đúng thực tế' },
    ]);
    expect(questionBankApi.updateQuestion).not.toHaveBeenCalled();
  });

  it('validates non-empty trimmed reviewNote when disposition is Dismissed', async () => {
    const incidentReport = {
      ...studentReport,
      incidentId: 'incident-dismiss-validation',
      questionVersionId: 'ver-101',
    };
    locationSearch = '?from=reported&incidentId=incident-dismiss-validation';
    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-dismiss-validation',
        revision: 1,
        status: 'Open',
        originalVersion: { versionId: 'ver-101' },
        reports: [incidentReport],
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    await screen.findByText(/Quyết định xử lý sự cố/i);
    const reportDecisionSelect = screen.getByLabelText(/Quyết định cho báo cáo/i);
    fireEvent.change(reportDecisionSelect, { target: { value: 'Dismissed' } });

    // Click submit with empty reason
    fireEvent.click(screen.getByRole('button', { name: /Gửi quyết định xử lý/i }));

    expect(await screen.findByText(/Vui lòng nhập lý do không chấp nhận cho tất cả báo cáo bị từ chối/i)).toBeInTheDocument();
    expect(questionBankApi.submitQuestionReportIncident).not.toHaveBeenCalled();
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

  it('resolves incident and displays rejection reason when opened from notification route without search params using blockingReportIncident contract', async () => {
    const multiLineRejection = 'Đề xuất phương án vô hiệu câu hỏi không hợp lệ.\nVui lòng sửa đáp án.';
    const adminIncidentReport = {
      ...adminPendingFixReport,
      incidentId: 'incident-from-notif',
      questionVersionId: 'ver-101',
      reviewNote: multiLineRejection,
    };

    locationPathname = '/expert/questions/101/reports';
    locationSearch = '';

    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: {
          incidentId: 'incident-from-notif',
          questionVersionId: 'ver-101',
          status: 'Open',
          requiresAdminReview: false,
        },
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
      expect(questionBankApi.getMyReportedQuestions).not.toHaveBeenCalled();
      expect(questionBankApi.getQuestionReportIncident).toHaveBeenCalledWith('incident-from-notif');
    });

    expect(await screen.findByText(/Báo cáo của phiên bản này/i)).toBeInTheDocument();
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

  it('preserves resolutionAction InvalidateAndAwardFull across incident refresh/conflict and does not silently revert to NoScoreChange', async () => {
    locationSearch = '?from=reported&incidentId=inc-score-preserve';

    const incidentDataInitial = {
      incidentId: 'inc-score-preserve',
      revision: 1,
      status: 'Open',
      proposedResolutionAction: 'NoScoreChange',
      originalVersion: { versionId: 'ver-101' },
      reports: [
        {
          id: 601,
          reportId: 601,
          reporterName: 'Học sinh 1',
          reporterRole: 'Student',
          status: 'Pending',
          reportReason: 'Đề sai nghiêm trọng',
          createdTime: '2026-09-01T00:00:00Z',
        },
      ],
    };

    const incidentDataAfterConflict = {
      ...incidentDataInitial,
      revision: 2,
    };

    questionBankApi.getQuestionReportIncident
      .mockResolvedValueOnce({ data: incidentDataInitial })
      .mockResolvedValue({ data: incidentDataAfterConflict });

    // First submit fails with REPORT_SUBMISSION_KEY_CONFLICT
    const conflictError = new Error('Conflict');
    conflictError.response = { data: { code: 'REPORT_SUBMISSION_KEY_CONFLICT' } };
    questionBankApi.submitQuestionReportIncident
      .mockRejectedValueOnce(conflictError)
      .mockResolvedValueOnce({ data: { success: true } });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Quyết định xử lý sự cố/i)).toBeInTheDocument();

    // The score action select initially defaults to NoScoreChange
    const scoreSelect = screen.getByLabelText(/Phương án điểm/i);
    expect(scoreSelect).toHaveValue('NoScoreChange');

    // Expert changes it to InvalidateAndAwardFull
    fireEvent.change(scoreSelect, { target: { value: 'InvalidateAndAwardFull' } });
    expect(scoreSelect).toHaveValue('InvalidateAndAwardFull');

    // Click submit button in header
    const submitBtn = screen.getByRole('button', { name: /Gửi quyết định xử lý/i });
    fireEvent.click(submitBtn);

    // Conflict error banner should appear and fetchPendingReports runs
    expect(await screen.findByText(/Dữ liệu gửi không còn khớp với trạng thái máy chủ/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(questionBankApi.getQuestionReportIncident).toHaveBeenCalledTimes(2);
    });

    // CRITICAL: Ensure select has NOT silently reverted to NoScoreChange
    expect(scoreSelect).toHaveValue('InvalidateAndAwardFull');

    // Click retry submit button
    const retryBtn = await screen.findByRole('button', { name: /Gửi lại quyết định xử lý/i });
    fireEvent.click(retryBtn);

    await waitFor(() => {
      expect(questionBankApi.submitQuestionReportIncident).toHaveBeenCalledTimes(2);
    });

    // Check second submit payload has resolutionAction === "InvalidateAndAwardFull"
    const secondCallPayload = questionBankApi.submitQuestionReportIncident.mock.calls[1][1];
    expect(secondCallPayload.resolutionAction).toBe('InvalidateAndAwardFull');
  });
});

describe('FE-1B: Ordinary edit guard and blocker routing', () => {
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

  beforeEach(() => {
    locationSearch = '';
    locationPathname = '/expert/questions/101/edit';
    questionBankApi.getMyReportedQuestions.mockResolvedValue({ data: { items: [] } });
    questionBankApi.getDifficulties.mockResolvedValue({ data: [{ difficultyId: 'diff-1', difficultyName: 'Nhận biết' }] });
    questionBankApi.getTopicTags.mockResolvedValue({ data: [{ tagId: 'tag-1', name: 'Đại số', depth: 1 }] });
    questionBankApi.getQuestionDetail.mockResolvedValue({ data: sampleDetail });
    questionBankApi.updateQuestion.mockResolvedValue({ data: { success: true } });
  });

  it('blocks ordinary Save and offers "Xử lý báo cáo" when detail has Open blockingReportIncident', async () => {
    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: {
          incidentId: 'incident-open-1',
          questionVersionId: 'ver-101',
          status: 'Open',
          requiresAdminReview: false,
        },
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    // Blocker banner should appear
    expect(await screen.findByText(/Câu hỏi đang có báo cáo sự cố cần xử lý trước khi có thể chỉnh sửa/i)).toBeInTheDocument();

    // Ordinary Save button should NOT be present in the document
    expect(screen.queryByRole('button', { name: /^Cập nhật câu hỏi$/i })).not.toBeInTheDocument();

    // "Xử lý báo cáo" button is available
    const navButtons = screen.getAllByRole('button', { name: /Xử lý báo cáo/i });
    expect(navButtons.length).toBeGreaterThanOrEqual(1);

    // Click "Xử lý báo cáo"
    fireEvent.click(navButtons[0]);

    // Navigates without sending any mutation API
    expect(mockNavigate).toHaveBeenCalledWith('/expert/questions/101/reports?incidentId=incident-open-1');
    expect(questionBankApi.updateQuestion).not.toHaveBeenCalled();
  });

  it('blocks ordinary Save and offers "Xử lý báo cáo" when detail has PendingAdminReview blockingReportIncident', async () => {
    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: {
          incidentId: 'incident-admin-review-1',
          questionVersionId: 'ver-101',
          status: 'PendingAdminReview',
          requiresAdminReview: true,
        },
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    // Blocker banner for PendingAdminReview should appear
    expect(await screen.findByText(/Câu hỏi đang có sự cố báo cáo chờ Admin xét duyệt/i)).toBeInTheDocument();

    // Ordinary Save button should NOT be present
    expect(screen.queryByRole('button', { name: /^Cập nhật câu hỏi$/i })).not.toBeInTheDocument();

    // "Xử lý báo cáo" button is available
    const navButtons = screen.getAllByRole('button', { name: /Xử lý báo cáo/i });
    expect(navButtons.length).toBeGreaterThanOrEqual(1);

    fireEvent.click(navButtons[0]);

    expect(mockNavigate).toHaveBeenCalledWith('/expert/questions/101/reports?incidentId=incident-admin-review-1');
    expect(questionBankApi.updateQuestion).not.toHaveBeenCalled();
  });

  it('protects unsaved changes when navigating to blocker and respects user confirmation', async () => {
    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: {
          incidentId: 'incident-guard-1',
          questionVersionId: 'ver-101',
          status: 'Open',
          requiresAdminReview: false,
        },
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Câu hỏi đang có báo cáo sự cố cần xử lý/i)).toBeInTheDocument();

    // Edit form content to make form dirty
    const textarea = screen.getByDisplayValue('1 + 1 = 2');
    fireEvent.change(textarea, { target: { value: '1 + 1 = 2 (chỉnh sửa bản nháp)' } });

    // Mock confirm dialog - User cancels first
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

    const navBtn = screen.getAllByRole('button', { name: /Xử lý báo cáo/i })[0];
    fireEvent.click(navBtn);

    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Bạn có thay đổi chưa lưu'));
    expect(mockNavigate).not.toHaveBeenCalled();

    // Now user confirms
    confirmSpy.mockReturnValue(true);
    fireEvent.click(navBtn);

    expect(mockNavigate).toHaveBeenCalledWith('/expert/questions/101/reports?incidentId=incident-guard-1');
    confirmSpy.mockRestore();
  });

  it('PUT returns REPORT_INCIDENT_REQUIRES_RESOLUTION: preserves form edits, refreshes blocker, shows "Xử lý báo cáo"', async () => {
    // Initially no blocker
    questionBankApi.getQuestionDetail
      .mockResolvedValueOnce({
        data: {
          ...sampleDetail,
          blockingReportIncident: null,
        },
      })
      .mockResolvedValueOnce({
        data: {
          ...sampleDetail,
          blockingReportIncident: {
            incidentId: 'incident-concurrent-1',
            questionVersionId: 'ver-101',
            status: 'Open',
            requiresAdminReview: false,
          },
        },
      });

    const conflictErr = new Error('Conflict');
    conflictErr.response = {
      status: 409,
      data: {
        code: 'REPORT_INCIDENT_REQUIRES_RESOLUTION',
        message: 'Question editing is blocked until active report incidents are resolved.',
      },
    };
    questionBankApi.updateQuestion.mockRejectedValueOnce(conflictErr);

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    // Normal save button is visible initially
    const saveBtn = await screen.findByRole('button', { name: /^Cập nhật câu hỏi$/i });
    expect(saveBtn).toBeInTheDocument();

    // Edit content
    const textarea = screen.getByDisplayValue('1 + 1 = 2');
    fireEvent.change(textarea, { target: { value: '1 + 1 = 2 (nội dung mới không được ghi đè)' } });

    // Click Save
    fireEvent.click(saveBtn);

    // Blocker error banner is displayed
    const blockerAlerts = await screen.findAllByText(/Câu hỏi đang có báo cáo sự cố cần xử lý trước khi có thể chỉnh sửa/i);
    expect(blockerAlerts.length).toBeGreaterThanOrEqual(1);

    // Crucial: Form input must NOT be overwritten!
    expect(textarea.value).toBe('1 + 1 = 2 (nội dung mới không được ghi đè)');

    // Crucial: getQuestionDetail was called to refresh blocker metadata
    expect(questionBankApi.getQuestionDetail).toHaveBeenCalledTimes(2);

    // Save button was replaced with "Xử lý báo cáo"
    expect(screen.queryByRole('button', { name: /^Cập nhật câu hỏi$/i })).not.toBeInTheDocument();
    const navBtn = screen.getAllByRole('button', { name: /Xử lý báo cáo/i })[0];
    expect(navBtn).toBeInTheDocument();

    // Navigating still triggers the unsaved-change guard
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(navBtn);
    expect(confirmSpy).toHaveBeenCalledWith(expect.stringContaining('Bạn có thay đổi chưa lưu'));
    expect(mockNavigate).toHaveBeenCalledWith('/expert/questions/101/reports?incidentId=incident-concurrent-1');
    confirmSpy.mockRestore();
  });

  it('handles REPORT_INCIDENT_SUBMISSION_REQUIRED on legacy admin submit-review by switching to incident flow without fallback mutation', async () => {
    locationSearch = '?from=reported';
    locationPathname = '/expert/questions/101/edit';

    const adminPendingFix = {
      id: 503,
      reportId: 503,
      reporterRole: 'Admin',
      status: 'PendingFix',
      reportReason: 'Cần sửa LaTeX',
      createdTime: '2026-08-25T00:00:00Z',
    };

    questionBankApi.getQuestionReports.mockResolvedValue({
      data: [adminPendingFix],
    });

    const incidentData = {
      incidentId: 'inc-admin-sub-req-1',
      revision: 1,
      status: 'Open',
      originalVersion: { versionId: 'ver-101' },
      reports: [
        {
          ...adminPendingFix,
          incidentId: 'inc-admin-sub-req-1',
        },
      ],
    };

    questionBankApi.getQuestionDetail
      .mockResolvedValueOnce({
        data: {
          ...sampleDetail,
          blockingReportIncident: null,
        },
      })
      .mockResolvedValue({
        data: {
          ...sampleDetail,
          blockingReportIncident: {
            incidentId: 'inc-admin-sub-req-1',
            questionVersionId: 'ver-101',
            status: 'Open',
            requiresAdminReview: false,
          },
        },
      });

    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: incidentData,
    });

    const subReqErr = new Error('Incident submission required');
    subReqErr.response = {
      status: 409,
      data: {
        code: 'REPORT_INCIDENT_SUBMISSION_REQUIRED',
        message: 'Reports assigned to an incident must be resolved through incident submission.',
      },
    };
    questionBankApi.submitQuestionReportReview.mockRejectedValueOnce(subReqErr);

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    const updateSubmitBtns = await screen.findAllByRole('button', { name: /Cập nhật và gửi Admin xét duyệt/i });
    expect(updateSubmitBtns.length).toBeGreaterThan(0);
    fireEvent.click(updateSubmitBtns[0]);

    // Error banner should notify user that report belongs to an incident
    expect(await screen.findByText(/Báo cáo này thuộc một sự cố và cần xử lý qua quy trình sự cố/i)).toBeInTheDocument();

    // Critical: It does NOT retry or fallback to any single report mutation API
    expect(questionBankApi.submitQuestionReportReview).toHaveBeenCalledTimes(1);

    // It transitioned to the incident workflow
    await waitFor(() => {
      expect(questionBankApi.getQuestionReportIncident).toHaveBeenCalledWith('inc-admin-sub-req-1');
    });
  });

  it('allows normal edit and save when only Closed or AdjustmentPending incidents exist (blockingReportIncident is null)', async () => {
    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: null,
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    // Ordinary Save button is initially disabled when form has not been modified
    const saveBtn = await screen.findByRole('button', { name: /^Cập nhật câu hỏi$/i });
    expect(saveBtn).toBeInTheDocument();
    expect(saveBtn).toBeDisabled();

    // No blocker banner
    expect(screen.queryByText(/Câu hỏi đang có báo cáo sự cố cần xử lý/i)).not.toBeInTheDocument();

    // Edit form to make it dirty
    const input = screen.getByDisplayValue('1 + 1 = 2');
    fireEvent.change(input, { target: { value: '1 + 1 = 3' } });
    expect(saveBtn).toBeEnabled();

    // Click Save
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(questionBankApi.updateQuestion).toHaveBeenCalledWith('101', expect.any(Object));
      expect(mockNavigate).toHaveBeenCalledWith('/expert/questions');
    });
  });

  it('gracefully handles missing metadata in blockingReportIncident without throwing errors', async () => {
    questionBankApi.getQuestionDetail.mockResolvedValue({
      data: {
        ...sampleDetail,
        blockingReportIncident: {
          incidentId: 'inc-sparse-1',
          status: 'Open',
        },
      },
    });

    render(
      <BrowserRouter>
        <NavigationGuardProvider>
          <QuestionEditorPage />
        </NavigationGuardProvider>
      </BrowserRouter>
    );

    expect(await screen.findByText(/Câu hỏi đang có báo cáo sự cố cần xử lý trước khi có thể chỉnh sửa/i)).toBeInTheDocument();
    const navBtn = screen.getAllByRole('button', { name: /Xử lý báo cáo/i })[0];
    expect(navBtn).toBeInTheDocument();
  });

  describe('FE-1C: Save button dirty tracking and baseline reset', () => {
    it('disables "Cập nhật câu hỏi" button when form is clean, enables when edited, and re-disables when reverted', async () => {
      locationSearch = '';
      locationPathname = '/expert/questions/101/edit';
      questionBankApi.getQuestionDetail.mockResolvedValue({
        data: sampleDetail,
      });

      render(
        <BrowserRouter>
          <NavigationGuardProvider>
            <QuestionEditorPage />
          </NavigationGuardProvider>
        </BrowserRouter>
      );

      // Baseline established after load: button is disabled
      const saveBtn = await screen.findByRole('button', { name: /^Cập nhật câu hỏi$/i });
      expect(saveBtn).toBeDisabled();

      // Edit an input -> button becomes enabled
      const input = screen.getByDisplayValue('1 + 1 = 2');
      fireEvent.change(input, { target: { value: '1 + 1 = 100' } });
      expect(saveBtn).toBeEnabled();

      // Revert input back to original value -> button becomes disabled again
      fireEvent.change(input, { target: { value: '1 + 1 = 2' } });
      expect(saveBtn).toBeDisabled();
    });

    it('updates baseline and re-disables "Cập nhật câu hỏi" button after successful save', async () => {
      locationSearch = '?from=reported';
      locationPathname = '/expert/questions/101/edit';
      questionBankApi.getQuestionDetail.mockResolvedValue({
        data: sampleDetail,
      });
      questionBankApi.getQuestionReports.mockResolvedValue({
        data: [],
      });
      questionBankApi.updateQuestion.mockResolvedValue({
        data: { success: true },
      });

      render(
        <BrowserRouter>
          <NavigationGuardProvider>
            <QuestionEditorPage />
          </NavigationGuardProvider>
        </BrowserRouter>
      );

      const saveBtn = await screen.findByRole('button', { name: /^Cập nhật câu hỏi$/i });
      expect(saveBtn).toBeDisabled();

      // Edit input
      const input = screen.getByDisplayValue('1 + 1 = 2');
      fireEvent.change(input, { target: { value: '1 + 1 = 10' } });
      expect(saveBtn).toBeEnabled();

      // Click save
      fireEvent.click(saveBtn);

      await waitFor(() => {
        expect(questionBankApi.updateQuestion).toHaveBeenCalled();
      });

      // Baseline updated -> button is disabled again
      expect(saveBtn).toBeDisabled();
    });

    it('does NOT disable "Lưu câu hỏi" button in create mode solely because !isDirty', async () => {
      mockParams = {};
      locationSearch = '';
      locationPathname = '/expert/questions/create';

      render(
        <BrowserRouter>
          <NavigationGuardProvider>
            <QuestionEditorPage />
          </NavigationGuardProvider>
        </BrowserRouter>
      );

      const createBtn = screen.getByRole('button', { name: /^Lưu câu hỏi$/i });
      expect(createBtn).toBeInTheDocument();
      // Should NOT be disabled purely because !isDirty
      expect(createBtn).toBeEnabled();
    });
  });
});

