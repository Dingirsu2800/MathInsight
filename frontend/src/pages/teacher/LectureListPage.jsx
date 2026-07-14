import * as React from "react";
import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { getLectures, publishLecture, deactivateLecture } from "../../services/learningApi";

const STATUS_BADGES = {
  Draft: { label: "Nháp", bg: "bg-[#F59E0B]/10", text: "text-[#F59E0B]" },
  Published: { label: "Đã xuất bản", bg: "bg-[#10B981]/10", text: "text-[#10B981]" },
  Deactivated: { label: "Ngừng hoạt động", bg: "bg-[#E11D48]/10", text: "text-[#E11D48]" },
};

export default function LectureListPage() {
  const navigate = useNavigate();
  const [lectures, setLectures] = useState([]);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [topicFilter, setTopicFilter] = useState("");
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 10;

  const fetchLectures = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getLectures({ page, pageSize, search, status: statusFilter, topic: topicFilter });
      setLectures(res.data?.items || res.data || []);
      setTotalCount(res.data?.totalCount || res.data?.length || 0);
    } catch (err) {
      console.error("Lỗi khi tải danh sách bài giảng:", err);
      setLectures([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [page, search, statusFilter, topicFilter]);

  useEffect(() => { fetchLectures(); }, [fetchLectures]);

  const handlePublish = async (id) => {
    try { await publishLecture(id); fetchLectures(); } catch (e) { console.error(e); }
  };
  const handleDeactivate = async (id) => {
    try { await deactivateLecture(id); fetchLectures(); } catch (e) { console.error(e); }
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    const d = new Date(iso);
    return d.toLocaleDateString("vi-VN");
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <TeacherLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        <DashboardPageHeader
          title="Bài giảng của tôi"
          subtitle="Quản lý, tạo mới và theo dõi các bài giảng."
        >
          <button
            onClick={() => navigate("/teacher/lectures/new")}
            className="bg-primary text-on-primary px-4 py-2 rounded-lg text-[16px] font-medium hover:opacity-90 transition-opacity flex items-center gap-2 whitespace-nowrap shadow-sm"
          >
            <span className="material-symbols-outlined text-[18px]">add</span>
            Tạo bài giảng mới
          </button>
        </DashboardPageHeader>

        {/* Filters */}
        <div className="bg-pure-surface border border-whisper-border rounded-lg p-4 shadow-sm flex flex-col md:flex-row gap-4 items-center">
          <div className="relative flex-1 w-full">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant">search</span>
            <input
              className="w-full pl-10 pr-4 py-2 border border-outline-variant rounded-lg bg-pure-surface focus:ring-1 focus:ring-primary focus:border-primary text-[13px]"
              placeholder="Tìm kiếm bài giảng..."
              type="text"
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            />
          </div>
          <div className="flex gap-4 w-full md:w-auto">
            <select
              className="flex-1 md:w-48 py-2 px-3 border border-outline-variant rounded-lg bg-pure-surface text-[13px] focus:ring-1 focus:ring-primary"
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
            >
              <option value="">Trạng thái</option>
              <option value="Draft">Nháp</option>
              <option value="Published">Đã xuất bản</option>
              <option value="Deactivated">Ngừng hoạt động</option>
            </select>
            <select
              className="flex-1 md:w-48 py-2 px-3 border border-outline-variant rounded-lg bg-pure-surface text-[13px] focus:ring-1 focus:ring-primary"
              value={topicFilter}
              onChange={(e) => { setTopicFilter(e.target.value); setPage(1); }}
            >
              <option value="">Chủ đề</option>
              <option value="dai-so">Đại số</option>
              <option value="hinh-hoc">Hình học</option>
              <option value="giai-tich">Giải tích</option>
              <option value="xac-suat">Xác suất</option>
              <option value="luong-giac">Lượng giác</option>
            </select>
          </div>
        </div>

        {/* Data Table */}
        <div className="bg-pure-surface border border-whisper-border rounded-lg shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse whitespace-nowrap">
              <thead className="bg-surface-container-low border-b border-whisper-border text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">
                <tr>
                  <th className="px-6 py-4">Tên bài giảng</th>
                  <th className="px-6 py-4">Chủ đề</th>
                  <th className="px-6 py-4">Trạng thái</th>
                  <th className="px-6 py-4">Lượt thích</th>
                  <th className="px-6 py-4">Ngày tạo</th>
                  <th className="px-6 py-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border text-[13px]">
                {loading ? (
                  <tr><td colSpan={6} className="px-6 py-12 text-center text-on-surface-variant">Đang tải dữ liệu...</td></tr>
                ) : lectures.length === 0 ? (
                  <tr><td colSpan={6} className="px-6 py-12 text-center text-on-surface-variant">Không có bài giảng nào.</td></tr>
                ) : lectures.map((lec) => {
                  const badge = STATUS_BADGES[lec.status] || STATUS_BADGES.Draft;
                  return (
                    <tr key={lec.lectureId} className="hover:bg-surface-container/30 transition-colors group">
                      <td className="px-6 py-4 font-medium text-on-surface">
                        <button onClick={() => navigate(`/teacher/lectures/${lec.lectureId}`)} className="hover:text-primary transition-colors text-left">
                          {lec.title}
                        </button>
                      </td>
                      <td className="px-6 py-4 text-on-surface-variant">{lec.tagName || "—"}</td>
                      <td className="px-6 py-4">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${badge.bg} ${badge.text}`}>
                          {badge.label}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-on-surface-variant">{lec.likes ?? 0}</td>
                      <td className="px-6 py-4 text-on-surface-variant font-mono text-[13px]">{formatDate(lec.createdTime)}</td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                          {lec.status !== "Deactivated" && (
                            <button
                              onClick={() => navigate(`/teacher/lectures/${lec.lectureId}/edit`)}
                              className="p-1.5 text-on-surface-variant hover:text-primary rounded-full hover:bg-surface-container-high transition-colors"
                              title="Chỉnh sửa"
                            >
                              <span className="material-symbols-outlined text-[18px]">edit</span>
                            </button>
                          )}
                          {lec.status === "Draft" && (
                            <button
                              onClick={() => handlePublish(lec.lectureId)}
                              className="p-1.5 text-on-surface-variant hover:text-[#10B981] rounded-full hover:bg-[#10B981]/10 transition-colors"
                              title="Xuất bản"
                            >
                              <span className="material-symbols-outlined text-[18px]">publish</span>
                            </button>
                          )}
                          {lec.status === "Published" && (
                            <button
                              onClick={() => handleDeactivate(lec.lectureId)}
                              className="p-1.5 text-on-surface-variant hover:text-error rounded-full hover:bg-error-container/50 transition-colors"
                              title="Ngừng hoạt động"
                            >
                              <span className="material-symbols-outlined text-[18px]">block</span>
                            </button>
                          )}
                          {lec.status === "Deactivated" && (
                            <button
                              onClick={() => navigate(`/teacher/lectures/${lec.lectureId}`)}
                              className="p-1.5 text-on-surface-variant hover:text-primary rounded-full hover:bg-surface-container-high transition-colors"
                              title="Xem"
                            >
                              <span className="material-symbols-outlined text-[18px]">visibility</span>
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {/* Pagination */}
          <div className="px-6 py-4 border-t border-whisper-border flex items-center justify-between bg-pure-surface">
            <span className="text-[13px] text-on-surface-variant">
              Hiển thị {Math.min((page - 1) * pageSize + 1, totalCount)}-{Math.min(page * pageSize, totalCount)} / {totalCount} bài giảng
            </span>
            <div className="flex items-center gap-1">
              <button
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
                className="p-1 rounded text-on-surface-variant hover:bg-surface-container hover:text-primary disabled:opacity-50"
              >
                <span className="material-symbols-outlined text-[20px]">chevron_left</span>
              </button>
              {Array.from({ length: totalPages }, (_, i) => i + 1).slice(0, 5).map((n) => (
                <button
                  key={n}
                  onClick={() => setPage(n)}
                  className={`w-8 h-8 flex items-center justify-center rounded text-[13px] ${
                    n === page
                      ? "bg-primary text-on-primary"
                      : "text-on-surface-variant hover:bg-surface-container"
                  }`}
                >
                  {n}
                </button>
              ))}
              <button
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
                className="p-1 rounded text-on-surface-variant hover:bg-surface-container hover:text-primary disabled:opacity-50"
              >
                <span className="material-symbols-outlined text-[20px]">chevron_right</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </TeacherLayout>
  );
}
