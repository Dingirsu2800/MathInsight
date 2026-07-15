import React, { useState, useEffect, useRef } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { CustomSelect } from "../../components/ui/custom-select";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getStatusLabel, getStatusBadgeVariant } from "../../utils/blueprintLabels";
import { getBlueprintActions } from "../../utils/blueprintAuth";
import { getBlueprintErrorMessage } from "../../utils/blueprintErrorLocalizer";
import { cn } from "../../utils/cn";

function getPaginationItems(totalPages, currentPage) {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  const items = [1];
  const rangeStart = Math.max(2, currentPage - 1);
  const rangeEnd = Math.min(totalPages - 1, currentPage + 1);

  if (rangeStart > 2) items.push("ellipsis-start");
  for (let page = rangeStart; page <= rangeEnd; page += 1) items.push(page);
  if (rangeEnd < totalPages - 1) items.push("ellipsis-end");

  items.push(totalPages);
  return items;
}

export default function BlueprintListPage() {
  const navigate = useNavigate();
  const location = useLocation();

  // Tab state: "all" | "mine" | "pending"
  const [activeTab, setActiveTab] = useState("all");

  // Filters state
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedGrade, setSelectedGrade] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("");

  // Pagination state
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(10);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  // List data states
  const [blueprints, setBlueprints] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Feedback banner state
  const [feedback, setFeedback] = useState(null);

  // Action states
  const [openMenuId, setOpenMenuId] = useState(null);
  const [cloneTarget, setCloneTarget] = useState(null);
  const [isCloneConfirmOpen, setIsCloneConfirmOpen] = useState(false);
  const [cloneLoading, setCloneLoading] = useState(false);
  const [cloneError, setCloneError] = useState("");

  const currentAccountId = localStorage.getItem("AccountId");
  const menuRef = useRef(null);

  // Read navigate state feedback once on mount or location change
  useEffect(() => {
    if (location.state?.feedback) {
      setFeedback(location.state.feedback);
      // Clean location state so it doesn't reappear on Back/Refresh
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location, navigate]);

  // Debounce search term
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(searchTerm);
    }, 300);
    return () => clearTimeout(handler);
  }, [searchTerm]);

  // Reset pagination on filter or tab change
  useEffect(() => {
    setPageIndex(1);
  }, [activeTab, debouncedSearch, selectedGrade, selectedStatus]);

  // Click outside to close actions dropdown menu
  useEffect(() => {
    const handleOutsideClick = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setOpenMenuId(null);
      }
    };
    document.addEventListener("mousedown", handleOutsideClick);
    return () => document.removeEventListener("mousedown", handleOutsideClick);
  }, []);

  // Fetch blueprints list
  useEffect(() => {
    let isCurrent = true;
    const abortController = new AbortController();

    const fetchList = async () => {
      setLoading(true);
      setError(null);
      try {
        let response;
        if (activeTab === "pending") {
          response = await testGeneratorApi.getPendingBlueprints({
            pageIndex,
            pageSize
          });
        } else {
          const params = {
            pageIndex,
            pageSize,
            search: debouncedSearch.trim() || undefined,
            grade: selectedGrade ? parseInt(selectedGrade, 10) : undefined,
            status: selectedStatus || undefined,
            expertId: activeTab === "mine" ? currentAccountId : undefined
          };
          response = await testGeneratorApi.getBlueprints(params);
        }

        if (isCurrent) {
          const data = response.data || {};
          setBlueprints(data.items || []);
          setTotalCount(data.totalCount || 0);
          setTotalPages(data.totalPages || 1);
        }
      } catch (err) {
        if (isCurrent) {
          setError(getBlueprintErrorMessage(err, "Không thể tải danh sách cấu trúc đề. Vui lòng thử lại."));
        }
      } finally {
        if (isCurrent) {
          setLoading(false);
        }
      }
    };

    fetchList();

    return () => {
      isCurrent = false;
      abortController.abort();
    };
  }, [activeTab, debouncedSearch, selectedGrade, selectedStatus, pageIndex, pageSize, currentAccountId]);

  // Handle clone action
  const handleCloneConfirm = async () => {
    if (!cloneTarget) return;
    setCloneLoading(true);
    setCloneError("");
    try {
      const res = await testGeneratorApi.cloneBlueprint(cloneTarget.blueprintId);
      setIsCloneConfirmOpen(false);
      setCloneTarget(null);

      // Navigate to the editor for the newly cloned draft blueprint
      navigate(`/expert/blueprints/${res.data.blueprintId}/edit`, {
        state: {
          feedback: {
            type: "success",
            message: `Nhân bản cấu trúc đề thành công thành bản sao nháp: "${res.data.blueprintName}"`
          }
        }
      });
    } catch (err) {
      setCloneError(getBlueprintErrorMessage(err, "Không thể nhân bản cấu trúc đề này. Vui lòng thử lại."));
    } finally {
      setCloneLoading(false);
    }
  };

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">

        {/* Page Header */}
        <DashboardPageHeader
          title="Cấu trúc đề"
          subtitle="Quản lý cấu trúc, tỷ lệ phân bổ câu hỏi và thiết lập ma trận cho đề thi học tập."
        >
          <Button onClick={() => navigate("/expert/blueprints/new")}>
            <span className="material-symbols-outlined mr-2 text-[18px]">add</span>
            Tạo cấu trúc đề
          </Button>
        </DashboardPageHeader>

        {/* Feedback Banner */}
        {feedback && (
          <div className={cn(
            "p-4 rounded-xl border flex items-start gap-3 relative select-text",
            {
              "bg-emerald-success/10 border-emerald-success/20 text-emerald-success": feedback.type === "success",
              "bg-error/10 border-error/20 text-error": feedback.type === "error"
            }
          )}>
            <span className="material-symbols-outlined mt-0.5 shrink-0">
              {feedback.type === "success" ? "check_circle" : "warning"}
            </span>
            <div className="flex-1 pr-8">
              <p className="text-sm font-bold">{feedback.message}</p>
            </div>
            <button
              onClick={() => setFeedback(null)}
              aria-label="Đóng thông báo"
              className="absolute top-3 right-3 text-on-surface-variant hover:text-on-surface transition-colors cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {/* Error Banner */}
        {error && (
          <div className="p-4 rounded-xl border bg-error/10 border-error/20 text-error flex items-start gap-3 select-text">
            <span className="material-symbols-outlined mt-0.5 shrink-0">error</span>
            <p className="text-sm font-bold flex-1">{error}</p>
          </div>
        )}

        {/* Tab view navigation */}
        <div className="border-b border-whisper-border">
          <nav className="flex gap-6">
            <button
              onClick={() => setActiveTab("all")}
              className={cn(
                "pb-3 px-1 text-sm font-bold border-b-2 transition-all cursor-pointer",
                activeTab === "all"
                  ? "border-primary text-primary"
                  : "border-transparent text-on-surface-variant hover:text-primary"
              )}
            >
              Tất cả
            </button>
            <button
              onClick={() => setActiveTab("mine")}
              className={cn(
                "pb-3 px-1 text-sm font-bold border-b-2 transition-all cursor-pointer",
                activeTab === "mine"
                  ? "border-primary text-primary"
                  : "border-transparent text-on-surface-variant hover:text-primary"
              )}
            >
              Của tôi
            </button>
            <button
              onClick={() => setActiveTab("pending")}
              className={cn(
                "pb-3 px-1 text-sm font-bold border-b-2 transition-all cursor-pointer",
                activeTab === "pending"
                  ? "border-primary text-primary"
                  : "border-transparent text-on-surface-variant hover:text-primary"
              )}
            >
              Chờ phản biện
            </button>
          </nav>
        </div>

        {/* Filters and Search toolbar */}
        {activeTab !== "pending" && (
          <div className="bg-pure-surface border border-whisper-border rounded-xl p-4 flex flex-wrap gap-4 items-center justify-between shadow-sm">
            <div className="relative w-full md:w-80">
              <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-sm">
                search
              </span>
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Tìm kiếm theo tên cấu trúc..."
                className="w-full pl-9 pr-3 py-2 bg-surface-container-low border border-whisper-border rounded-lg text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
              />
              {searchTerm && (
                <button
                  onClick={() => setSearchTerm("")}
                  aria-label="Xóa nội dung tìm kiếm"
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-surface cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[16px]">close</span>
                </button>
              )}
            </div>

            <div className="flex gap-3 w-full md:w-auto">
              <div className="w-36">
                <CustomSelect
                  value={selectedGrade}
                  onValueChange={setSelectedGrade}
                  placeholder="Khối lớp"
                  items={[
                    { value: "", label: "Tất cả khối" },
                    { value: "10", label: "Khối 10" },
                    { value: "11", label: "Khối 11" },
                    { value: "12", label: "Khối 12" }
                  ]}
                />
              </div>
              <div className="w-44">
                <CustomSelect
                  value={selectedStatus}
                  onValueChange={setSelectedStatus}
                  placeholder="Trạng thái"
                  items={[
                    { value: "", label: "Tất cả trạng thái" },
                    { value: "Draft", label: "Bản nháp" },
                    { value: "PendingReview", label: "Chờ phản biện" },
                    { value: "Approved", label: "Đã duyệt" },
                    { value: "Rejected", label: "Bị từ chối" },
                    { value: "Active", label: "Đang sử dụng" }
                  ]}
                />
              </div>
            </div>
          </div>
        )}

        {/* Data Table */}
        <div className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm overflow-hidden flex flex-col">
          <div className="overflow-x-auto">
            <table className="w-full text-left whitespace-nowrap border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider">
                    Tên cấu trúc
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-24">
                    Khối
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-48">
                    Cấu trúc
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-24 text-center">
                    Số câu
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-28">
                    Thời gian
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-40">
                    Người tạo
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-36">
                    Trạng thái
                  </th>
                  <th className="py-3 px-4 text-xs font-bold text-on-surface-variant uppercase tracking-wider w-24 text-right">
                    Thao tác
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border text-xs select-text">
                {loading ? (
                  <tr>
                    <td colSpan={8} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải danh sách cấu trúc đề...</span>
                      </div>
                    </td>
                  </tr>
                ) : blueprints.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="py-16 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">search_off</span>
                        Chưa có cấu trúc đề nào phù hợp.
                      </div>
                    </td>
                  </tr>
                ) : (
                  blueprints.map((bp) => {
                    const actions = getBlueprintActions(bp, currentAccountId);
                    const isMenuOpen = openMenuId === bp.blueprintId;

                    return (
                      <tr key={bp.blueprintId} className="hover:bg-surface-bright transition-colors group">
                        <td className="py-3 px-4 max-w-sm">
                          <button
                            onClick={() => navigate(`/expert/blueprints/${bp.blueprintId}`)}
                            className="font-bold text-on-surface text-left hover:text-primary transition-colors cursor-pointer block w-full truncate max-w-xs md:max-w-sm whitespace-normal break-words line-clamp-2"
                            title={bp.blueprintName}
                          >
                            {bp.blueprintName}
                          </button>
                        </td>
                        <td className="py-3 px-4 font-semibold text-on-surface-variant">
                          Lớp {bp.grade}
                        </td>
                        <td className="py-3 px-4 text-on-surface-variant">
                          {bp.sectionCount} phần · {bp.detailSlotCount} phân bổ
                        </td>
                        <td className="py-3 px-4 text-center font-bold text-on-surface font-mono">
                          {bp.totalQuestions}
                        </td>
                        <td className="py-3 px-4 text-on-surface-variant">
                          {bp.durationMinutes} phút
                        </td>
                        <td className="py-3 px-4 text-on-surface-variant truncate max-w-[140px]" title={bp.expertName || "Chưa cập nhật"}>
                          {bp.expertName || "Chưa cập nhật"}
                        </td>
                        <td className="py-3 px-4">
                          <Badge variant={getStatusBadgeVariant(bp.status)}>
                            {getStatusLabel(bp.status)}
                          </Badge>
                        </td>
                        <td className="py-3 px-4 text-right relative">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              onClick={() => navigate(`/expert/blueprints/${bp.blueprintId}`)}
                              className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                              aria-label="Xem chi tiết cấu trúc đề"
                              title="Xem chi tiết"
                            >
                              <span className="material-symbols-outlined text-[18px]">visibility</span>
                            </button>
                            <div className="relative">
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setOpenMenuId(isMenuOpen ? null : bp.blueprintId);
                                }}
                                className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                                aria-label="Thao tác thêm"
                                title="Lựa chọn khác"
                              >
                                <span className="material-symbols-outlined text-[18px]">more_vert</span>
                              </button>

                              {/* Dropdown Action Menu */}
                              {isMenuOpen && (
                                <div
                                  ref={menuRef}
                                  className="absolute right-0 mt-1 w-40 bg-pure-surface border border-whisper-border rounded-xl shadow-lg z-50 overflow-hidden text-left"
                                >
                                  <button
                                    onClick={() => {
                                      setOpenMenuId(null);
                                      navigate(`/expert/blueprints/${bp.blueprintId}`);
                                    }}
                                    className="w-full px-4 py-2.5 text-[11px] font-bold text-on-surface-variant hover:bg-surface-container-low hover:text-primary transition-colors flex items-center gap-2 border-b border-whisper-border/30 cursor-pointer"
                                  >
                                    <span className="material-symbols-outlined text-[16px]">visibility</span>
                                    XEM CHI TIẾT
                                  </button>
                                  {actions.canEdit && (
                                    <button
                                      onClick={() => {
                                        setOpenMenuId(null);
                                        navigate(`/expert/blueprints/${bp.blueprintId}/edit`);
                                      }}
                                      className="w-full px-4 py-2.5 text-[11px] font-bold text-on-surface-variant hover:bg-surface-container-low hover:text-primary transition-colors flex items-center gap-2 border-b border-whisper-border/30 cursor-pointer"
                                    >
                                      <span className="material-symbols-outlined text-[16px]">edit</span>
                                      CHỈNH SỬA
                                    </button>
                                  )}
                                  {actions.canClone && (
                                    <button
                                      onClick={() => {
                                        setOpenMenuId(null);
                                        setCloneTarget(bp);
                                        setCloneError("");
                                        setIsCloneConfirmOpen(true);
                                      }}
                                      className="w-full px-4 py-2.5 text-[11px] font-bold text-on-surface-variant hover:bg-surface-container-low hover:text-primary transition-colors flex items-center gap-2 cursor-pointer"
                                    >
                                      <span className="material-symbols-outlined text-[16px]">content_copy</span>
                                      SAO CHÉP (CLONE)
                                    </button>
                                  )}
                                </div>
                              )}
                            </div>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="p-4 border-t border-whisper-border bg-surface-container-low flex items-center justify-between">
              <span className="text-xs text-on-surface-variant font-bold">
                Hiển thị {blueprints.length} trong số {totalCount} cấu trúc đề
              </span>
              <div className="flex items-center gap-1.5">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                  disabled={pageIndex <= 1 || loading}
                  className="px-3 h-8 font-bold"
                >
                  <span className="material-symbols-outlined text-[16px] mr-1">chevron_left</span>
                  Trước
                </Button>
                <div className="flex items-center gap-1">
                  {getPaginationItems(totalPages, pageIndex).map((item) => {
                    if (typeof item !== "number") {
                      return <span key={item} aria-hidden="true" className="w-5 text-center text-on-surface-variant">…</span>;
                    }

                    const isCurrent = pageIndex === item;
                    return (
                      <button
                        key={item}
                        onClick={() => setPageIndex(item)}
                        disabled={loading}
                        aria-label={`Trang ${item}`}
                        aria-current={isCurrent ? "page" : undefined}
                        className={cn(
                          "w-8 h-8 rounded-lg flex items-center justify-center font-bold transition-all text-xs cursor-pointer",
                          isCurrent
                            ? "bg-primary text-on-primary shadow-sm"
                            : "text-on-surface hover:bg-surface-container"
                        )}
                      >
                        {item}
                      </button>
                    );
                  })}
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                  disabled={pageIndex >= totalPages || loading}
                  className="px-3 h-8 font-bold"
                >
                  Sau
                  <span className="material-symbols-outlined text-[16px] ml-1">chevron_right</span>
                </Button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Clone Confirmation Dialog */}
      <Dialog isOpen={isCloneConfirmOpen} onClose={() => !cloneLoading && setIsCloneConfirmOpen(false)}>
        <DialogHeader>
          <DialogTitle>Xác nhận sao chép cấu trúc đề</DialogTitle>
          <DialogDescription>
            Tạo một bản sao nháp (Draft) mới từ cấu trúc đề: <span className="font-bold text-on-surface">"{cloneTarget?.blueprintName}"</span>.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Hệ thống sẽ thực hiện nhân bản toàn bộ thông tin phần thi và tỷ lệ phân bổ chi tiết của cấu trúc đề này.
            Mọi cấu hình liên kết sẽ thuộc quyền sở hữu của bạn ở trạng thái nháp. Bạn có muốn tiếp tục?
          </p>
          {cloneError && (
            <div className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold mt-3 select-text">
              {cloneError}
            </div>
          )}
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={cloneLoading}
            onClick={() => setIsCloneConfirmOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="primary"
            disabled={cloneLoading}
            onClick={handleCloneConfirm}
          >
            {cloneLoading ? "Đang sao chép..." : "Xác nhận Sao chép"}
          </Button>
        </DialogFooter>
      </Dialog>

    </ExpertLayout>
  );
}
