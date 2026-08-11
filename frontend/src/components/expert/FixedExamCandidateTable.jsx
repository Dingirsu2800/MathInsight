import React, { useState, useEffect } from "react";
import { Button } from "../ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import LatexPreview from "../expert/LatexPreview";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { cn } from "../../utils/cn";

export default function FixedExamCandidateTable({
  blueprintId,
  activeDetailId,
  activeDetail,
  isQuotaReached = false,
  selectedQuestions = [],
  onAddQuestion
}) {
  const [candidates, setCandidates] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Search & Pagination states
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [pageIndex, setPageIndex] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 5;

  // Detail preview modal state
  const [previewCandidate, setPreviewCandidate] = useState(null);

  const selectedQuestionIds = React.useMemo(() => {
    return new Set(selectedQuestions.map((q) => q.questionId));
  }, [selectedQuestions]);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  // Reset pagination on filter change
  useEffect(() => {
    setPageIndex(1);
  }, [activeDetailId, debouncedSearch]);

  // Load candidate questions with AbortController to prevent race conditions
  useEffect(() => {
    if (!blueprintId || !activeDetailId) {
      setCandidates([]);
      setTotalCount(0);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError("");

    testGeneratorApi.getFixedTestCandidates(
      blueprintId,
      {
        blueprintDetailId: activeDetailId,
        search: debouncedSearch.trim() || undefined,
        pageIndex,
        pageSize
      },
      { signal: controller.signal }
    )
      .then((res) => {
        const data = res.data || {};
        setCandidates(data.items || data.candidates || (Array.isArray(data) ? data : []));
        setTotalCount(data.totalCount || data.total || 0);
        setLoading(false);
      })
      .catch((err) => {
        if (err.name === "CanceledError" || err.name === "AbortError" || err.code === "ERR_CANCELED") {
          return;
        }
        console.error(err);
        setError(getTestGenErrorMessage(err, "Không thể tải danh sách câu hỏi phù hợp."));
        setLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [blueprintId, activeDetailId, debouncedSearch, pageIndex]);

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div className="flex flex-col gap-3 flex-1 select-none">
      {/* Header & Search */}
      <div className="flex items-center justify-between gap-3">
        <div className="relative flex-1">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-[18px] pointer-events-none">
            search
          </span>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm kiếm nội dung câu hỏi..."
            className="w-full h-9 pl-9 pr-4 bg-pure-surface border border-outline-variant rounded-lg text-xs font-semibold text-on-surface focus:outline-none focus:border-primary"
          />
        </div>
      </div>

      {/* Candidate List */}
      <div className="flex flex-col gap-2.5 min-h-[380px]">
        {!activeDetailId ? (
          <div className="p-8 text-center text-xs text-on-surface-variant bg-surface-container-low/30 rounded-xl border border-whisper-border">
            Chọn một dòng yêu cầu bên ma trận để hiển thị câu hỏi phù hợp.
          </div>
        ) : loading ? (
          <div className="p-8 text-center text-xs text-on-surface-variant">
            <div className="w-5 h-5 border-2 border-primary border-t-transparent rounded-full animate-spin inline-block mr-2" />
            Đang tìm câu hỏi phù hợp...
          </div>
        ) : error ? (
          <div className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold">
            {error}
          </div>
        ) : candidates.length === 0 ? (
          <div className="p-8 text-center text-xs text-on-surface-variant bg-surface-container-low/30 rounded-xl border border-whisper-border">
            Không tìm thấy câu hỏi đủ điều kiện nào cho phần ma trận này.
          </div>
        ) : (
          candidates.map((candidate) => {
            const qId = candidate.questionId || candidate.id;
            const isAdded = selectedQuestionIds.has(qId);
            const isDisabled = isAdded || isQuotaReached;
            const rawContent = candidate.content || candidate.questionContent || candidate.statement || "";
            const topicName = candidate.topicName || activeDetail?.tagName;
            const difficultyName = candidate.difficultyName || activeDetail?.difficultyName;

            return (
              <div
                key={qId}
                className={cn(
                  "p-3 rounded-xl border flex flex-col gap-2 transition-all select-text",
                  isAdded
                    ? "bg-surface-container-low/50 border-whisper-border opacity-60"
                    : isQuotaReached
                    ? "bg-surface-container-low/30 border-whisper-border opacity-75"
                    : "bg-pure-surface border-whisper-border hover:border-primary/40 shadow-sm"
                )}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 text-xs text-on-surface line-clamp-3 font-medium leading-relaxed">
                    <LatexPreview content={rawContent} />
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0 select-none">
                    <button
                      type="button"
                      onClick={() => setPreviewCandidate(candidate)}
                      className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded-lg transition-colors cursor-pointer"
                      title="Xem chi tiết câu hỏi"
                      aria-label="Xem chi tiết câu hỏi"
                    >
                      <span className="material-symbols-outlined text-[18px]">visibility</span>
                    </button>
                    <Button
                      type="button"
                      variant={isAdded ? "outline" : isQuotaReached ? "outline" : "primary"}
                      size="sm"
                      disabled={isDisabled}
                      onClick={() => onAddQuestion(candidate)}
                      className="h-8 text-xs font-bold px-3"
                    >
                      {isAdded ? (
                        <span className="flex items-center gap-1 text-emerald-success">
                          <span className="material-symbols-outlined text-[16px]">check</span>
                          Đã chọn
                        </span>
                      ) : isQuotaReached ? (
                        <span className="flex items-center gap-1 text-on-surface-variant">
                          <span className="material-symbols-outlined text-[16px]">block</span>
                          Đã đủ số câu
                        </span>
                      ) : (
                        <span className="flex items-center gap-1">
                          <span className="material-symbols-outlined text-[16px]">add</span>
                          Thêm vào đề
                        </span>
                      )}
                    </Button>
                  </div>
                </div>

                {candidate.pictureUrl && (
                  <div className="my-1 max-w-[200px] overflow-hidden rounded border border-whisper-border">
                    <img src={candidate.pictureUrl} alt="Minh họa" className="max-h-24 object-contain" />
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2 text-[10px] text-on-surface-variant font-medium select-none">
                  {topicName && (
                    <span className="px-2 py-0.5 rounded bg-surface-container text-on-surface-variant">
                      {topicName}
                    </span>
                  )}
                  {difficultyName && (
                    <span className="px-2 py-0.5 rounded bg-surface-container text-on-surface-variant">
                      {difficultyName}
                    </span>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between border-t border-whisper-border pt-2 text-xs select-none">
          <span className="text-on-surface-variant font-medium text-[11px]">
            Trang {pageIndex} / {totalPages} ({totalCount} câu)
          </span>
          <div className="flex gap-1">
            <Button
              variant="outline"
              size="sm"
              disabled={pageIndex <= 1}
              onClick={() => setPageIndex(p => Math.max(1, p - 1))}
              className="h-7 text-xs px-2 font-bold"
            >
              Trước
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={pageIndex >= totalPages}
              onClick={() => setPageIndex(p => Math.min(totalPages, p + 1))}
              className="h-7 text-xs px-2 font-bold"
            >
              Sau
            </Button>
          </div>
        </div>
      )}

      {/* Candidate Full Preview Modal */}
      <Dialog
        isOpen={Boolean(previewCandidate)}
        onClose={() => setPreviewCandidate(null)}
        className="max-w-2xl"
      >
        {previewCandidate && (
          <>
            <DialogHeader>
              <DialogTitle className="text-sm font-bold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined text-primary text-[20px]">visibility</span>
                Chi tiết câu hỏi khả dụng
              </DialogTitle>
              <DialogDescription>
                Xem đầy đủ nội dung câu hỏi, hình ảnh và thuộc tính trước khi thêm vào đề thi.
              </DialogDescription>
            </DialogHeader>

            <DialogContent className="space-y-4 text-xs select-text">
              <div>
                <h4 className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Nội dung câu hỏi:</h4>
                <div className="p-3.5 bg-surface-container-low rounded-xl border border-whisper-border text-xs leading-relaxed">
                  <LatexPreview content={previewCandidate.content || previewCandidate.questionContent || previewCandidate.statement || ""} />
                </div>
              </div>

              {previewCandidate.pictureUrl && (
                <div className="rounded-xl overflow-hidden border border-whisper-border text-center p-2 bg-pure-surface">
                  <img src={previewCandidate.pictureUrl} alt="Hình minh họa" className="max-h-48 mx-auto object-contain" />
                </div>
              )}

              <div className="flex flex-wrap gap-2 text-xs">
                {(previewCandidate.topicName || activeDetail?.tagName) && (
                  <span className="px-2.5 py-1 rounded-lg bg-primary/10 text-primary font-bold">
                    Chủ đề: {previewCandidate.topicName || activeDetail?.tagName}
                  </span>
                )}
                {(previewCandidate.difficultyName || activeDetail?.difficultyName) && (
                  <span className="px-2.5 py-1 rounded-lg bg-surface-container text-on-surface-variant font-bold">
                    Độ khó: {previewCandidate.difficultyName || activeDetail?.difficultyName}
                  </span>
                )}
              </div>
            </DialogContent>

            <DialogFooter>
              <Button variant="outline" size="sm" onClick={() => setPreviewCandidate(null)}>
                Đóng
              </Button>
              <Button
                variant="primary"
                size="sm"
                disabled={selectedQuestionIds.has(previewCandidate.questionId || previewCandidate.id) || isQuotaReached}
                onClick={() => {
                  onAddQuestion(previewCandidate);
                  setPreviewCandidate(null);
                }}
              >
                {selectedQuestionIds.has(previewCandidate.questionId || previewCandidate.id)
                  ? "Đã chọn"
                  : isQuotaReached
                  ? "Đã đủ số câu"
                  : "Thêm vào đề"}
              </Button>
            </DialogFooter>
          </>
        )}
      </Dialog>
    </div>
  );
}
