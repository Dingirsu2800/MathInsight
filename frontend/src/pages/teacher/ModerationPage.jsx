import * as React from "react";
import { useState, useEffect } from "react";
import TeacherLayout from "./TeacherLayout";
import { getModerationQueue, resolveReport, hideComment } from "../../services/learningApi";
import { toast } from "../../components/common/Toast";

export default function ModerationPage() {
  const [reports, setReports] = useState([]);
  const [statusFilter, setStatusFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);

  // Modal State
  const [showModal, setShowModal] = useState(false);
  const [activeReportId, setActiveReportId] = useState(null);
  const [reasonInput, setReasonInput] = useState("");

  const fetchReports = async () => {
    setLoading(true);
    try {
      // Fetch all reports to calculate accurate statistics (fetch 100 to make sure we get most for now)
      const res = await getModerationQueue({ search, pageSize: 100 });
      setReports(res.data?.items || res.data || []);
    } catch (err) {
      console.error("Lỗi khi tải danh sách báo cáo:", err);
      setReports([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchReports(); }, [statusFilter, search]);

  const handleResolve = async (id, isDismissed) => {
    if (isDismissed) {
      // Dismiss directly
      try {
        await resolveReport(id, { isDismissed: true, reason: null });
        toast.success("Đã bỏ qua báo cáo thành công");
        fetchReports();
      } catch (e) {
        console.error(e);
        toast.error("Lỗi khi xử lý báo cáo");
      }
    } else {
      // Show modal for 'Hidden'
      setActiveReportId(id);
      setReasonInput("");
      setShowModal(true);
    }
  };

  const confirmResolveHidden = async () => {
    if (!reasonInput.trim()) {
      toast.warning("Vui lòng nhập lý do ẩn bình luận!");
      return;
    }
    try {
      await resolveReport(activeReportId, { isDismissed: false, reason: reasonInput });
      setShowModal(false);
      toast.success("Đã ẩn bình luận vi phạm thành công");
      fetchReports();
    } catch (e) {
      console.error(e);
      toast.error("Lỗi khi xử lý báo cáo");
    }
  };

  const parseUtcDate = (dateStr) => {
    if (!dateStr) return new Date();
    if (dateStr.endsWith('Z')) return new Date(dateStr);
    return new Date(dateStr + 'Z');
  };

  const timeAgo = (dateStr) => {
    const hours = Math.floor((new Date() - parseUtcDate(dateStr)) / 3600000);
    if (hours < 24) return `${Math.max(0, hours)} giờ trước`;
    return `${Math.floor(hours/24)} ngày trước`;
  };

  const getStatusInfo = (status) => {
    if (status === "Pending") return { label: "Chờ xử lý", bg: "bg-amber-100", text: "text-amber-800", bar: "bg-amber-500", iconBg: "bg-amber-50", iconText: "text-amber-600" };
    if (status === "Resolved") return { label: "Đã xử lý", bg: "bg-emerald-100", text: "text-emerald-800", bar: "bg-emerald-500", iconBg: "bg-emerald-50", iconText: "text-emerald-600" };
    return { label: "Đã bác bỏ", bg: "bg-gray-100", text: "text-gray-800", bar: "bg-gray-500", iconBg: "bg-gray-50", iconText: "text-gray-600" };
  };

  const pendingCount = reports.filter(r => r.status === "Pending").length;
  const resolvedCount = reports.filter(r => r.status === "Resolved").length;
  const rejectedCount = reports.filter(r => r.status !== "Pending" && r.status !== "Resolved").length;

  const displayReports = reports.filter(r => {
    if (statusFilter === "all") return true;
    if (statusFilter === "pending") return r.status === "Pending";
    if (statusFilter === "resolved") return r.status === "Resolved";
    if (statusFilter === "rejected") return r.status !== "Pending" && r.status !== "Resolved";
    return true;
  });

  return (
    <TeacherLayout>
      <div className="p-gutter flex flex-col gap-8 w-full max-w-6xl mx-auto">
        {/* Header & Stats */}
        <div>
          <h2 className="text-[32px] font-semibold leading-[40px] tracking-[-0.02em] text-on-surface mb-2">Hàng đợi kiểm duyệt</h2>
          <p className="text-[14px] text-on-surface-variant mb-6">Xem xét các báo cáo vi phạm từ học sinh.</p>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-gutter">
            <div className="bg-pure-surface rounded-xl p-4 border border-whisper-border flex flex-col gap-2">
              <span className="text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Chờ xử lý</span>
              <div className="flex items-baseline gap-2">
                <span className="text-[32px] font-semibold text-amber-600">{pendingCount}</span>
              </div>
            </div>
            <div className="bg-pure-surface rounded-xl p-4 border border-whisper-border flex flex-col gap-2">
              <span className="text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Đã xử lý</span>
              <div className="flex items-baseline gap-2">
                <span className="text-[32px] font-semibold text-emerald-600">{resolvedCount}</span>
              </div>
            </div>
            <div className="bg-pure-surface rounded-xl p-4 border border-whisper-border flex flex-col gap-2">
              <span className="text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Đã bác bỏ</span>
              <div className="flex items-baseline gap-2">
                <span className="text-[32px] font-semibold text-outline">{rejectedCount}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Filters */}
        <div className="flex items-center justify-between border-b border-whisper-border pb-4">
          <div className="flex items-center gap-3">
            <span className="text-[16px] font-medium text-on-surface">Trạng thái:</span>
            <select 
              className="bg-pure-surface border border-outline-variant text-on-surface text-[14px] rounded-lg focus:ring-primary focus:border-primary px-3 py-1.5 h-10 outline-none"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="all">Tất cả</option>
              <option value="pending">Chờ xử lý</option>
              <option value="resolved">Đã xử lý</option>
              <option value="rejected">Đã bác bỏ</option>
            </select>
          </div>
          <div className="relative">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-sm">search</span>
            <input 
              className="pl-9 pr-4 py-2 bg-pure-surface border border-outline-variant rounded-lg text-[13px] text-on-surface focus:ring-primary focus:border-primary outline-none w-64" 
              placeholder="Tìm kiếm báo cáo..." 
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>

        {/* Report Cards List */}
        <div className="flex flex-col gap-4">
          {displayReports.map((report) => {
            const statusInfo = getStatusInfo(report.status);
            const isPending = report.status === "Pending";
            return (
              <div key={report.reportId} className={`bg-pure-surface rounded-xl border border-whisper-border shadow-sm flex overflow-hidden group hover:shadow-md transition-shadow ${!isPending ? 'opacity-80' : ''}`}>
                <div className={`w-1.5 ${statusInfo.bar} shrink-0`}></div>
                <div className="p-6 flex-1 flex flex-col gap-5">
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-3">
                      <div className={`w-10 h-10 rounded-full ${statusInfo.iconBg} flex items-center justify-center ${statusInfo.iconText}`}>
                        <span className="material-symbols-outlined" style={{ fontVariationSettings: "'FILL' 1" }}>flag</span>
                      </div>
                      <div>
                        <div className="flex items-center gap-3 mb-1">
                          <h3 className="text-[16px] font-medium text-on-surface m-0">Báo cáo #{report.reportId}</h3>
                          <span className={`px-2 py-0.5 rounded-full ${statusInfo.bg} ${statusInfo.text} text-[10px] font-semibold uppercase tracking-wider`}>
                            {statusInfo.label}
                          </span>
                        </div>
                        <p className="text-[13px] text-on-surface-variant m-0">{timeAgo(report.createdTime)}</p>
                      </div>
                    </div>
                  </div>

                  <div className="bg-surface-container-low rounded-lg p-4 text-[14px] text-on-surface flex flex-col gap-2 border border-whisper-border">
                    {isPending ? (
                      <>
                        <div className="flex gap-2"><span className="font-semibold w-28 shrink-0">Người báo cáo:</span> <span>{report.reporterName} (Học sinh)</span></div>
                        <div className="flex gap-2"><span className="font-semibold w-28 shrink-0">Lý do:</span> <span className="text-error font-medium">{report.reportReason}</span></div>
                        <div className="flex gap-2"><span className="font-semibold w-28 shrink-0">Phân loại:</span> <span>{report.targetType === "Question" ? "Câu hỏi" : "Câu trả lời"} của <strong>{report.targetAuthorName || "Đang tải tên..."}</strong></span></div>
                        <div className="flex gap-2"><span className="font-semibold w-28 shrink-0">Nội dung:</span> <span className="italic text-on-surface-variant line-clamp-2">"{report.targetPreview}"</span></div>
                        <div className="flex gap-2"><span className="font-semibold w-28 shrink-0">Bài giảng:</span> <span className="text-primary font-medium">{report.lectureTitle}</span></div>
                      </>
                    ) : (
                      <>
                        <div className="flex gap-2"><span className="font-semibold w-32 shrink-0">Phân loại:</span> <span>{report.targetType === "Question" ? "Câu hỏi" : "Câu trả lời"} của <strong>{report.targetAuthorName || "Đang tải tên..."}</strong></span></div>
                        <div className="flex gap-2"><span className="font-semibold w-32 shrink-0">Nội dung:</span> <span className="italic text-on-surface-variant line-clamp-2">"{report.targetPreview}"</span></div>
                        <div className="flex gap-2"><span className="font-semibold w-32 shrink-0">Xử lý bởi:</span> <span>{report.resolvedBy || "Đang tải tên..."}</span></div>
                        <div className="flex gap-2"><span className="font-semibold w-32 shrink-0">Thời gian xử lý:</span> <span className="font-mono text-[13px]">{parseUtcDate(report.resolvedAt).toLocaleString("vi-VN")}</span></div>
                      </>
                    )}
                  </div>

                  <div className="flex items-center justify-end gap-3 mt-2">
                    {isPending ? (
                      <>
                        <button onClick={() => handleResolve(report.reportId, true)} className="px-4 py-2 text-[16px] font-medium border border-outline text-on-surface-variant rounded-lg hover:bg-surface-container-low transition-colors">Bác bỏ báo cáo</button>
                        <button onClick={() => handleResolve(report.reportId, false)} className="px-4 py-2 text-[16px] font-medium bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 transition-colors shadow-sm">Ghi nhận vi phạm & Ẩn</button>
                      </>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-center gap-2 mt-4 pt-4 border-t border-whisper-border pb-8">
          <button className="w-10 h-10 rounded-lg flex items-center justify-center border border-outline-variant text-on-surface-variant hover:bg-surface-container-high transition-colors disabled:opacity-50" disabled>
            <span className="material-symbols-outlined text-sm">chevron_left</span>
          </button>
          <button className="w-10 h-10 rounded-lg flex items-center justify-center bg-primary text-on-primary text-[14px] font-semibold">1</button>
          <button className="w-10 h-10 rounded-lg flex items-center justify-center hover:bg-surface-container-high text-on-surface-variant text-[14px] transition-colors">2</button>
          <button className="w-10 h-10 rounded-lg flex items-center justify-center hover:bg-surface-container-high text-on-surface-variant text-[14px] transition-colors">3</button>
          <button className="w-10 h-10 rounded-lg flex items-center justify-center border border-outline-variant text-on-surface-variant hover:bg-surface-container-high transition-colors">
            <span className="material-symbols-outlined text-sm">chevron_right</span>
          </button>
        </div>
      </div>

      {/* Moderation Reason Modal */}
      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
          <div className="bg-pure-surface rounded-2xl p-6 w-full max-w-md shadow-xl flex flex-col gap-4 animate-in fade-in zoom-in-95 duration-200">
            <h3 className="text-xl font-semibold text-on-surface">Ghi nhận vi phạm & Ẩn</h3>
            <p className="text-[14px] text-on-surface-variant">
              Vui lòng nhập lý do cụ thể để học sinh hiểu rõ vì sao bình luận của họ bị ẩn (ví dụ: Ngôn từ thô tục, Spam,...).
            </p>
            <textarea
              className="w-full h-24 p-3 border border-outline-variant rounded-xl text-[14px] text-on-surface focus:border-primary focus:ring-1 focus:ring-primary outline-none resize-none"
              placeholder="Nhập lý do ở đây..."
              value={reasonInput}
              onChange={(e) => setReasonInput(e.target.value)}
              autoFocus
            />
            <div className="flex items-center justify-end gap-3 mt-2">
              <button
                onClick={() => setShowModal(false)}
                className="px-4 py-2 rounded-lg text-[14px] font-medium text-on-surface-variant hover:bg-surface-container transition-colors"
              >
                Hủy bỏ
              </button>
              <button
                onClick={confirmResolveHidden}
                className="px-4 py-2 rounded-lg text-[14px] font-medium bg-error text-white hover:bg-red-700 transition-colors shadow-sm"
              >
                Xác nhận Ẩn
              </button>
            </div>
          </div>
        </div>
      )}
    </TeacherLayout>
  );
}
