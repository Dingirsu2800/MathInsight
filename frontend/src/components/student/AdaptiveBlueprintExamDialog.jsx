import React, { useState, useEffect, useRef, useMemo } from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { useAdaptiveExamFlow } from "../../hooks/useAdaptiveExamFlow";
import { cn } from "../../utils/cn";

export default function AdaptiveBlueprintExamDialog({ isOpen, onClose }) {
  // Search and Pagination State
  const [searchInput, setSearchInput] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);

  // Blueprint Options State
  const [options, setOptions] = useState([]);
  const [loadingOptions, setLoadingOptions] = useState(false);
  const [optionsError, setOptionsError] = useState("");
  const [selectedBlueprintId, setSelectedBlueprintId] = useState("");

  // Own Adaptive Exam Flow Instance (Isolated from Featured Panel)
  const {
    generating,
    starting,
    isBusy,
    generatedTestId,
    actionError,
    resumeSessionId,
    handleCreateAndStart,
    resetActionError,
  } = useAdaptiveExamFlow();

  const abortControllerRef = useRef(null);
  const activeRequestIdRef = useRef(0);
  const isSearchFirstRenderRef = useRef(true);

  // Debounce search input by 300ms
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchInput);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  // Reset page and selected option when debounced search term changes
  useEffect(() => {
    if (isSearchFirstRenderRef.current) {
      isSearchFirstRenderRef.current = false;
      return;
    }
    setPageIndex(1);
    setSelectedBlueprintId("");
  }, [debouncedSearch]);

  const fetchOptions = async (searchTerm, page) => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    const currentRequestId = ++activeRequestIdRef.current;

    setLoadingOptions(true);
    setOptionsError("");

    try {
      const res = await testGeneratorApi.getBlueprintExamOptions(
        {
          search: searchTerm ? searchTerm.trim() : undefined,
          pageIndex: page,
          pageSize,
        },
        { signal: abortController.signal }
      );

      if (currentRequestId !== activeRequestIdRef.current) return;

      const data = res.data || {};
      const items = Array.isArray(data) ? data : (data.items || []);
      const count = typeof data.totalCount === "number" ? data.totalCount : items.length;

      setOptions(items);
      setTotalCount(count);

      // Selection Invariant: Keep one selected BlueprintID across rerenders when it remains in the current result; otherwise clear it explicitly.
      setSelectedBlueprintId((prev) => {
        if (prev && items.some((item) => item.blueprintId === prev)) {
          return prev;
        }
        return "";
      });
    } catch (err) {
      if (
        err?.name === "CanceledError" ||
        err?.name === "AbortError" ||
        err?.code === "ERR_CANCELED" ||
        err?.message === "canceled" ||
        abortController.signal.aborted
      ) {
        return;
      }
      if (currentRequestId !== activeRequestIdRef.current) return;
      setOptionsError(getTestGenErrorMessage(err, "Không thể tải danh sách cấu trúc đề thi. Vui lòng thử lại sau."));
    } finally {
      if (currentRequestId === activeRequestIdRef.current) {
        setLoadingOptions(false);
      }
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchOptions(debouncedSearch, pageIndex);
    } else {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
    }
  }, [isOpen, debouncedSearch, pageIndex]);

  const totalPages = useMemo(() => Math.ceil(totalCount / pageSize) || 1, [totalCount, pageSize]);
  const selectedBlueprint = options.find((b) => b.blueprintId === selectedBlueprintId) || null;

  const onConfirm = () => {
    handleCreateAndStart(selectedBlueprintId, onClose);
  };

  const handleSafeClose = () => {
    if (!isBusy) {
      onClose();
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={handleSafeClose}
      isCloseDisabled={isBusy}
      className="w-[92vw] max-w-[800px] max-h-[85vh] flex flex-col p-6"
    >
      <div className="flex flex-col h-full select-none">
        <DialogHeader className="shrink-0">
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">auto_awesome</span>
            Chọn cấu trúc đề thi
          </DialogTitle>
          <DialogDescription>
            Đề thi được cá nhân hóa dựa trên cấu trúc chuẩn và kết quả làm bài gần đây của em.
          </DialogDescription>
        </DialogHeader>

        {/* Search Bar - Header region */}
        <div className="shrink-0 mb-3 select-text">
          <div className="relative w-full">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-[18px] pointer-events-none">
              search
            </span>
            <input
              type="text"
              value={searchInput}
              disabled={isBusy}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Tìm kiếm theo tên cấu trúc đề..."
              aria-label="Tìm kiếm theo tên cấu trúc đề"
              className="w-full h-10 pl-9 pr-8 bg-surface-container-low border border-whisper-border rounded-xl text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
            />
            {searchInput && (
              <button
                type="button"
                disabled={isBusy}
                onClick={() => setSearchInput("")}
                aria-label="Xóa từ khóa tìm kiếm"
                className="absolute right-2.5 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-surface p-0.5 rounded cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">close</span>
              </button>
            )}
          </div>
        </div>

        {/* Scrollable Content Region */}
        <DialogContent className="flex-1 overflow-y-auto pr-1">
          <div className="flex flex-col gap-3.5 select-text">
            {/* Loading options */}
            {loadingOptions && (
              <div className="flex flex-col gap-2.5">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="p-3.5 bg-surface-container-low rounded-xl border border-whisper-border animate-pulse flex flex-col gap-2">
                    <div className="h-4 bg-surface-container-high rounded w-3/4"></div>
                    <div className="h-3 bg-surface-container rounded w-1/2"></div>
                  </div>
                ))}
              </div>
            )}

            {/* Error loading options */}
            {optionsError && !loadingOptions && (
              <div role="alert" className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                  <span>{optionsError}</span>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => fetchOptions(debouncedSearch, pageIndex)}
                  className="h-8 text-xs font-bold shrink-0"
                >
                  Thử lại
                </Button>
              </div>
            )}

            {/* Empty options */}
            {!loadingOptions && !optionsError && options.length === 0 && (
              <div className="bg-surface-container-low border border-whisper-border rounded-xl p-8 text-center text-on-surface-variant flex flex-col items-center justify-center gap-2">
                <span className="material-symbols-outlined text-[40px] text-outline-variant">assignment_late</span>
                <p className="text-sm font-bold text-on-surface">Chưa có cấu trúc đề thi nào phù hợp với khối lớp của bạn.</p>
                <p className="text-xs">
                  {debouncedSearch
                    ? "Không tìm thấy cấu trúc đề khớp với từ khóa tìm kiếm."
                    : "Vui lòng quay lại sau hoặc liên hệ giáo viên để biết thêm chi tiết."}
                </p>
              </div>
            )}

            {/* Blueprint list selection */}
            {!loadingOptions && !optionsError && options.length > 0 && (
              <div className="flex flex-col gap-2">
                <div className="flex flex-col gap-2 max-h-56 overflow-y-auto pr-1">
                  {options.map((bp) => {
                    const isSelected = bp.blueprintId === selectedBlueprintId;
                    return (
                      <button
                        key={bp.blueprintId}
                        type="button"
                        onClick={() => {
                          if (!isBusy && !generatedTestId) {
                            setSelectedBlueprintId(bp.blueprintId);
                            resetActionError();
                          }
                        }}
                        disabled={isBusy || !!generatedTestId}
                        className={cn(
                          "w-full text-left p-3 rounded-xl border transition-all flex items-center justify-between gap-3 cursor-pointer select-none",
                          isSelected
                            ? "bg-primary/5 border-primary shadow-sm ring-1 ring-primary/30"
                            : "bg-surface-container-low border-whisper-border hover:border-primary/40",
                          (isBusy || !!generatedTestId) && "cursor-not-allowed opacity-80"
                        )}
                      >
                        <div className="flex items-start gap-3 min-w-0 flex-1">
                          <div className={cn(
                            "w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 mt-0.5 transition-colors",
                            isSelected ? "border-primary bg-primary" : "border-outline-variant"
                          )}>
                            {isSelected && <div className="w-2 h-2 rounded-full bg-white"></div>}
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="text-xs font-bold text-on-surface truncate">
                                {bp.blueprintName}
                              </span>
                              <span className="bg-primary/10 text-primary border border-primary/20 text-[10px] font-extrabold px-1.5 py-0.2 rounded shrink-0">
                                Khối {bp.grade}
                              </span>
                            </div>
                            <div className="flex items-center gap-3 text-[11px] text-on-surface-variant mt-1">
                              <span>{bp.sectionCount} phần</span>
                              <span>·</span>
                              <span className="font-mono">{bp.totalQuestions} câu</span>
                              <span>·</span>
                              <span>{bp.durationMinutes === 0 ? "Không giới hạn" : `${bp.durationMinutes} phút`}</span>
                              <span>·</span>
                              <span className="font-mono font-bold text-primary">{bp.totalScore} điểm</span>
                            </div>
                          </div>
                        </div>
                      </button>
                    );
                  })}
                </div>

                {/* Pagination Controls */}
                <div className="flex items-center justify-between pt-2 border-t border-whisper-border text-xs text-on-surface-variant">
                  <span className="font-semibold">
                    {totalCount > 0 ? `Tổng cộng: ${totalCount} cấu trúc đề` : "0 cấu trúc đề"}
                    {totalPages > 1 && ` (Trang ${pageIndex}/${totalPages})`}
                  </span>
                  {totalPages > 1 && (
                    <div className="flex items-center gap-1.5">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={pageIndex <= 1 || loadingOptions || isBusy}
                        onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                        className="h-8 px-2.5 text-xs font-bold"
                      >
                        <span className="material-symbols-outlined text-[16px] mr-1">chevron_left</span>
                        Trước
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={pageIndex >= totalPages || loadingOptions || isBusy}
                        onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                        className="h-8 px-2.5 text-xs font-bold"
                      >
                        Tiếp
                        <span className="material-symbols-outlined text-[16px] ml-1">chevron_right</span>
                      </Button>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Natural explanation banner */}
            {selectedBlueprint && (
              <div className="bg-surface-container-low border border-whisper-border p-3.5 rounded-xl flex flex-col gap-2">
                <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                  <span className="material-symbols-outlined text-primary text-[18px]">psychology</span>
                  <span>Quy định đề thi theo năng lực</span>
                </div>
                <div className="flex flex-col gap-1 text-xs text-on-surface-variant leading-relaxed">
                  <div className="flex items-start gap-2">
                    <span className="material-symbols-outlined text-primary text-[16px] shrink-0 mt-0.5">check_circle</span>
                    <span>Cấu trúc đề thi (số phần, số câu hỏi, dạng bài và thời gian làm bài) được giữ nguyên theo chuẩn.</span>
                  </div>
                  <div className="flex items-start gap-2">
                    <span className="material-symbols-outlined text-primary text-[16px] shrink-0 mt-0.5">tune</span>
                    <span>Độ khó của một số câu hỏi có thể được tự động điều chỉnh phù hợp với kết quả học tập gần đây của em.</span>
                  </div>
                </div>
              </div>
            )}

            {/* Resume Session Banner */}
            {resumeSessionId && (
              <div className="p-3.5 bg-amber-500/10 border border-amber-500/20 rounded-xl text-on-surface flex items-start gap-2.5 text-xs">
                <span className="material-symbols-outlined text-amber-700 text-[20px] shrink-0 mt-0.5">pending_actions</span>
                <div>
                  <strong className="block font-bold text-amber-700 mb-0.5">Phiên làm bài chưa hoàn tất</strong>
                  <p className="text-on-surface-variant leading-relaxed">
                    Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này. Hãy chọn "Tiếp tục bài đang làm" để tiếp tục tiến trình của bạn.
                  </p>
                </div>
              </div>
            )}

            {/* Action Error Banner */}
            {actionError && !resumeSessionId && (
              <div role="alert" className="p-3.5 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-start gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
                <p className="flex-1 leading-relaxed">{actionError}</p>
              </div>
            )}
          </div>
        </DialogContent>

        {/* Footer */}
        <DialogFooter className="shrink-0">
          <Button
            type="button"
            variant="outline"
            disabled={isBusy}
            onClick={handleSafeClose}
            className="min-h-[44px]"
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="primary"
            disabled={loadingOptions || options.length === 0 || (!selectedBlueprintId && !generatedTestId) || isBusy}
            onClick={onConfirm}
            className="min-h-[44px] min-w-[160px] font-bold"
          >
            {generating ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang tạo đề...</span>
              </div>
            ) : starting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang bắt đầu...</span>
              </div>
            ) : resumeSessionId ? (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                <span>Tiếp tục bài đang làm</span>
              </div>
            ) : generatedTestId ? (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">refresh</span>
                <span>Thử bắt đầu lại</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">auto_awesome</span>
                <span>Tạo và bắt đầu</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
