import React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";

export default function TopicPracticeConfirmDialog({
  isOpen,
  onClose,
  topic,
  onConfirm,
  submitting,
  errorMessage
}) {
  if (!topic) return null;

  return (
    <Dialog isOpen={isOpen} onClose={() => !submitting && onClose()} isCloseDisabled={submitting}>
      <div className="flex flex-col h-full select-none">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-on-surface">
            <span className="material-symbols-outlined text-primary text-[22px]">fitness_center</span>
            Xác nhận tạo bài luyện tập
          </DialogTitle>
          <DialogDescription>
            Chủ đề: <span className="font-bold text-on-surface">{topic.tagName}</span>
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 select-text">
            {/* Business specs summary */}
            <div className="bg-surface-container-low p-4 rounded-xl border border-whisper-border flex flex-col gap-2.5">
              <h4 className="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-0.5">
                Quy định bài luyện tập
              </h4>
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
                <span>Tối đa 2 câu hỏi gồm nhiều mệnh đề (Composite)</span>
              </div>
            </div>

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
            disabled={submitting}
            onClick={onConfirm}
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
