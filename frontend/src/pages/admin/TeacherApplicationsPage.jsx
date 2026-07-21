import * as React from "react";
import AdminLayout from "./AdminLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { CustomSelect } from "../../components/ui/custom-select";
import { adminApi } from "../../services/adminApi";

const STATUS_LABELS = {
  Pending: "Chờ duyệt",
  Approved: "Đã duyệt",
  Rejected: "Đã từ chối"
};

const STATUS_BADGE_VARIANTS = {
  Pending: "warning",
  Approved: "success",
  Rejected: "error"
};

function formatDateTime(value) {
  if (!value) return "-";
  try {
    return new Date(value).toLocaleString("vi-VN");
  } catch {
    return "-";
  }
}

function resolveErrorMessage(err, fallback) {
  const data = err?.response?.data;
  return data?.message || err?.message || fallback;
}

export default function TeacherApplicationsPage() {
  const [applications, setApplications] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  const [statusFilter, setStatusFilter] = React.useState("Pending");

  const [pageIndex, setPageIndex] = React.useState(1);
  const [pageSize] = React.useState(10);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);

  // Detail / resolve dialog
  const [isDetailOpen, setIsDetailOpen] = React.useState(false);
  const [detail, setDetail] = React.useState(null);
  const [detailLoading, setDetailLoading] = React.useState(false);
  const [detailError, setDetailError] = React.useState("");

  const [isRejectMode, setIsRejectMode] = React.useState(false);
  const [rejectReason, setRejectReason] = React.useState("");
  const [resolveLoading, setResolveLoading] = React.useState(false);
  const [resolveError, setResolveError] = React.useState("");

  const fetchApplications = React.useCallback(() => {
    setLoading(true);
    setError("");

    const params = { pageIndex, pageSize };
    if (statusFilter) params.status = statusFilter;

    adminApi.getApplications(params)
      .then((res) => {
        const data = res.data || {};
        setApplications(data.items || []);
        setTotalCount(data.totalCount || 0);
        setTotalPages(data.totalPages || 1);
      })
      .catch((err) => {
        console.error("Không thể tải danh sách đơn đăng ký:", err);
        setError(resolveErrorMessage(err, "Không thể kết nối tới máy chủ API."));
      })
      .finally(() => setLoading(false));
  }, [pageIndex, pageSize, statusFilter]);

  React.useEffect(() => {
    fetchApplications();
  }, [fetchApplications]);

  const openDetail = (application) => {
    setIsDetailOpen(true);
    setIsRejectMode(false);
    setRejectReason("");
    setResolveError("");
    setDetail(null);
    setDetailLoading(true);
    setDetailError("");

    adminApi.getApplicationDetail(application.applicationId)
      .then((res) => setDetail(res.data))
      .catch((err) => {
        console.error(err);
        setDetailError(resolveErrorMessage(err, "Không thể tải chi tiết đơn đăng ký."));
      })
      .finally(() => setDetailLoading(false));
  };

  const handleApprove = async () => {
    if (!detail) return;
    setResolveLoading(true);
    setResolveError("");
    try {
      await adminApi.resolveApplication(detail.applicationId, { approve: true });
      setIsDetailOpen(false);
      fetchApplications();
    } catch (err) {
      console.error(err);
      setResolveError(resolveErrorMessage(err, "Duyệt đơn thất bại."));
    } finally {
      setResolveLoading(false);
    }
  };

  const handleReject = async (e) => {
    e.preventDefault();
    if (!detail) return;
    if (!rejectReason.trim()) {
      setResolveError("Vui lòng nhập lý do từ chối.");
      return;
    }
    setResolveLoading(true);
    setResolveError("");
    try {
      await adminApi.resolveApplication(detail.applicationId, {
        approve: false,
        reviewComments: rejectReason.trim()
      });
      setIsDetailOpen(false);
      fetchApplications();
    } catch (err) {
      console.error(err);
      setResolveError(resolveErrorMessage(err, "Từ chối đơn thất bại."));
    } finally {
      setResolveLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">

        <DashboardPageHeader
          title="Đơn đăng ký giáo viên"
          subtitle="Xem xét và duyệt/từ chối đơn đăng ký tài khoản Giáo viên."
        />

        {error && (
          <div className="p-4 border rounded-xl flex items-center justify-between text-sm font-semibold shadow-sm bg-error/10 border-error/20 text-error">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined">error</span>
              <span>{error}</span>
            </div>
          </div>
        )}

        {/* Filter */}
        <div className="flex items-center gap-3 bg-pure-surface border border-whisper-border p-4 rounded-xl shadow-sm">
          <span className="text-xs font-bold text-on-surface-variant uppercase tracking-wider">Trạng thái</span>
          <div className="w-48">
            <CustomSelect
              value={statusFilter}
              onValueChange={(val) => { setStatusFilter(val); setPageIndex(1); }}
              items={[
                { value: "Pending", label: "Chờ duyệt" },
                { value: "Approved", label: "Đã duyệt" },
                { value: "Rejected", label: "Đã từ chối" }
              ]}
            />
          </div>
        </div>

        {/* Table */}
        <div className="w-full bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr className="text-on-surface-variant uppercase text-[11px] font-bold tracking-wider">
                  <th className="py-3 px-4">Giáo viên</th>
                  <th className="py-3 px-4">Email</th>
                  <th className="py-3 px-4 w-40">Ngày nộp</th>
                  <th className="py-3 px-4 w-32">Trạng thái</th>
                  <th className="py-3 px-4 w-28 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border bg-pure-surface text-[14px]">
                {loading ? (
                  <tr>
                    <td colSpan={5} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải danh sách đơn đăng ký...</span>
                      </div>
                    </td>
                  </tr>
                ) : applications.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">inbox</span>
                        Không có đơn đăng ký nào phù hợp.
                      </div>
                    </td>
                  </tr>
                ) : (
                  applications.map((application) => (
                    <tr key={application.applicationId} className="hover:bg-surface-bright transition-all group duration-150">
                      <td className="py-3 px-4 font-semibold text-on-surface">{application.teacherFullName}</td>
                      <td className="py-3 px-4 text-on-surface-variant">{application.teacherEmail}</td>
                      <td className="py-3 px-4 text-on-surface-variant">{formatDateTime(application.appliedTime)}</td>
                      <td className="py-3 px-4">
                        <Badge variant={STATUS_BADGE_VARIANTS[application.status] || "outline"}>
                          {STATUS_LABELS[application.status] || application.status}
                        </Badge>
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => openDetail(application)}
                          className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                          aria-label="Xem chi tiết đơn"
                          title="Xem chi tiết"
                        >
                          <span className="material-symbols-outlined text-[18px]">visibility</span>
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          <div className="bg-surface-container-low border-t border-whisper-border p-4 flex items-center justify-between">
            <span className="text-xs text-on-surface-variant font-bold">
              Hiển thị {applications.length} trong số {totalCount} đơn đăng ký
            </span>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
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
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex((p) => Math.min(totalPages || 1, p + 1))}
                disabled={pageIndex >= totalPages || loading}
              >
                Tiếp
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* DETAIL / RESOLVE DIALOG */}
      <Dialog isOpen={isDetailOpen} onClose={() => setIsDetailOpen(false)} variant="modal">
        <DialogHeader>
          <div className="flex items-center gap-2 mb-1">
            {detail && (
              <Badge variant={STATUS_BADGE_VARIANTS[detail.status] || "outline"}>
                {STATUS_LABELS[detail.status] || detail.status}
              </Badge>
            )}
          </div>
          <DialogTitle>Chi tiết đơn đăng ký giáo viên</DialogTitle>
          <DialogDescription>Xem hồ sơ và chứng chỉ đính kèm trước khi duyệt hoặc từ chối.</DialogDescription>
        </DialogHeader>

        <DialogContent className="space-y-4">
          {detailLoading ? (
            <div className="flex flex-col items-center justify-center py-10 gap-2">
              <div className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
              <span className="text-xs text-on-surface-variant">Đang tải chi tiết...</span>
            </div>
          ) : detailError ? (
            <p className="text-sm text-error text-center py-4">{detailError}</p>
          ) : detail ? (
            <>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Họ tên</h4>
                  <p className="font-semibold text-on-surface text-[14px]">{detail.teacherFullName}</p>
                </div>
                <div>
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Email</h4>
                  <p className="font-semibold text-on-surface text-[14px]">{detail.teacherEmail}</p>
                </div>
                <div>
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Số điện thoại</h4>
                  <p className="font-semibold text-on-surface text-[14px]">{detail.teacherPhoneNumber || "-"}</p>
                </div>
                <div>
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Ngày nộp</h4>
                  <p className="font-semibold text-on-surface text-[14px]">{formatDateTime(detail.appliedTime)}</p>
                </div>
              </div>

              {detail.biography && (
                <div>
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Tiểu sử</h4>
                  <p className="p-3 bg-surface-container rounded-xl text-[13px] leading-relaxed border border-whisper-border">
                    {detail.biography}
                  </p>
                </div>
              )}

              <div>
                <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Chứng chỉ đính kèm</h4>
                {detail.documentsUrl ? (
                  <a
                    href={detail.documentsUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-primary font-semibold text-[13px] hover:underline"
                  >
                    <span className="material-symbols-outlined text-[16px]">description</span>
                    Xem tài liệu
                  </a>
                ) : (
                  <p className="text-[13px] text-on-surface-variant">Không có tài liệu đính kèm.</p>
                )}
              </div>

              {detail.status !== "Pending" && (
                <div className="pt-2 border-t border-whisper-border">
                  <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Kết quả xét duyệt</h4>
                  <p className="text-[13px] text-on-surface-variant">
                    Xử lý lúc {formatDateTime(detail.reviewedTime)}
                    {detail.reviewComments && <> — Lý do: {detail.reviewComments}</>}
                  </p>
                </div>
              )}

              {resolveError && (
                <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                  <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                  <span>{resolveError}</span>
                </div>
              )}

              {isRejectMode && (
                <div className="space-y-2">
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">
                    Lý do từ chối <span className="text-error">*</span>
                  </label>
                  <textarea
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    rows="3"
                    placeholder="Ví dụ: Chứng chỉ không rõ ràng, thiếu thông tin xác thực..."
                    className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                    required
                  />
                </div>
              )}
            </>
          ) : null}
        </DialogContent>

        <DialogFooter>
          {detail && detail.status === "Pending" && !isRejectMode && (
            <>
              <Button
                variant="outline"
                className="border-error text-error hover:bg-error/5"
                onClick={() => { setIsRejectMode(true); setResolveError(""); }}
                disabled={resolveLoading}
              >
                Từ chối
              </Button>
              <Button onClick={handleApprove} disabled={resolveLoading}>
                {resolveLoading ? "Đang xử lý..." : "Duyệt đơn"}
              </Button>
            </>
          )}
          {detail && detail.status === "Pending" && isRejectMode && (
            <>
              <Button variant="outline" onClick={() => setIsRejectMode(false)} disabled={resolveLoading}>
                Quay lại
              </Button>
              <Button
                className="bg-error hover:bg-deep-rose text-white"
                onClick={handleReject}
                disabled={resolveLoading}
              >
                {resolveLoading ? "Đang gửi..." : "Xác nhận từ chối"}
              </Button>
            </>
          )}
          {(!detail || detail.status !== "Pending") && (
            <Button variant="outline" onClick={() => setIsDetailOpen(false)}>
              Đóng
            </Button>
          )}
        </DialogFooter>
      </Dialog>
    </AdminLayout>
  );
}
