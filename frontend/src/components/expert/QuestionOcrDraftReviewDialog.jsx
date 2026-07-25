import React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { CustomSelect } from "../ui/custom-select";
import LatexPreview from "./LatexPreview";
import { cn } from "../../utils/cn";
import { getQuestionTypeLabel } from "../../utils/questionLabels";

function clamp(value) {
  return Math.min(1, Math.max(0, value));
}

function ManualImageCropSelector({ sourceUrl, selection: selectedCrop, onSelectionChange, disabled }) {
  const containerRef = React.useRef(null);
  const dragStartRef = React.useRef(null);
  const [selection, setSelection] = React.useState(null);

  React.useEffect(() => {
    setSelection(null);
  }, [sourceUrl]);

  React.useEffect(() => {
    setSelection(selectedCrop);
  }, [selectedCrop]);

  const getPointerPosition = (event) => {
    const bounds = containerRef.current?.getBoundingClientRect();
    if (!bounds) return null;

    return {
      x: clamp((event.clientX - bounds.left) / bounds.width),
      y: clamp((event.clientY - bounds.top) / bounds.height)
    };
  };

  const updateSelection = (end) => {
    const start = dragStartRef.current;
    if (!start || !end) return null;

    return {
      x: Math.min(start.x, end.x),
      y: Math.min(start.y, end.y),
      width: Math.abs(end.x - start.x),
      height: Math.abs(end.y - start.y)
    };
  };

  const handlePointerDown = (event) => {
    if (disabled) return;

    const start = getPointerPosition(event);
    if (!start) return;

    event.currentTarget.setPointerCapture(event.pointerId);
    dragStartRef.current = start;
    setSelection({ x: start.x, y: start.y, width: 0, height: 0 });
  };

  const handlePointerMove = (event) => {
    if (!dragStartRef.current) return;
    const nextSelection = updateSelection(getPointerPosition(event));
    if (nextSelection) setSelection(nextSelection);
  };

  const handlePointerUp = (event) => {
    if (!dragStartRef.current) return;

    const nextSelection = updateSelection(getPointerPosition(event));
    dragStartRef.current = null;
    if (!nextSelection || nextSelection.width < 0.03 || nextSelection.height < 0.03) {
      setSelection(null);
      onSelectionChange(null);
      return;
    }

    setSelection(nextSelection);
    onSelectionChange(nextSelection);
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h4 className="text-xs font-bold text-on-surface">Cắt hình thủ công:</h4>
          <p className="text-[10px] text-on-surface-variant">Kéo khung quanh hình minh họa cần đính kèm.</p>
        </div>
        {selection && (
          <button
            type="button"
            onClick={() => {
              setSelection(null);
              onSelectionChange(null);
            }}
            disabled={disabled}
            className="text-[10px] font-bold text-primary hover:underline disabled:cursor-not-allowed"
          >
            Xóa vùng cắt
          </button>
        )}
      </div>
      <div
        ref={containerRef}
        className="relative overflow-hidden rounded-lg border border-dashed border-primary/50 bg-surface-container-low touch-none"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerCancel={handlePointerUp}
      >
        <img src={sourceUrl} alt="Chọn vùng cắt" draggable={false} className="block w-full select-none" />
        {selection && (
          <div
            className="pointer-events-none absolute border-2 border-primary bg-primary/15"
            style={{
              left: `${selection.x * 100}%`,
              top: `${selection.y * 100}%`,
              width: `${selection.width * 100}%`,
              height: `${selection.height * 100}%`
            }}
          />
        )}
      </div>
    </div>
  );
}

export default function QuestionOcrDraftReviewDialog({
  isOpen,
  onClose,
  ocrResult,
  reviewDraft,
  setReviewDraft,
  attachSourceImage,
  setAttachSourceImage,
  selectedExtractedImageId,
  setSelectedExtractedImageId,
  manualCropSelection,
  setManualCropSelection,
  ocrImageUploading,
  ocrImageUploadError,
  onApplyDraft,
  ocrPreviewUrl,
  isOcrBusy
}) {
  const [isDiagnosticOpen, setIsDiagnosticOpen] = React.useState(false);

  if (!ocrResult || !reviewDraft) return null;

  const suggestedType = reviewDraft.suggestedQuestionType || "UNKNOWN";
  const confidence = ocrResult.pageConfidence ? Math.round(ocrResult.pageConfidence * 100) : null;
  const isTypeUnknown = suggestedType === "UNKNOWN";
  const extractedImages = ocrResult.extractedImages || [];

  const handlePartTypeChange = (idx, newType) => {
    const nextParts = [...(reviewDraft.parts || [])];
    nextParts[idx] = {
      ...nextParts[idx],
      partType: newType
    };
    setReviewDraft(prev => ({
      ...prev,
      parts: nextParts
    }));
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={isOcrBusy ? () => {} : onClose}
      variant="drawer"
      className="max-w-2xl h-full flex flex-col"
    >
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2 text-headline-sm">
          <span className="material-symbols-outlined text-[24px] text-primary">preview</span>
          Xem trước & Hiệu chỉnh bản nháp OCR
        </DialogTitle>
        <DialogDescription>
          Kiểm tra kỹ nội dung đã nhận diện trước khi áp dụng vào biểu mẫu.
        </DialogDescription>
      </DialogHeader>

      <DialogContent className="space-y-5 pr-2">
        {/* Source Image Preview */}
        <div className="space-y-1.5">
          <h4 className="text-xs font-bold text-on-surface">Ảnh gốc đã quét:</h4>
          {ocrPreviewUrl && (
            <div className="border border-whisper-border rounded-xl overflow-hidden bg-surface-container-low p-2 max-w-md mx-auto">
              <img src={ocrPreviewUrl} alt="Source OCR Preview" className="max-h-48 object-contain mx-auto rounded" />
            </div>
          )}
        </div>

        {ocrPreviewUrl && (
          <ManualImageCropSelector
            sourceUrl={ocrPreviewUrl}
            selection={manualCropSelection}
            onSelectionChange={setManualCropSelection}
            disabled={isOcrBusy}
          />
        )}

        {extractedImages.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h4 className="text-xs font-bold text-on-surface">Hình minh họa được phát hiện:</h4>
                <p className="text-[10px] text-on-surface-variant">Chọn tối đa một hình để đính kèm khi áp dụng bản nháp.</p>
              </div>
              {selectedExtractedImageId && (
                <button
                  type="button"
                  onClick={() => setSelectedExtractedImageId(null)}
                  disabled={isOcrBusy}
                  className="text-[10px] font-bold text-primary hover:underline disabled:cursor-not-allowed"
                >
                  Bỏ chọn
                </button>
              )}
              {manualCropSelection && (
                <span className="text-[10px] font-bold text-primary">Đang dùng vùng cắt thủ công</span>
              )}
            </div>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {extractedImages.map((image, index) => {
                const isSelected = image.id === selectedExtractedImageId;
                return (
                  <button
                    key={image.id}
                    type="button"
                    onClick={() => setSelectedExtractedImageId(isSelected ? null : image.id)}
                    disabled={isOcrBusy}
                    className={cn(
                      "overflow-hidden rounded-lg border p-2 text-left transition-colors disabled:cursor-not-allowed",
                      isSelected
                        ? "border-primary bg-primary/5 ring-1 ring-primary"
                        : "border-whisper-border bg-pure-surface hover:border-primary/50"
                    )}
                  >
                    <img
                      src={image.dataUrl}
                      alt={`Hình minh họa OCR ${index + 1}`}
                      className="h-28 w-full rounded object-contain bg-surface-container-low"
                    />
                    <span className="mt-1 block text-[10px] font-bold text-on-surface">
                      {isSelected ? "Sẽ đính kèm vào câu hỏi" : `Hình ${index + 1}`}
                    </span>
                    {image.annotation && (
                      <span className="block truncate text-[10px] text-on-surface-variant" title={image.annotation}>
                        {image.annotation}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* Suggested Type & Confidence Info */}
        <div className="p-3 bg-surface-container rounded-xl flex flex-wrap items-center justify-between gap-3 border border-whisper-border/60">
          <div>
            <p className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Loại câu hỏi gợi ý từ OCR</p>
            <div className="mt-1 flex items-center gap-2">
              <span className={cn(
                "px-2.5 py-0.5 rounded-full text-xs font-bold border",
                isTypeUnknown
                  ? "bg-error/5 border-error/20 text-error"
                  : "bg-primary/5 border-primary/20 text-primary"
              )}>
                {isTypeUnknown ? "Không xác định" : getQuestionTypeLabel(suggestedType)}
              </span>
              {confidence !== null && (
                <span className="text-[10px] text-on-surface-variant font-medium">
                  Độ tin cậy: <strong className="text-on-surface">{confidence}%</strong>
                </span>
              )}
            </div>
          </div>

          {/* Let Expert adjust type within the draft context to preview fields */}
          <div className="w-52">
            <span className="text-[10px] font-bold text-on-surface-variant block mb-1">Thay đổi loại câu hỏi bản nháp:</span>
            <CustomSelect
              value={suggestedType}
              onValueChange={(val) => setReviewDraft(prev => ({ ...prev, suggestedQuestionType: val }))}
              items={[
                { value: "SINGLE_CHOICE", label: "Trắc nghiệm một đáp án" },
                { value: "MULTIPLE_CHOICE", label: "Trắc nghiệm nhiều đáp án" },
                { value: "TRUE_FALSE", label: "Đúng / Sai" },
                { value: "SHORT_ANSWER", label: "Trả lời ngắn" },
                { value: "COMPOSITE", label: "Nhiều mệnh đề" },
                { value: "UNKNOWN", label: "Không xác định" }
              ]}
              disabled={isOcrBusy}
            />
          </div>
        </div>

        {/* Warnings List */}
        {ocrResult.warnings && ocrResult.warnings.length > 0 && (
          <div className="p-3 bg-amber-warning/10 border border-amber-warning/30 text-amber-warning rounded-lg space-y-1 text-xs">
            <h5 className="font-bold flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[16px]">warning</span>
              Cảnh báo từ hệ thống OCR:
            </h5>
            <ul className="list-disc pl-4 space-y-0.5">
              {ocrResult.warnings.map((w, idx) => (
                <li key={idx}>{w}</li>
              ))}
            </ul>
          </div>
        )}

        {/* Editable Stem */}
        <div className="space-y-1.5">
          <label className="text-xs font-bold text-on-surface flex items-center gap-1">
            Nội dung câu hỏi:
            <span className="text-[10px] font-normal text-on-surface-variant">(Hỗ trợ mã LaTeX)</span>
          </label>
          <textarea
            value={reviewDraft.questionContent || ""}
            onChange={(e) => setReviewDraft(prev => ({ ...prev, questionContent: e.target.value }))}
            rows={4}
            className="w-full text-xs border border-whisper-border rounded-xl p-3 bg-pure-surface focus:outline-none focus:border-primary font-mono leading-relaxed"
            placeholder="Nhập đề bài..."
            disabled={isOcrBusy}
          />
          {reviewDraft.questionContent && (
            <div className="p-3 bg-surface-container rounded-lg border border-whisper-border/40 space-y-1">
              <span className="text-[10px] font-bold text-on-surface-variant block">Xem trước đề bài:</span>
              <div className="text-xs text-on-surface break-words">
                <LatexPreview content={reviewDraft.questionContent} />
              </div>
            </div>
          )}
        </div>

        {/* Editable Solution */}
        <div className="space-y-1.5">
          <label className="text-xs font-bold text-on-surface flex items-center gap-1">
            Lời giải chi tiết:
            <span className="text-[10px] font-normal text-on-surface-variant">(Tùy chọn)</span>
          </label>
          <textarea
            value={reviewDraft.solutionContent || ""}
            onChange={(e) => setReviewDraft(prev => ({ ...prev, solutionContent: e.target.value }))}
            rows={3}
            className="w-full text-xs border border-whisper-border rounded-xl p-3 bg-pure-surface focus:outline-none focus:border-primary font-mono leading-relaxed"
            placeholder="Nhập hướng dẫn giải..."
            disabled={isOcrBusy}
          />
          {reviewDraft.solutionContent && (
            <div className="p-3 bg-surface-container rounded-lg border border-whisper-border/40 space-y-1">
              <span className="text-[10px] font-bold text-on-surface-variant block">Xem trước lời giải:</span>
              <div className="text-xs text-on-surface break-words">
                <LatexPreview content={reviewDraft.solutionContent} />
              </div>
            </div>
          )}
        </div>

        {/* Editable Answers options */}
        {(suggestedType === "SINGLE_CHOICE" || suggestedType === "MULTIPLE_CHOICE") && (
          <div className="space-y-2.5">
            <div className="flex items-center justify-between border-b border-whisper-border pb-1">
              <label className="text-xs font-bold text-on-surface">Các phương án trả lời:</label>
              <span className="text-[10px] text-on-surface-variant italic">Đáp án đúng sẽ được để trống khi áp dụng</span>
            </div>
            <div className="space-y-2">
              {(reviewDraft.answers || []).map((ans, idx) => (
                <div key={idx} className="flex items-center gap-2">
                  <span className="text-xs font-mono font-bold text-on-surface-variant w-5 shrink-0">
                    {String.fromCharCode(65 + idx)}.
                  </span>
                  <input
                    type="text"
                    value={ans.content || ""}
                    onChange={(e) => {
                      const nextAnswers = [...(reviewDraft.answers || [])];
                      nextAnswers[idx] = { ...nextAnswers[idx], content: e.target.value };
                      setReviewDraft(prev => ({ ...prev, answers: nextAnswers }));
                    }}
                    className="flex-1 text-xs border border-whisper-border rounded-lg px-3 py-1.5 bg-pure-surface focus:outline-none focus:border-primary"
                    placeholder="Nội dung phương án..."
                    disabled={isOcrBusy}
                  />
                  {ans.suggestedIsCorrect && (
                    <span className="text-[10px] text-emerald-success bg-emerald-success/10 px-2 py-0.5 rounded font-bold shrink-0">
                      Gợi ý đúng
                    </span>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Editable Parts list for COMPOSITE */}
        {suggestedType === "COMPOSITE" && (
          <div className="space-y-3.5">
            <div className="flex items-center justify-between border-b border-whisper-border pb-1">
              <label className="text-xs font-bold text-on-surface">Danh sách các mệnh đề:</label>
              <span className="text-[10px] text-on-surface-variant italic">Dữ liệu đáp án mệnh đề sẽ do Expert tự nhập</span>
            </div>
            <div className="space-y-3">
              {(reviewDraft.parts || []).map((part, idx) => {
                const isPartUnknown = part.partType === "UNKNOWN";
                return (
                  <div key={idx} className="p-3 bg-surface-container rounded-xl border border-whisper-border/50 space-y-2.5">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-bold text-primary">Mệnh đề {part.label || String.fromCharCode(97 + idx)}:</span>
                      <div className="w-40">
                        <CustomSelect
                          value={part.partType}
                          onValueChange={(val) => handlePartTypeChange(idx, val)}
                          items={[
                            { value: "TRUE_FALSE", label: "Đúng / Sai" },
                            { value: "SHORT_ANSWER", label: "Trả lời ngắn" },
                            { value: "NUMERIC_ANSWER", label: "Điền số" },
                            { value: "UNKNOWN", label: "Không xác định" }
                          ]}
                          disabled={isOcrBusy}
                        />
                      </div>
                    </div>
                    <textarea
                      value={part.content || ""}
                      onChange={(e) => {
                        const nextParts = [...(reviewDraft.parts || [])];
                        nextParts[idx] = { ...nextParts[idx], content: e.target.value };
                        setReviewDraft(prev => ({ ...prev, parts: nextParts }));
                      }}
                      rows={2}
                      className="w-full text-xs border border-whisper-border rounded-lg p-2 bg-pure-surface focus:outline-none focus:border-primary font-mono"
                      placeholder="Nội dung mệnh đề..."
                      disabled={isOcrBusy}
                    />
                    {isPartUnknown && (
                      <p className="text-[10px] text-error italic flex items-center gap-1">
                        <span className="material-symbols-outlined text-[12px]">info</span>
                        Loại mệnh đề không xác định và sẽ bị bỏ qua khi áp dụng.
                      </p>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Collapsible Diagnostic Original Markdown section */}
        <details className="border border-whisper-border rounded-lg bg-surface-container-low p-3 text-xs">
          <summary className="font-bold text-on-surface cursor-pointer select-none outline-none">
            Mã Markdown gốc từ Mistral OCR (Chẩn đoán)
          </summary>
          <div className="mt-2 p-3 bg-charcoal-ink text-inverse-on-surface rounded-lg font-mono text-[11px] overflow-x-auto whitespace-pre-wrap leading-relaxed shadow-inner max-h-48">
            {ocrResult.rawMarkdown || "Không có dữ liệu Markdown."}
          </div>
        </details>
      </DialogContent>

      <DialogFooter className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-t border-whisper-border pt-4 mt-auto">
        {/* Source Image Checkbox & Upload Errors */}
        <div className="flex flex-col gap-1.5 max-w-sm">
          <label className="flex items-center gap-2 text-xs font-semibold text-on-surface cursor-pointer select-none">
            <input
              type="checkbox"
              checked={attachSourceImage}
              onChange={(e) => setAttachSourceImage(e.target.checked)}
              disabled={isOcrBusy}
              className="w-4 h-4 rounded text-primary focus:ring-primary border-outline-variant cursor-pointer disabled:cursor-not-allowed"
            />
            <span>Đính kèm toàn bộ ảnh nguồn vào câu hỏi</span>
          </label>

          {ocrImageUploadError && (
            <p className="text-[10px] text-error flex items-start gap-1 font-medium" role="alert">
              <span className="material-symbols-outlined text-[14px] shrink-0 mt-0.5">error</span>
              <span>{ocrImageUploadError}</span>
            </p>
          )}
        </div>

        {/* Modal Buttons */}
        <div className="flex items-center gap-2 justify-end shrink-0">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={isOcrBusy}
            className="normal-case text-xs font-bold"
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="secondary"
            onClick={() => onApplyDraft("content")}
            disabled={isOcrBusy}
            className="normal-case text-xs font-bold"
          >
            {ocrImageUploading ? "Đang xử lý..." : "Áp dụng nội dung"}
          </Button>

          <Button
            type="button"
            variant="primary"
            onClick={() => onApplyDraft("full")}
            disabled={isOcrBusy || isTypeUnknown}
            className="normal-case text-xs font-bold"
            title={isTypeUnknown ? "Không thể áp dụng toàn bộ vì loại câu hỏi không xác định" : ""}
          >
            {ocrImageUploading ? "Đang xử lý..." : "Áp dụng bản nháp"}
          </Button>
        </div>
      </DialogFooter>
    </Dialog>
  );
}
