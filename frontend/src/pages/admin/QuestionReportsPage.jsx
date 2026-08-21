import * as React from "react";
import { Button } from "../../components/ui/button";
import { Badge } from "../../components/ui/badge";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { questionBankApi } from "../../services/questionBankApi";
import AdminLayout from "./AdminLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import LatexPreview from "../../components/expert/LatexPreview";

const REPORT_STATUS_LABELS = {
  Pending: "Chờ xử lý",
  PendingFix: "Chờ expert sửa",
  PendingReview: "Chờ admin duyệt",
  Resolved: "Đã xử lý",
  Dismissed: "Bỏ qua"
};

const REPORT_STATUS_VARIANT = {
  Pending: "warning",
  PendingFix: "warning",
  PendingReview: "primary",
  Resolved: "success",
  Dismissed: "secondary"
};

function formatDate(value) {
  if (!value) return "-";
  try {
    return new Date(value).toLocaleString("vi-VN");
  } catch {
    return "-";
  }
}

export default function QuestionReportsPage() {
  const [reports, setReports] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");
  const [pageIndex, setPageIndex] = React.useState(1);
  const [pageSize] = React.useState(10);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);
  const [selectedReport, setSelectedReport] = React.useState(null);
  const [rejectNote, setRejectNote] = React.useState("");
  const [rejectOpen, setRejectOpen] = React.useState(false);
  const [actionLoading, setActionLoading] = React.useState(false);
  const [actionError, setActionError] = React.useState("");

  const fetchReports = React.useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await questionBankApi.getAdminQuestionReports({
        status: "PendingReview",
        pageIndex,
        pageSize
      });

      const data = res.data || {};
      const items = data.items || [];
      setReports(items);
      setTotalCount(data.totalCount || items.length);
      setTotalPages(data.totalPages || 1);
    } catch (err) {
      console.error(err);
      setError(err.response?.data?.message || err.message || "Không thể tải danh sách báo cáo câu hỏi.");
    } finally {
      setLoading(false);
    }
  }, [pageIndex, pageSize]);

  React.useEffect(() => {
    fetchReports();
  }, [fetchReports]);

  const handleApprove = async (report) => {
    setActionLoading(true);
    setActionError("");
    try {
      await questionBankApi.approveAdminQuestionReport(report.reportId);
      await fetchReports();
    } catch (err) {
      console.error(err);
      setActionError(err.response?.data?.message || err.message || "Phê duyệt báo cáo thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  const handleReject = async () => {
    if (!selectedReport) return;
    const note = rejectNote.trim();
    if (!note) {
      setActionError("Vui lòng nhập lý do từ chối để gửi lại cho expert.");
      return;
    }

    setActionLoading(true);
    setActionError("");
    try {
      await questionBankApi.rejectAdminQuestionReport(selectedReport.reportId, {
        reviewNote: note
      });
      setRejectOpen(false);
      setRejectNote("");
      setSelectedReport(null);
      await fetchReports();
    } catch (err) {
      console.error(err);
      setActionError(err.response?.data?.message || err.message || "Từ chối báo cáo thất bại.");
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        <DashboardPageHeader
          title="Báo cáo câu hỏi"
          subtitle="Quản trị viên xem các báo cáo đã được expert gửi lên và có quyền phê duyệt hoặc từ chối với lý do gửi lại cho expert."
        />

        <div className="w-full bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr className="text-on-surface-variant uppercase text-[11px] font-bold tracking-wider">
                  <th className="py-3 px-4 w-72">Câu hỏi</th>
                  <th className="py-3 px-4 w-36">Chuyên gia</th>
                  <th className="py-3 px-4 w-44">Lý do báo cáo</th>
                  <th className="py-3 px-4 w-28">Trạng thái</th>
                  <th className="py-3 px-4 w-32">Ngày gửi</th>
                  <th className="py-3 px-4 w-40 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border bg-pure-surface text-[14px]">
                {loading ? (
                  <tr>
                    <td colSpan={6} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải báo cáo...</span>
                      </div>
                    </td>
                  </tr>
                ) : error ? (
                  <tr>
                    <td colSpan={6} className="py-16 text-center text-error font-semibold">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[32px]">error</span>
                        <span>{error}</span>
                        <Button variant="outline" size="sm" onClick={fetchReports} className="mt-2">
                          Thử lại
                        </Button>
                      </div>
                    </td>
                  </tr>
                ) : reports.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-14 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">check_circle</span>
                        Không có báo cáo nào đang chờ admin duyệt.
                      </div>
                    </td>
                  </tr>
                ) : (
                  reports.map((report) => (
                    <tr key={report.reportId} className="hover:bg-surface-bright transition-colors">
                      <td className="py-4 px-4 align-top">
                        <div className="flex flex-col gap-1">
                          <span className="font-mono text-[10px] text-primary bg-primary/10 border border-primary/20 px-2 py-0.5 rounded font-bold inline-block w-fit">
                            Q-{report.questionId}
                          </span>
                          <div className="font-semibold text-[13px] leading-relaxed text-on-surface whitespace-pre-line break-words">
                            <LatexPreview content={report.questionContent || "Không có nội dung"} />
                          </div>
                        </div>
                      </td>
                      <td className="py-4 px-4 align-top text-[13px]">
                        {report.expertName || report.expertId || "-"}
                      </td>
                      <td className="py-4 px-4 align-top">
                        <div className="text-[13px] text-on-surface leading-relaxed whitespace-pre-line break-words">
                          {report.reportReason || "-"}
                        </div>
                      </td>
                      <td className="py-4 px-4 align-top">
                        <Badge variant={REPORT_STATUS_VARIANT[report.status] || "secondary"}>
                          {REPORT_STATUS_LABELS[report.status] || report.status}
                        </Badge>
                      </td>
                      <td className="py-4 px-4 align-top text-[13px] text-on-surface-variant">
                        {formatDate(report.createdTime)}
                      </td>
                      <td className="py-4 px-4 align-top text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            className="normal-case h-8 text-xs border-emerald-success text-emerald-success hover:bg-emerald-success/5"
                            onClick={() => handleApprove(report)}
                            disabled={actionLoading}
                          >
                            Phê duyệt
                          </Button>
                          <Button
                            variant="outline"
                            size="sm"
                            className="normal-case h-8 text-xs border-error text-error hover:bg-error/5"
                            onClick={() => {
                              setSelectedReport(report);
                              setRejectNote("");
                              setActionError("");
                              setRejectOpen(true);
                            }}
                            disabled={actionLoading}
                          >
                            Từ chối
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="bg-surface-container-low border-t border-whisper-border p-4 flex items-center justify-between">
            <span className="text-xs text-on-surface-variant font-bold">
              Hiển thị {reports.length} / {totalCount} báo cáo
            </span>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                disabled={pageIndex <= 1 || loading}
              >
                Trước
              </Button>
              <div className="flex items-center justify-center bg-pure-surface border border-whisper-border rounded px-3 text-xs font-bold select-none text-on-surface">
                {pageIndex} / {totalPages || 1}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPageIndex((p) => Math.min(totalPages || 1, p + 1))}
                disabled={pageIndex >= totalPages || loading}
              >
                Tiếp
              </Button>
            </div>
          </div>
        </div>
      </div>

      <Dialog isOpen={rejectOpen} onClose={() => setRejectOpen(false)} variant="modal">
        <DialogHeader>
          <DialogTitle>Từ chối báo cáo</DialogTitle>
          <DialogDescription>
            Nhập lý do từ chối để gửi lại cho expert và yêu cầu sửa/cập nhật câu hỏi.
          </DialogDescription>
        </DialogHeader>
        <DialogContent className="space-y-4">
          {actionError && (
            <div className="p-3 text-xs font-bold text-error bg-error/5 border border-error/15 rounded-xl leading-relaxed flex items-start gap-2">
              <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
              <span>{actionError}</span>
            </div>
          )}
          <div>
            <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">
              Lý do từ chối <span className="text-error">*</span>
            </label>
            <textarea
              value={rejectNote}
              onChange={(e) => setRejectNote(e.target.value)}
              rows="4"
              placeholder="Ví dụ: Câu hỏi chưa đủ tiêu chí, đáp án sai, cần chỉnh sửa nội dung hoặc phân loại..."
              className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
            />
          </div>
        </DialogContent>
        <DialogFooter>
          <Button variant="outline" onClick={() => setRejectOpen(false)} disabled={actionLoading}>Hủy</Button>
          <Button className="bg-error hover:bg-deep-rose text-white" onClick={handleReject} disabled={actionLoading}>
            {actionLoading ? "Đang gửi..." : "Gửi lý do từ chối"}
          </Button>
        </DialogFooter>
      </Dialog>
    </AdminLayout>
  );
}
