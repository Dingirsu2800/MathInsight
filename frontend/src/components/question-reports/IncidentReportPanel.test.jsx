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
});
