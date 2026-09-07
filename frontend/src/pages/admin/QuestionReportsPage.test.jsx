import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionReportsPage from './QuestionReportsPage';
import { questionBankApi } from '../../services/questionBankApi';

vi.mock('../../services/questionBankApi', () => ({
  questionBankApi: {
    getAdminQuestionReports: vi.fn(),
    getQuestionReportIncident: vi.fn(),
    approveAdminQuestionReport: vi.fn(),
    rejectAdminQuestionReport: vi.fn(),
  },
}));

vi.mock('./AdminLayout', () => ({ default: ({ children }) => <div>{children}</div> }));
vi.mock('../../components/layout/DashboardPageHeader', () => ({ default: ({ title }) => <h1>{title}</h1> }));
vi.mock('../../components/expert/LatexPreview', () => ({ default: ({ content }) => <span>{content}</span> }));

const reviewItem = {
  reportId: 'report-101',
  incidentId: 'incident-101',
  questionId: 'question-101',
  expertName: 'Expert One',
  questionContent: 'Nội dung đã sửa',
  reportReason: 'Đáp án cần kiểm tra',
  status: 'PendingReview',
  createdTime: '2026-09-07T00:00:00Z',
};

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('QuestionReportsPage incident approval', () => {
  it('loads the incident and shows the submitted correction, score action, and individual dispositions before approval', async () => {
    questionBankApi.getAdminQuestionReports.mockResolvedValue({
      data: { items: [reviewItem], totalCount: 1, totalPages: 1 },
    });
    questionBankApi.getQuestionReportIncident.mockResolvedValue({
      data: {
        incidentId: 'incident-101',
        status: 'PendingAdminReview',
        proposedResolutionAction: 'InvalidateAndAwardFull',
        originalVersion: { versionId: 'version-original', questionContent: 'Nội dung gốc' },
        submittedCorrectionVersion: { versionId: 'version-corrected', questionContent: 'Nội dung đã sửa' },
        reports: [
          { reportId: 'report-101', reporterRole: 'Student', reportReason: 'Đáp án cần kiểm tra', proposedStatus: 'Resolved' },
          { reportId: 'report-102', reporterRole: 'Expert', reportReason: 'Độ khó chưa đúng', proposedStatus: 'Dismissed' },
        ],
      },
    });

    render(<QuestionReportsPage />);

    fireEvent.click(await screen.findByRole('button', { name: /Xem và duyệt/i }));

    await waitFor(() => {
      expect(questionBankApi.getQuestionReportIncident).toHaveBeenCalledWith('incident-101');
    });
    expect(await screen.findByText(/Phiên bản đã sửa/i)).toBeInTheDocument();
    expect(screen.getByText('Nội dung gốc')).toBeInTheDocument();
    expect(screen.getAllByText('Nội dung đã sửa').length).toBeGreaterThan(0);
    expect(screen.getByText(/Vô hiệu câu hỏi và cộng đủ điểm/i)).toBeInTheDocument();
    expect(screen.getByText('Chấp nhận báo cáo')).toBeInTheDocument();
    expect(screen.getByText('Không chấp nhận báo cáo')).toBeInTheDocument();
  });
});
