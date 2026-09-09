import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";
import IncidentReportPanel from "./IncidentReportPanel";

describe("IncidentReportPanel component", () => {
  afterEach(() => cleanup());
  const sevenReports = [
    {
      reportId: "rep-1",
      reporterName: "Nguyễn Văn A",
      reporterRole: "Student",
      reportReason: "Sai công thức nghiệm",
      status: "Pending",
      createdTime: "2026-09-01T10:00:00Z"
    },
    {
      reportId: "rep-2",
      reporterName: "Trần Thị B",
      reporterRole: "Student",
      reportReason: "Đề bài thiếu điều kiện x > 0",
      status: "Resolved",
      createdTime: "2026-09-01T11:00:00Z"
    },
    {
      reportId: "rep-3",
      reporterName: "Thầy Hùng",
      reporterRole: "Expert",
      reportReason: "Đáp án C và D trùng nhau",
      status: "Pending",
      createdTime: "2026-09-02T08:00:00Z"
    },
    {
      reportId: "rep-4",
      reporterName: "Quản trị viên Hệ thống",
      reporterRole: "Admin",
      reportReason: "Phát hiện lỗi định dạng LaTeX",
      status: "PendingFix",
      reviewNote: "Công thức phần tích phân chưa chuẩn.\nVui lòng xem lại bước 2.",
      createdTime: "2026-09-02T09:00:00Z"
    },
    {
      reportId: "rep-5",
      reporterName: "Lê Văn C",
      reporterRole: "Student",
      reportReason: "Không hiểu bước 3",
      status: "PendingReview",
      createdTime: "2026-09-02T10:00:00Z"
    },
    {
      reportId: "rep-6",
      reporterName: "Cô Lan",
      reporterRole: "Expert",
      reportReason: "Báo cáo nhầm câu",
      status: "Dismissed",
      createdTime: "2026-09-02T11:00:00Z"
    },
    {
      reportId: "rep-7",
      reporterName: null, // Test neutral Vietnamese fallback
      reporterRole: "Student",
      reportReason: "Hình vẽ bị lệch",
      status: "Pending",
      createdTime: "2026-09-03T12:00:00Z"
    }
  ];

  it("renders heading 'Báo cáo của phiên bản này' and displays total and actionable counts correctly for Open incident", () => {
    const openIncident = {
      incidentId: "inc-100",
      status: "Open",
      revision: 1
    };

    render(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
        reportDispositions={{ "rep-1": "Resolved" }}
      />
    );

    expect(screen.getByText(/Báo cáo của phiên bản này/i)).toBeInTheDocument();
    expect(screen.getByText(/Tổng số: 7/i)).toBeInTheDocument();
    // 5 active reports in Open incident (rep-1, rep-3, rep-4, rep-5, rep-7)
    expect(screen.getByText(/Cần xử lý: 5/i)).toBeInTheDocument();

    // Groups
    expect(screen.getByTestId("group-header-expert")).toHaveTextContent("Chờ Expert xử lý (5)");
    expect(screen.getByTestId("group-header-handled")).toHaveTextContent("Đã xử lý (2)");
    expect(screen.queryByTestId("group-header-admin")).not.toBeInTheDocument();
  });

  it("groups active reports under 'Chờ Admin duyệt' when incident is PendingAdminReview", () => {
    const pendingReviewIncident = {
      incidentId: "inc-100",
      status: "PendingAdminReview",
      revision: 1
    };

    render(
      <IncidentReportPanel
        reports={sevenReports}
        incident={pendingReviewIncident}
      />
    );

    expect(screen.getByText(/Tổng số: 7/i)).toBeInTheDocument();
    expect(screen.getByText(/Cần xử lý: 0/i)).toBeInTheDocument();

    expect(screen.getByTestId("group-header-admin")).toHaveTextContent("Chờ Admin duyệt (5)");
    expect(screen.getByTestId("group-header-handled")).toHaveTextContent("Đã xử lý (2)");
    expect(screen.queryByTestId("group-header-expert")).not.toBeInTheDocument();
    expect(screen.getByText(/Bản sửa và đề xuất xử lý sự cố đang chờ Admin phê duyệt/i)).toBeInTheDocument();
  });

  it("displays fallback 'Người báo cáo' when reporterName is missing and shows distinct reporter names", () => {
    const openIncident = { incidentId: "inc-100", status: "Open" };

    render(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
      />
    );

    expect(screen.getByTestId("reporter-name-rep-1")).toHaveTextContent("Nguyễn Văn A");
    expect(screen.getByTestId("reporter-name-rep-2")).toHaveTextContent("Trần Thị B");
    // rep-7 has null reporterName -> neutral fallback
    expect(screen.getByTestId("reporter-name-rep-7")).toHaveTextContent("Người báo cáo");
  });

  it("displays Admin rejection reviewNote with Lý do từ chối and preserves line breaks", () => {
    const openIncident = { incidentId: "inc-100", status: "Open" };

    render(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
      />
    );

    expect(screen.getByText(/Lý do từ chối:/i)).toBeInTheDocument();
    const noteEl = screen.getByText((content) => content.includes("Công thức phần tích phân chưa chuẩn"));
    expect(noteEl).toBeInTheDocument();
    expect(noteEl).toHaveClass("whitespace-pre-wrap");
    expect(noteEl.textContent).toContain("Công thức phần tích phân chưa chuẩn.\nVui lòng xem lại bước 2.");
  });

  it("renders CustomSelect with accessible label, defaults to Resolved, and shows required reason textarea when Dismissed", () => {
    const openIncident = { incidentId: "inc-100", status: "Open" };
    const onDispositionChange = vi.fn();
    const onReviewNoteChange = vi.fn();

    const { rerender } = render(
      <IncidentReportPanel
        reports={[sevenReports[0]]} // rep-1 (Pending)
        incident={openIncident}
        reportDispositions={{ "rep-1": "Resolved" }}
        onDispositionChange={onDispositionChange}
        reportReviewNotes={{ "rep-1": "" }}
        onReviewNoteChange={onReviewNoteChange}
      />
    );

    // Decision select button is rendered
    const selectTrigger = screen.getByRole("combobox", {
      name: /Quyết định cho báo cáo của Nguyễn Văn A/i
    });
    expect(selectTrigger).toBeInTheDocument();
    expect(selectTrigger).toHaveTextContent("Chấp nhận báo cáo");

    // When disposition is Resolved, no reason textarea is shown
    expect(screen.queryByTestId("dismissed-reason-container-rep-1")).not.toBeInTheDocument();

    // Rerender with Dismissed
    rerender(
      <IncidentReportPanel
        reports={[sevenReports[0]]}
        incident={openIncident}
        reportDispositions={{ "rep-1": "Dismissed" }}
        onDispositionChange={onDispositionChange}
        reportReviewNotes={{ "rep-1": "Lý do kiểm tra" }}
        onReviewNoteChange={onReviewNoteChange}
      />
    );

    expect(screen.getByTestId("dismissed-reason-container-rep-1")).toBeInTheDocument();
    expect(screen.getByLabelText(/Lý do không chấp nhận/i)).toHaveValue("Lý do kiểm tra");
    expect(screen.getByText("14/2000")).toBeInTheDocument();

    // Test typing in reason
    const textarea = screen.getByLabelText(/Lý do không chấp nhận/i);
    fireEvent.change(textarea, { target: { value: "Lý do mới đã sửa" } });
    expect(onReviewNoteChange).toHaveBeenCalledWith("rep-1", "Lý do mới đã sửa");
  });

  it("shows validation warning when Dismissed reason is empty", () => {
    const openIncident = { incidentId: "inc-100", status: "Open" };

    render(
      <IncidentReportPanel
        reports={[sevenReports[0]]}
        incident={openIncident}
        reportDispositions={{ "rep-1": "Dismissed" }}
        reportReviewNotes={{ "rep-1": "   " }} // Whitespace only
      />
    );

    expect(screen.getByText(/Bắt buộc nhập lý do không chấp nhận cho báo cáo này/i)).toBeInTheDocument();
  });

  it("legacy Admin PendingFix renders 'Cập nhật và gửi Admin xét duyệt' and does NOT render individual resolution buttons", () => {
    const legacyAdminReport = {
      reportId: "rep-admin-1",
      reporterName: "Admin User",
      reporterRole: "Admin",
      status: "PendingFix",
      reportReason: "Yêu cầu sửa định dạng câu",
      reviewNote: "Lý do từ chối trước đó",
      createdTime: "2026-09-05T10:00:00Z"
    };

    const onSubmitAdminReview = vi.fn();
    const onResolveLegacyReport = vi.fn();

    render(
      <IncidentReportPanel
        reports={[legacyAdminReport]}
        incident={null}
        hasSavedInSession={true}
        onSubmitAdminReview={onSubmitAdminReview}
        onResolveLegacyReport={onResolveLegacyReport}
      />
    );

    // Individual action buttons must NOT be rendered for Admin
    expect(screen.queryByTestId("legacy-resolve-btn-rep-admin-1")).not.toBeInTheDocument();
    expect(screen.queryByTestId("legacy-dismiss-btn-rep-admin-1")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Đã khắc phục/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Không chấp nhận/i })).not.toBeInTheDocument();

    // Admin submit button must be present
    const submitBtn = screen.getByTestId("legacy-admin-submit-btn-rep-admin-1");
    expect(submitBtn).toBeInTheDocument();
    expect(submitBtn).toHaveTextContent("Cập nhật và gửi Admin xét duyệt");

    fireEvent.click(submitBtn);
    expect(onSubmitAdminReview).toHaveBeenCalledWith("rep-admin-1");
    expect(onResolveLegacyReport).not.toHaveBeenCalled();
  });

  it("legacy Admin PendingFix in retryable state renders 'Gửi lại Admin xét duyệt' and calls onRetryAdminReview", () => {
    const legacyAdminReport = {
      reportId: "rep-admin-2",
      reporterName: "Admin User",
      reporterRole: "Admin",
      status: "PendingFix",
      reportReason: "Yêu cầu sửa định dạng",
      createdTime: "2026-09-05T10:00:00Z"
    };

    const onRetryAdminReview = vi.fn();
    const onSubmitAdminReview = vi.fn();

    render(
      <IncidentReportPanel
        reports={[legacyAdminReport]}
        incident={null}
        hasSavedInSession={true}
        adminReviewSubmitState="retryable"
        onSubmitAdminReview={onSubmitAdminReview}
        onRetryAdminReview={onRetryAdminReview}
      />
    );

    const retryBtn = screen.getByTestId("legacy-admin-submit-btn-rep-admin-2");
    expect(retryBtn).toHaveTextContent("Gửi lại Admin xét duyệt");

    fireEvent.click(retryBtn);
    expect(onRetryAdminReview).toHaveBeenCalledWith("rep-admin-2");
    expect(onSubmitAdminReview).not.toHaveBeenCalled();
  });

  it("legacy Student/Expert renders individual buttons calling onResolveLegacyReport and NOT admin submit button", () => {
    const legacyStudentReport = {
      reportId: "rep-student-1",
      reporterName: "Học sinh Nam",
      reporterRole: "Student",
      status: "Pending",
      reportReason: "Sai đáp án A",
      createdTime: "2026-09-05T10:00:00Z"
    };

    const onResolveLegacyReport = vi.fn();
    const onSubmitAdminReview = vi.fn();

    render(
      <IncidentReportPanel
        reports={[legacyStudentReport]}
        incident={null}
        hasSavedInSession={true}
        onResolveLegacyReport={onResolveLegacyReport}
        onSubmitAdminReview={onSubmitAdminReview}
      />
    );

    // Individual action buttons must exist
    const resolveBtn = screen.getByTestId("legacy-resolve-btn-rep-student-1");
    const dismissBtn = screen.getByTestId("legacy-dismiss-btn-rep-student-1");
    expect(resolveBtn).toBeInTheDocument();
    expect(dismissBtn).toBeInTheDocument();

    // Admin submit button must NOT exist
    expect(screen.queryByTestId("legacy-admin-submit-btn-rep-student-1")).not.toBeInTheDocument();

    // Click "Đã khắc phục"
    fireEvent.click(resolveBtn);
    expect(onResolveLegacyReport).toHaveBeenCalledWith("rep-student-1", "Resolved", "Student");
    expect(onSubmitAdminReview).not.toHaveBeenCalled();

    // Click "Không chấp nhận"
    fireEvent.click(dismissBtn);
    expect(onResolveLegacyReport).toHaveBeenCalledWith("rep-student-1", "Dismissed", "Student");
  });

  it("renders CustomSelect for 'Phương án điểm' with correct values and disables when loading", () => {
    const openIncident = { incidentId: "inc-100", status: "Open" };
    const onResolutionActionChange = vi.fn();

    const { rerender } = render(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
        resolutionAction="NoScoreChange"
        onResolutionActionChange={onResolutionActionChange}
        loading={false}
      />
    );

    const selectTrigger = screen.getByRole("combobox", { name: /Phương án điểm/i });
    expect(selectTrigger).toBeInTheDocument();
    expect(selectTrigger).toHaveTextContent("Không điều chỉnh điểm");
    expect(selectTrigger).not.toBeDisabled();

    // Rerender with InvalidateAndAwardFull
    rerender(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
        resolutionAction="InvalidateAndAwardFull"
        onResolutionActionChange={onResolutionActionChange}
        loading={false}
      />
    );
    expect(selectTrigger).toHaveTextContent("Vô hiệu câu hỏi và cộng đủ điểm");

    // Rerender with loading=true
    rerender(
      <IncidentReportPanel
        reports={sevenReports}
        incident={openIncident}
        resolutionAction="InvalidateAndAwardFull"
        onResolutionActionChange={onResolutionActionChange}
        loading={true}
      />
    );
    expect(selectTrigger).toBeDisabled();
  });

  it("enables legacy dismiss without hasSavedInSession while keeping resolve disabled and passes reviewNote", () => {
    const legacyStudentReport = {
      reportId: "rep-student-2",
      reporterName: "Học sinh Minh",
      reporterRole: "Student",
      status: "Pending",
      reportReason: "Sai kiến thức cơ bản",
      createdTime: "2026-09-05T10:00:00Z"
    };

    const onResolveLegacyReport = vi.fn();
    const onReviewNoteChange = vi.fn();

    render(
      <IncidentReportPanel
        reports={[legacyStudentReport]}
        incident={null}
        hasSavedInSession={false}
        reportReviewNotes={{ "rep-student-2": "Câu hỏi đúng trọng tâm sách giáo khoa" }}
        onReviewNoteChange={onReviewNoteChange}
        onResolveLegacyReport={onResolveLegacyReport}
      />
    );

    const resolveBtn = screen.getByTestId("legacy-resolve-btn-rep-student-2");
    const dismissBtn = screen.getByTestId("legacy-dismiss-btn-rep-student-2");

    // Resolve MUST be disabled when not saved
    expect(resolveBtn).toBeDisabled();
    // Dismiss MUST be enabled even when not saved
    expect(dismissBtn).toBeEnabled();

    // Reason textarea is present
    const reasonInput = screen.getByLabelText(/Lý do không chấp nhận báo cáo của Học sinh Minh/i);
    expect(reasonInput).toHaveValue("Câu hỏi đúng trọng tâm sách giáo khoa");

    fireEvent.click(dismissBtn);
    expect(onResolveLegacyReport).toHaveBeenCalledWith(
      "rep-student-2",
      "Dismissed",
      "Student",
      "Câu hỏi đúng trọng tâm sách giáo khoa"
    );

    // Verify footnote clarifies only "Đã khắc phục" requires save
    expect(screen.getByText(/Nút “Đã khắc phục” sẽ hoạt động sau khi bạn ấn “Cập nhật câu hỏi”/i)).toBeInTheDocument();
    expect(screen.getByText(/Bạn có thể từ chối báo cáo kèm lý do mà không cần lưu câu hỏi/i)).toBeInTheDocument();
  });
});

