import React, { useState, useEffect, useMemo } from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { cn } from "../../utils/cn";
import { getDifficultyLevelName } from "../../utils/questionLabels";

export default function TopicPracticeConfirmDialog({
  isOpen,
  onClose,
  topic,
  onConfirm,
  submitting,
  errorMessage
}) {
  const [mode, setMode] = useState("recommended"); // "recommended" | "manual"
  const [selectedDifficultyId, setSelectedDifficultyId] = useState(null);

  const availableLevels = useMemo(() => {
    return Array.isArray(topic?.difficultyAvailability) ? topic.difficultyAvailability : [];
  }, [topic]);

  const hasManualLevels = availableLevels.length > 0;

  // Reset states on modal open or topic change
  useEffect(() => {
    if (isOpen) {
      setMode("recommended");
      const firstAvailable = availableLevels.find((d) => d.canGenerate);
      setSelectedDifficultyId(firstAvailable ? firstAvailable.difficultyId : null);
    }
  }, [isOpen, topic, availableLevels]);

  if (!topic) return null;

  const handleConfirmClick = () => {
    if (mode === "manual") {
      if (!selectedDifficultyId) return;
      onConfirm({ tagId: topic.tagId, difficultyId: selectedDifficultyId });
    } else {
      onConfirm({ tagId: topic.tagId });
    }
  };

  const isManualDisabled = mode === "manual" && !selectedDifficultyId;

  return (
    <Dialog isOpen={isOpen} onClose={() => !submitting && onClose()} isCloseDisabled={submitting} className="max-w-lg">
      <div className="flex flex-col h-full select-none">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-on-surface">
            <span className="material-symbols-outlined text-primary text-[22px]">fitness_center</span>
            Tạo bài luyện tập chủ đề
          </DialogTitle>
          <DialogDescription>
            Chủ đề: <span className="font-bold text-on-surface">{topic.tagName}</span>
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 select-text">
            {/* Mode selection tabs if manual levels available */}
            {hasManualLevels && (
              <div className="flex flex-col gap-2">
                <label className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                  Chế độ chọn mức độ câu hỏi:
                </label>
                <div className="grid grid-cols-2 gap-2 bg-surface-container-low p-1 rounded-xl border border-whisper-border">
                  <button
                    type="button"
                    onClick={() => setMode("recommended")}
                    className={cn(
                      "py-2 px-3 rounded-lg text-xs font-bold transition-all cursor-pointer flex items-center justify-center gap-1.5",
                      mode === "recommended"
                        ? "bg-pure-surface text-primary shadow-sm border border-primary/20"
                        : "text-on-surface-variant hover:text-on-surface"
                    )}
                  >
                    <span className="material-symbols-outlined text-[16px]">auto_awesome</span>
                    <span>Mức phù hợp</span>
                    <span className="ml-1 px-1.5 py-0.2 text-[9px] rounded-full bg-primary/10 text-primary font-extrabold uppercase">
                      Khuyến nghị
                    </span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setMode("manual")}
                    className={cn(
                      "py-2 px-3 rounded-lg text-xs font-bold transition-all cursor-pointer flex items-center justify-center gap-1.5",
                      mode === "manual"
                        ? "bg-pure-surface text-primary shadow-sm border border-primary/20"
                        : "text-on-surface-variant hover:text-on-surface"
                    )}
                  >
                    <span className="material-symbols-outlined text-[16px]">tune</span>
                    Tự chọn độ khó
                  </button>
                </div>
              </div>
            )}

            {/* Recommended Mode Details */}
            {mode === "recommended" && (
              <div className="flex flex-col gap-3">
                <div className="bg-surface-container-low p-4 rounded-xl border border-whisper-border flex flex-col gap-2.5">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-0.5">
                    Quy định bài luyện tập
                  </h4>
                  <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                    <span className="material-symbols-outlined text-primary text-[18px]">psychology</span>
                    <span>
                      {topic.isWeakRecommended || (topic.recommendedDifficultyLevel !== null && topic.recommendedDifficultyLevel !== undefined)
                        ? "Hệ thống phân bổ câu hỏi dựa trên kết quả gần đây của em ở chủ đề này."
                        : "Em chưa có đủ kết quả ở chủ đề này nên hệ thống sử dụng mức độ tổng hợp."}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                    <span className="material-symbols-outlined text-primary text-[18px]">format_list_numbered</span>
                    <span>10 câu hỏi</span>
                  </div>
                  <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                    <span className="material-symbols-outlined text-primary text-[18px]">timer_off</span>
                    <span>Không giới hạn thời gian</span>
                  </div>
                  <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                    <span className="material-symbols-outlined text-primary text-[18px]">rule</span>
                    <span>Tối đa 2 câu hỏi gồm nhiều mệnh đề.</span>
                  </div>
                </div>

                {topic.isWeakRecommended && topic.weakTagName && topic.recommendedDifficultyLevel && (
                  <div className="border border-amber-500/30 bg-amber-500/10 px-4 py-3 rounded-xl text-xs leading-relaxed text-on-surface">
                    Bài luyện sẽ ưu tiên chủ đề <span className="font-bold">{topic.weakTagName}</span> ở mức độ <span className="font-bold">{getDifficultyLevelName(topic.recommendedDifficultyLevel)}</span>.
                    Hệ thống vẫn chọn đủ 10 câu trong phạm vi chủ đề bạn đã chọn.
                  </div>
                )}
              </div>
            )}

            {/* Manual Difficulty Level Selection */}
            {mode === "manual" && (
              <div className="flex flex-col gap-2.5">
                <p className="text-xs text-on-surface-variant leading-relaxed">
                  Vui lòng chọn 1 độ khó cụ thể bên dưới. Hệ thống sẽ sinh 10 câu hỏi đúng độ khó này:
                </p>
                <div className="grid grid-cols-1 gap-2">
                  {availableLevels.map((lvl) => {
                    const isSelected = selectedDifficultyId === lvl.difficultyId;
                    const canSelect = lvl.canGenerate;
                    const levelLabel = getDifficultyLevelName(lvl.levelValue, lvl.difficultyName);

                    return (
                      <button
                        key={lvl.difficultyId}
                        type="button"
                        disabled={!canSelect}
                        onClick={() => canSelect && setSelectedDifficultyId(lvl.difficultyId)}
                        className={cn(
                          "p-3 rounded-xl border flex items-center justify-between transition-all text-xs font-semibold cursor-pointer",
                          !canSelect
                            ? "bg-surface-container-low/40 border-whisper-border/60 text-on-surface-variant/50 cursor-not-allowed opacity-60"
                            : isSelected
                            ? "bg-primary/10 border-primary text-primary font-bold shadow-sm"
                            : "bg-pure-surface border-whisper-border hover:border-primary/40 text-on-surface"
                        )}
                      >
                        <div className="flex items-center gap-2">
                          <span className={cn(
                            "w-4 h-4 rounded-full border flex items-center justify-center text-[10px]",
                            isSelected ? "border-primary bg-primary text-white" : "border-outline-variant"
                          )}>
                            {isSelected && "✓"}
                          </span>
                          <span>Mức {lvl.levelValue}: {levelLabel}</span>
                        </div>

                        <span className={cn(
                          "px-2 py-0.5 rounded text-[10px] font-bold font-mono",
                          canSelect
                            ? isSelected ? "bg-primary text-on-primary" : "bg-surface-container text-on-surface-variant"
                            : "bg-error/10 text-error"
                        )}>
                          {canSelect ? `${lvl.availableQuestionCount} câu khả dụng` : `Chưa đủ 10 câu (${lvl.availableQuestionCount}/10)`}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Error Message */}
            {errorMessage && (
              <div role="alert" className="p-3.5 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-start gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
                <p className="flex-1 leading-relaxed">{errorMessage}</p>
              </div>
            )}
          </div>
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={onClose}
            className="min-h-[44px]"
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="primary"
            disabled={submitting || isManualDisabled}
            onClick={handleConfirmClick}
            className="min-h-[44px] min-w-[140px] font-bold"
          >
            {submitting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang tạo bài...</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                <span>Bắt đầu làm bài</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
