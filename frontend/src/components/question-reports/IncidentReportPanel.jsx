import React from "react";
import { CustomSelect } from "../ui/custom-select";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { cn } from "../../utils/cn";

export function getRoleLabel(role) {
  if (role === "Student") return "Học sinh";
  if (role === "Expert") return "Chuyên gia";
  if (role === "Admin") return "Quản trị viên";
  return role || "Người dùng";
}

export function getReportStatusMeta(status) {
  switch (status) {
    case "Pending":
      return { label: "Chờ xử lý", className: "bg-amber-500/10 text-amber-700 border-amber-500/20" };
    case "PendingFix":
      return { label: "Admin yêu cầu chỉnh sửa", className: "bg-error/10 text-error border-error/20" };
    case "PendingReview":
      return { label: "Chờ Admin duyệt", className: "bg-primary/10 text-primary border-primary/20" };
    case "Resolved":
      return { label: "Đã chấp nhận", className: "bg-emerald-success/10 text-emerald-success border-emerald-success/20" };
    case "Dismissed":
      return { label: "Không chấp nhận", className: "bg-outline-variant/20 text-on-surface-variant border-outline-variant/30" };
    default:
      return { label: status || "Không xác định", className: "bg-outline-variant/10 text-on-surface-variant border-outline-variant/20" };
  }
}

const DECISION_ITEMS = [
  { value: "Resolved", label: "Chấp nhận báo cáo" },
  { value: "Dismissed", label: "Không chấp nhận báo cáo" }
];

const RESOLUTION_ACTION_ITEMS = [
  { value: "NoScoreChange", label: "Không điều chỉnh điểm" },
  { value: "InvalidateAndAwardFull", label: "Vô hiệu câu hỏi và cộng đủ điểm" }
];

export default function IncidentReportPanel({
  reports = [],
  incident = null,
  reportDispositions = {},
  onDispositionChange,
  reportReviewNotes = {},
  onReviewNoteChange,
  resolutionAction = "NoScoreChange",
  onResolutionActionChange,
  reportsLoading = false,
  reportsError = "",
  onRetry,
  // Legacy single report handling handlers (when report has no incident)
  onResolveLegacyReport,
  onSubmitAdminReview,
  onRetryAdminReview,
  adminReviewSubmitState = "idle",
  loading = false,
  isDetailReady = true,
  updatingReportId = null,
  hasSavedInSession = false,
  className = ""
}) {
  const incidentStatus = incident?.status || (incident ? "Open" : null);
  const isPendingAdminReview = incidentStatus === "PendingAdminReview";
  const isAdjustmentPending = incidentStatus === "AdjustmentPending";
  const isClosed = incidentStatus === "Closed";
  const hasOpenIncident = Boolean(incident && incidentStatus === "Open");

  // Partition reports into 3 truthful groups
  const { expertActionableReports, adminReviewReports, handledReports } = React.useMemo(() => {
    const expertGroup = [];
    const adminGroup = [];
    const handledGroup = [];

    reports.forEach((rep) => {
      const isHandled = rep.status === "Resolved" || rep.status === "Dismissed";
      if (isHandled) {
        handledGroup.push(rep);
        return;
      }

      // Active reports: Pending, PendingFix, PendingReview
      if (incident) {
        if (isPendingAdminReview) {
          // When incident is PendingAdminReview, all active reports await Admin review
          adminGroup.push(rep);
        } else if (hasOpenIncident) {
          // When incident is Open, active reports require Expert action
          expertGroup.push(rep);
        } else {
          // Incident in other state (Closed/AdjustmentPending)
          handledGroup.push(rep);
        }
      } else {
        // Legacy standalone reports (no incident)
        if (rep.status === "PendingReview") {
          adminGroup.push(rep);
        } else {
          expertGroup.push(rep);
        }
      }
    });

    return {
      expertActionableReports: expertGroup,
      adminReviewReports: adminGroup,
      handledReports: handledGroup
    };
  }, [reports, incident, isPendingAdminReview, hasOpenIncident]);

  const totalCount = reports.length;
  const actionableCount = expertActionableReports.length;

  return (
    <div className={cn("bg-pure-surface rounded-xl border border-error/20 p-5 lg:p-6 diffused-shadow shadow-sm", className)}>
      {/* Header with Title and Truthful Counts */}
      <div className="border-b border-error/10 pb-3 mb-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-xs font-bold text-error tracking-wider flex items-center gap-1.5 uppercase m-0">
            <span className="material-symbols-outlined text-[16px]">report</span>
            Báo cáo của phiên bản này
          </h3>
          <div className="flex items-center gap-2 text-[11px]">
            <span className="px-2 py-0.5 rounded-full font-bold bg-error/10 text-error border border-error/20">
              Tổng số: {totalCount}
            </span>
            <span
              className={cn(
                "px-2 py-0.5 rounded-full font-bold border",
                actionableCount > 0
                  ? "bg-amber-500/10 text-amber-700 border-amber-500/20"
                  : "bg-emerald-success/10 text-emerald-success border-emerald-success/20"
              )}
            >
              Cần xử lý: {actionableCount}
            </span>
          </div>
        </div>

        {/* Incident status alerts */}
        {isPendingAdminReview && (
          <div className="mt-2.5 p-2.5 bg-primary/10 border border-primary/20 rounded-lg text-xs text-primary font-medium flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px]">hourglass_top</span>
            <span>Bản sửa và đề xuất xử lý sự cố đang chờ Admin phê duyệt. Bạn không thể chỉnh sửa quyết định lúc này.</span>
          </div>
        )}

        {isAdjustmentPending && (
          <div className="mt-2.5 p-2.5 bg-amber-500/10 border border-amber-500/20 rounded-lg text-xs text-amber-800 font-medium flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px]">sync</span>
            <span>Hệ thống đang tiến hành điều chỉnh điểm cho học sinh bị ảnh hưởng (AdjustmentPending).</span>
          </div>
        )}

        {isClosed && (
          <div className="mt-2.5 p-2.5 bg-emerald-success/10 border border-emerald-success/20 rounded-lg text-xs text-emerald-success font-medium flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px]">check_circle</span>
            <span>Sự cố này đã được đóng hoàn tất.</span>
          </div>
        )}
      </div>

      {/* Resolution Action Selector for Open Incident */}
      {hasOpenIncident && (
        <div className="mb-4 rounded-lg border border-primary/20 bg-primary/5 p-3 text-xs space-y-2">
          <div className="font-bold text-primary flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[15px]">tune</span>
            Quyết định xử lý sự cố
          </div>
          <div className="space-y-1">
            <label
              htmlFor="incident-resolution-action"
              className="block text-[11px] font-bold text-on-surface-variant"
            >
              Phương án điểm
            </label>
            <CustomSelect
              id="incident-resolution-action"
              aria-label="Phương án điểm"
              value={resolutionAction}
              onValueChange={(val) => onResolutionActionChange?.(val)}
              items={RESOLUTION_ACTION_ITEMS}
              disabled={loading || !isDetailReady}
              className="h-10 text-sm rounded-lg bg-pure-surface border-outline-variant"
              itemClassName="text-sm py-2"
            />
          </div>
          <p className="text-[11px] text-on-surface-variant leading-relaxed m-0">
            Chọn quyết định riêng cho từng báo cáo bên dưới. Nếu tất cả đều không chấp nhận và không điều chỉnh điểm, hệ thống không tạo phiên bản câu hỏi mới.
          </p>
        </div>
      )}

      {/* Error state */}
      {reportsError ? (
        <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-lg text-center font-semibold">
          <p className="mb-2">{reportsError}</p>
          {onRetry && (
            <Button variant="outline" size="sm" onClick={onRetry} className="text-[10px] h-7">
              Thử lại
            </Button>
          )}
        </div>
      ) : reportsLoading && reports.length === 0 ? (
        <div className="py-6 text-center text-xs text-on-surface-variant flex items-center justify-center gap-2">
          <div className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
          <span>Đang tải các báo cáo...</span>
        </div>
      ) : reports.length === 0 ? (
        <div className="p-3 text-xs text-emerald-success bg-emerald-success/5 border border-emerald-success/15 rounded-lg text-center font-bold">
          Không có báo cáo nào cho phiên bản này.
        </div>
      ) : (
        <div className="space-y-5 max-h-[32rem] overflow-y-auto pr-1">
          {/* GROUP 1: Chờ Expert xử lý */}
          {expertActionableReports.length > 0 && (
            <div className="space-y-3">
              <div data-testid="group-header-expert" className="flex items-center gap-2 text-xs font-bold text-on-surface border-b border-outline-variant/30 pb-1.5">
                <span className="w-2 h-2 rounded-full bg-amber-500"></span>
                <span>Chờ Expert xử lý ({expertActionableReports.length})</span>
              </div>
              <div className="space-y-3">
                {expertActionableReports.map((rep) => (
                  <ReportItemCard
                    key={rep.reportId || rep.id}
                    report={rep}
                    isActionable={true}
                    hasOpenIncident={hasOpenIncident}
                    disposition={reportDispositions[String(rep.reportId || rep.id)] || "Resolved"}
                    onDispositionChange={(val) => onDispositionChange?.(String(rep.reportId || rep.id), val)}
                    reviewNote={reportReviewNotes[String(rep.reportId || rep.id)] ?? ""}
                    onReviewNoteChange={(val) => onReviewNoteChange?.(String(rep.reportId || rep.id), val)}
                    onResolveLegacyReport={onResolveLegacyReport}
                    onSubmitAdminReview={onSubmitAdminReview}
                    onRetryAdminReview={onRetryAdminReview}
                    adminReviewSubmitState={adminReviewSubmitState}
                    loading={loading}
                    isDetailReady={isDetailReady}
                    reportsLoading={reportsLoading}
                    reportsError={reportsError}
                    updatingReportId={updatingReportId}
                    hasSavedInSession={hasSavedInSession}
                  />
                ))}
              </div>
            </div>
          )}

          {/* GROUP 2: Chờ Admin duyệt */}
          {adminReviewReports.length > 0 && (
            <div className="space-y-3">
              <div data-testid="group-header-admin" className="flex items-center gap-2 text-xs font-bold text-on-surface border-b border-outline-variant/30 pb-1.5">
                <span className="w-2 h-2 rounded-full bg-primary"></span>
                <span>Chờ Admin duyệt ({adminReviewReports.length})</span>
              </div>
              <div className="space-y-3">
                {adminReviewReports.map((rep) => (
                  <ReportItemCard
                    key={rep.reportId || rep.id}
                    report={rep}
                    isActionable={false}
                    isAwaitingAdmin={true}
                    hasOpenIncident={hasOpenIncident}
                    disposition={rep.proposedStatus || reportDispositions[String(rep.reportId || rep.id)] || "Resolved"}
                    reviewNote={rep.proposedReviewNote || reportReviewNotes[String(rep.reportId || rep.id)] || ""}
                  />
                ))}
              </div>
            </div>
          )}

          {/* GROUP 3: Đã xử lý */}
          {handledReports.length > 0 && (
            <div className="space-y-3">
              <div data-testid="group-header-handled" className="flex items-center gap-2 text-xs font-bold text-on-surface border-b border-outline-variant/30 pb-1.5">
                <span className="w-2 h-2 rounded-full bg-emerald-success"></span>
                <span>Đã xử lý ({handledReports.length})</span>
              </div>
              <div className="space-y-3">
                {handledReports.map((rep) => (
                  <ReportItemCard
                    key={rep.reportId || rep.id}
                    report={rep}
                    isActionable={false}
                    isHandled={true}
                    hasOpenIncident={hasOpenIncident}
                  />
                ))}
              </div>
            </div>
          )}

          {/* Helper note for legacy student/expert reports needing save first */}
          {!hasOpenIncident && !hasSavedInSession && !reportsError && expertActionableReports.some(rep => (rep.reporterRole === "Student" || rep.reporterRole === "Expert") && rep.status === "Pending") && (
            <p className="text-[10px] text-on-surface-variant/75 mt-3 italic leading-relaxed text-center">
              * Nút &ldquo;Đã khắc phục&rdquo; sẽ hoạt động sau khi bạn ấn &ldquo;Cập nhật câu hỏi&rdquo; thành công ít nhất một lần. Bạn có thể từ chối báo cáo kèm lý do mà không cần lưu câu hỏi.
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function ReportItemCard({
  report,
  isActionable = false,
  isAwaitingAdmin = false,
  isHandled = false,
  hasOpenIncident = false,
  disposition = "Resolved",
  onDispositionChange,
  reviewNote = "",
  onReviewNoteChange,
  onResolveLegacyReport,
  onSubmitAdminReview,
  onRetryAdminReview,
  adminReviewSubmitState = "idle",
  loading = false,
  isDetailReady = true,
  reportsLoading = false,
  reportsError = "",
  updatingReportId,
  hasSavedInSession
}) {
  const reportId = String(report.reportId || report.id);
  const reporterName = report.reporterName?.trim() || "Người báo cáo";
  const roleLabel = getRoleLabel(report.reporterRole || report.role);
  const statusMeta = getReportStatusMeta(report.status);
  const time = report.createdTime
    ? new Date(report.createdTime).toLocaleString("vi-VN")
    : "Chưa rõ thời gian";
  const isUpdating = String(updatingReportId) === reportId;

  return (
    <div
      data-testid={`report-card-${reportId}`}
      className="p-3.5 bg-surface-container-lowest border border-outline-variant/50 rounded-lg text-xs space-y-2.5 shadow-2xs"
    >
      {/* Reporter info and metadata bar */}
      <div className="flex flex-wrap justify-between items-center gap-1.5 text-[11px]">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="font-bold text-error bg-error/10 border border-error/20 px-1.5 py-0.5 rounded text-[10px] uppercase">
            {roleLabel}
          </span>
          <span className="font-semibold text-on-surface" data-testid={`reporter-name-${reportId}`}>
            {reporterName}
          </span>
          <span className={cn("px-1.5 py-0.2 rounded border text-[10px] font-medium", statusMeta.className)}>
            {statusMeta.label}
          </span>
        </div>
        <span className="text-[10px] font-mono text-on-surface-variant/70">{time}</span>
      </div>

      {/* Report reason */}
      <div className="text-on-surface font-normal leading-relaxed italic bg-surface-container-low/50 p-2 rounded border border-outline-variant/20">
        &ldquo;{report.reportReason || report.reason || "Không cung cấp lý do chi tiết."}&rdquo;
      </div>

      {/* Admin Prior Rejection Review Note (if any) */}
      {report.reviewNote && (
        <div className="p-2.5 bg-error/10 border border-error/20 rounded-lg text-xs space-y-1">
          <div className="font-bold text-error flex items-center gap-1">
            <span className="material-symbols-outlined text-[14px]">cancel</span>
            <span>Lý do từ chối:</span>
          </div>
          <div className="whitespace-pre-wrap break-words text-on-surface leading-relaxed text-[11px]">
            {report.reviewNote}
          </div>
        </div>
      )}

      {/* Action Controls for Open Incident */}
      {hasOpenIncident && isActionable && (
        <div className="pt-2 border-t border-outline-variant/30 space-y-2">
          <div className="space-y-1">
            <label
              htmlFor={`report-decision-${reportId}`}
              className="block text-[11px] font-bold text-on-surface-variant"
            >
              Quyết định xử lý
            </label>
            <CustomSelect
              id={`report-decision-${reportId}`}
              aria-label={`Quyết định cho báo cáo của ${reporterName}`}
              value={disposition}
              onValueChange={(val) => onDispositionChange?.(val)}
              items={DECISION_ITEMS}
              disabled={loading || !isDetailReady}
              className="h-10 text-sm rounded-lg bg-pure-surface border-outline-variant"
              itemClassName="text-sm py-2"
            />
          </div>

          {/* Dismissed reason input - required <= 2000 chars */}
          {disposition === "Dismissed" && (
            <div className="space-y-1 pt-1" data-testid={`dismissed-reason-container-${reportId}`}>
              <div className="flex justify-between items-center text-[11px]">
                <label
                  htmlFor={`report-reason-${reportId}`}
                  className="font-bold text-error flex items-center gap-1"
                >
                  <span>Lý do không chấp nhận</span>
                  <span className="text-error font-black">*</span>
                </label>
                <span
                  className={cn(
                    "text-[10px] font-mono",
                    reviewNote.length > 2000 ? "text-error font-bold" : "text-on-surface-variant"
                  )}
                >
                  {reviewNote.length}/2000
                </span>
              </div>
              <textarea
                id={`report-reason-${reportId}`}
                aria-label={`Lý do không chấp nhận báo cáo của ${reporterName}`}
                rows={2}
                value={reviewNote}
                maxLength={2050}
                disabled={loading || !isDetailReady}
                onChange={(e) => onReviewNoteChange?.(e.target.value)}
                placeholder="Nhập lý do không chấp nhận báo cáo này (bắt buộc)..."
                className={cn(
                  "w-full rounded-lg border p-2 text-xs text-on-surface bg-pure-surface focus:outline-none focus:ring-2 transition-all resize-y",
                  reviewNote.trim().length === 0
                    ? "border-error/40 focus:ring-error/20 focus:border-error"
                    : "border-outline-variant focus:ring-primary/20 focus:border-primary"
                )}
              />
              {reviewNote.trim().length === 0 && (
                <p className="text-[10px] text-error font-medium m-0">
                  Bắt buộc nhập lý do không chấp nhận cho báo cáo này.
                </p>
              )}
              {reviewNote.length > 2000 && (
                <p className="text-[10px] text-error font-medium m-0">
                  Lý do không được vượt quá 2000 ký tự.
                </p>
              )}
            </div>
          )}
        </div>
      )}

      {/* Read-only view for reports awaiting Admin approval */}
      {isAwaitingAdmin && (
        <div className="pt-2 border-t border-outline-variant/30 text-[11px] space-y-1.5 text-on-surface-variant bg-surface-container-low/30 p-2 rounded">
          <div className="font-semibold text-primary flex items-center gap-1">
            <span className="material-symbols-outlined text-[14px]">pending</span>
            <span>
              Đề xuất: {disposition === "Dismissed" ? "Không chấp nhận báo cáo" : "Chấp nhận báo cáo"}
            </span>
          </div>
          {disposition === "Dismissed" && reviewNote && (
            <div className="text-[11px] space-y-0.5">
              <span className="font-bold text-on-surface">Lý do đề xuất không chấp nhận:</span>
              <div className="whitespace-pre-wrap text-on-surface-variant pl-2 border-l-2 border-primary/30">
                {reviewNote}
              </div>
            </div>
          )}
          {report.submittedTime && (
            <div className="text-[10px] text-on-surface-variant/80 font-mono font-medium">
              Gửi duyệt lúc: {new Date(report.submittedTime).toLocaleString("vi-VN")}
            </div>
          )}
          <div className="text-[10px] italic text-on-surface-variant/80">
            Đang chờ Admin phê duyệt đề xuất này.
          </div>
        </div>
      )}

      {/* Read-only view for handled reports */}
      {isHandled && (
        <div className="pt-1.5 border-t border-outline-variant/20 text-[11px] text-on-surface-variant flex items-center gap-1.5">
          <span className="material-symbols-outlined text-[14px] text-emerald-success">check</span>
          <span>Báo cáo này đã được hoàn tất xử lý.</span>
        </div>
      )}

      {/* Legacy standalone report actions (no incident) */}
      {!hasOpenIncident && isActionable && (
        <>
          {/* Case 1: Student/Expert legacy reports can use individual action buttons */}
          {(report.reporterRole === "Student" || report.reporterRole === "Expert") && onResolveLegacyReport && (
            <div className="pt-2 border-t border-error/10 space-y-2">
              <div className="space-y-1" data-testid={`legacy-dismiss-reason-container-${reportId}`}>
                <div className="flex justify-between items-center text-[11px]">
                  <label
                    htmlFor={`report-reason-${reportId}`}
                    className="font-bold text-on-surface-variant flex items-center gap-1"
                  >
                    <span>Lý do không chấp nhận</span>
                    <span className="text-on-surface-variant/70 text-[10px]">(bắt buộc nếu từ chối)</span>
                  </label>
                  <span
                    className={cn(
                      "text-[10px] font-mono",
                      reviewNote.length > 2000 ? "text-error font-bold" : "text-on-surface-variant"
                    )}
                  >
                    {reviewNote.length}/2000
                  </span>
                </div>
                <textarea
                  id={`report-reason-${reportId}`}
                  aria-label={`Lý do không chấp nhận báo cáo của ${reporterName}`}
                  rows={2}
                  value={reviewNote}
                  maxLength={2050}
                  disabled={loading || !isDetailReady}
                  onChange={(e) => onReviewNoteChange?.(e.target.value)}
                  placeholder="Nhập lý do nếu không chấp nhận báo cáo này..."
                  className={cn(
                    "w-full rounded-lg border p-2 text-xs text-on-surface bg-pure-surface focus:outline-none focus:ring-2 transition-all resize-y",
                    reviewNote.length > 2000
                      ? "border-error/40 focus:ring-error/20 focus:border-error"
                      : "border-outline-variant focus:ring-primary/20 focus:border-primary"
                  )}
                />
                {reviewNote.length > 2000 && (
                  <p className="text-[10px] text-error font-medium m-0">
                    Lý do không được vượt quá 2000 ký tự.
                  </p>
                )}
              </div>

              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  data-testid={`legacy-resolve-btn-${reportId}`}
                  disabled={!hasSavedInSession || isUpdating || loading || reportsLoading || Boolean(reportsError) || !isDetailReady}
                  onClick={() => onResolveLegacyReport(report.reportId || report.id, "Resolved", report.reporterRole)}
                  className={cn(
                    "px-2.5 py-1 rounded text-[10px] font-bold transition-all border outline-none flex items-center justify-center min-w-[85px] h-7",
                    hasSavedInSession && !isUpdating && !loading && !reportsLoading && !reportsError && isDetailReady
                      ? "bg-emerald-success text-white border-transparent hover:bg-emerald-success/90 cursor-pointer active:scale-95"
                      : "bg-outline-variant/10 text-on-surface-variant/40 border-outline-variant/20 cursor-not-allowed"
                  )}
                  title={!hasSavedInSession ? "Hãy lưu câu hỏi trước khi xử lý báo cáo" : "Đánh dấu là đã khắc phục lỗi"}
                >
                  {isUpdating ? (
                    <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                  ) : (
                    "Đã khắc phục"
                  )}
                </button>
                <button
                  type="button"
                  data-testid={`legacy-dismiss-btn-${reportId}`}
                  disabled={isUpdating || loading || reportsLoading || Boolean(reportsError) || !isDetailReady}
                  onClick={() => {
                    if (reviewNote !== undefined && reviewNote !== "") {
                      onResolveLegacyReport(report.reportId || report.id, "Dismissed", report.reporterRole, reviewNote);
                    } else {
                      onResolveLegacyReport(report.reportId || report.id, "Dismissed", report.reporterRole);
                    }
                  }}
                  className={cn(
                    "px-2.5 py-1 rounded text-[10px] font-bold transition-all border outline-none flex items-center justify-center min-w-[85px] h-7",
                    !isUpdating && !loading && !reportsLoading && !reportsError && isDetailReady
                      ? "bg-pure-surface text-on-surface-variant border-outline-variant hover:bg-surface-container cursor-pointer active:scale-95"
                      : "bg-outline-variant/10 text-on-surface-variant/40 border-outline-variant/20 cursor-not-allowed"
                  )}
                  title="Không chấp nhận báo cáo này"
                >
                  {isUpdating ? (
                    <div className="w-3.5 h-3.5 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                  ) : (
                    "Không chấp nhận"
                  )}
                </button>
              </div>
            </div>
          )}

          {/* Case 2: Admin legacy reports (PendingFix) must submit for Admin review, NEVER use individual buttons */}
          {report.reporterRole === "Admin" && (
            <div className="flex justify-end pt-2 border-t border-error/10">
              <button
                type="button"
                data-testid={`legacy-admin-submit-btn-${reportId}`}
                disabled={loading || isUpdating || !isDetailReady || reportsLoading || Boolean(reportsError) || adminReviewSubmitState === "saving" || adminReviewSubmitState === "submitting"}
                onClick={() => {
                  if (adminReviewSubmitState === "retryable") {
                    onRetryAdminReview?.(report.reportId || report.id);
                  } else {
                    onSubmitAdminReview?.(report.reportId || report.id);
                  }
                }}
                className={cn(
                  "px-2.5 py-1 rounded text-[10px] font-bold transition-all border outline-none flex items-center justify-center min-w-[120px] h-7 bg-primary text-white border-transparent hover:bg-primary/95 cursor-pointer active:scale-95",
                  (loading || isUpdating || !isDetailReady || reportsLoading || Boolean(reportsError) || adminReviewSubmitState === "saving" || adminReviewSubmitState === "submitting") && "opacity-50 cursor-not-allowed"
                )}
                title="Gửi yêu cầu kiểm tra tới Admin"
              >
                {isUpdating || adminReviewSubmitState === "saving" || adminReviewSubmitState === "submitting" ? (
                  <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                ) : adminReviewSubmitState === "retryable" ? (
                  "Gửi lại Admin xét duyệt"
                ) : (
                  "Cập nhật và gửi Admin xét duyệt"
                )}
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
