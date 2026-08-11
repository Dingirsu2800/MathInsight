import React from "react";
import { cn } from "../../utils/cn";
import { getQuestionTypeLabel } from "../../utils/questionLabels";

export default function FixedExamRequirementPanel({
  blueprintDetails = [],
  selectedQuestions = [],
  activeDetailId,
  onSelectDetail
}) {
  // Count selected questions for each blueprintDetailId
  const countsByDetail = React.useMemo(() => {
    const map = {};
    selectedQuestions.forEach((q) => {
      if (q.blueprintDetailId) {
        map[q.blueprintDetailId] = (map[q.blueprintDetailId] || 0) + 1;
      }
    });
    return map;
  }, [selectedQuestions]);

  const totalRequired = blueprintDetails.reduce((acc, d) => acc + (d.quantity || 0), 0);
  const totalSelected = selectedQuestions.length;

  return (
    <div className="flex flex-col gap-3 bg-surface-container-low/50 border border-whisper-border rounded-xl p-4 select-none">
      <div className="flex items-center justify-between border-b border-whisper-border pb-3">
        <h3 className="text-xs font-bold text-on-surface uppercase tracking-wider flex items-center gap-2">
          <span className="material-symbols-outlined text-primary text-[18px]">grid_view</span>
          Ma trận yêu cầu ({totalSelected}/{totalRequired})
        </h3>
        <span className={cn(
          "px-2 py-0.5 rounded text-[10px] font-bold font-mono",
          totalSelected === totalRequired
            ? "bg-emerald-success/15 text-emerald-success border border-emerald-success/30"
            : "bg-amber-500/15 text-amber-700 border border-amber-500/30"
        )}>
          {totalSelected === totalRequired ? "Đủ chỉ tiêu" : "Chưa đủ câu"}
        </span>
      </div>

      <div className="flex flex-col gap-2 max-h-[480px] overflow-y-auto pr-1">
        {blueprintDetails.map((detail, index) => {
          const detailId = detail.blueprintDetailId || detail.id;
          const requiredCount = detail.quantity || 0;
          const selectedCount = countsByDetail[detailId] || 0;
          const isComplete = selectedCount === requiredCount;
          const isOverflow = selectedCount > requiredCount;
          const isActive = activeDetailId === detailId;

          return (
            <button
              key={detailId}
              type="button"
              onClick={() => onSelectDetail(detailId)}
              className={cn(
                "w-full text-left p-3 rounded-xl border transition-all cursor-pointer flex flex-col gap-1.5",
                isActive
                  ? "bg-pure-surface border-primary shadow-sm ring-1 ring-primary/30"
                  : "bg-pure-surface/70 border-whisper-border hover:border-primary/40"
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-bold text-on-surface flex items-center gap-1.5 truncate">
                  <span className="text-primary font-mono text-[11px]">#{index + 1}</span>
                  <span className="truncate">{detail.topicName || detail.tagName || "Chủ đề chưa gán"}</span>
                </span>
                <span className={cn(
                  "px-2 py-0.5 rounded text-[10px] font-bold font-mono shrink-0",
                  isComplete
                    ? "bg-emerald-success/15 text-emerald-success"
                    : isOverflow
                    ? "bg-error/15 text-error"
                    : "bg-surface-container text-on-surface-variant"
                )}>
                  {selectedCount}/{requiredCount} câu
                </span>
              </div>

              <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[10px] text-on-surface-variant">
                <span>{getQuestionTypeLabel(detail.questionType)}</span>
                <span>•</span>
                <span>{detail.difficultyName || "Độ khó chung"}</span>
                {detail.sectionName && (
                  <>
                    <span>•</span>
                    <span className="truncate max-w-[120px]">{detail.sectionName}</span>
                  </>
                )}
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
