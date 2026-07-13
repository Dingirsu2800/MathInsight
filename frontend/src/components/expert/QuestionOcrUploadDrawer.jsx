import React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { cn } from "../../utils/cn";

export default function QuestionOcrUploadDrawer({
  isOpen,
  onClose,
  ocrFile,
  ocrPreviewUrl,
  ocrScanning,
  ocrScanError,
  onFileSelect,
  onFileClear,
  onScan,
  isOcrBusy
}) {
  const [isDragging, setIsDragging] = React.useState(false);
  const fileInputRef = React.useRef(null);

  const handleDragOver = (e) => {
    if (isOcrBusy) return;
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = () => {
    setIsDragging(false);
  };

  const handleDrop = (e) => {
    if (isOcrBusy) return;
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) {
      onFileSelect(file);
    }
  };

  const handleContainerClick = (e) => {
    // Avoid triggering input click if target is a button or is busy
    if (isOcrBusy) return;
    if (e.target.closest("button") || e.target.closest("input")) return;
    fileInputRef.current?.click();
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      onFileSelect(file);
      // Reset input value to allow selecting same file again
      e.target.value = "";
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={isOcrBusy ? () => {} : onClose}
      variant="drawer"
      className="max-w-lg h-full flex flex-col"
    >
      <DialogHeader className="shrink-0 pb-4 border-b border-outline-variant">
        <DialogTitle className="flex items-center gap-2 text-headline-sm text-primary">
          <span className="material-symbols-outlined text-[24px]">document_scanner</span>
          Tạo bản nháp từ ảnh đề
        </DialogTitle>
        <DialogDescription>
          Mistral OCR sẽ tự động nhận diện nội dung đề bài, đáp án và lời giải để bạn kiểm tra trước khi áp dụng vào câu hỏi.
        </DialogDescription>
      </DialogHeader>

      <DialogContent className="flex-1 overflow-y-auto p-6 space-y-5 font-semibold text-on-surface">
        {/* Upload Container */}
        <div
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={handleContainerClick}
          onKeyDown={(e) => {
            if (!isOcrBusy && (e.key === "Enter" || e.key === " ")) {
              e.preventDefault();
              fileInputRef.current?.click();
            }
          }}
          tabIndex={isOcrBusy ? -1 : 0}
          role="button"
          aria-label="Tải lên ảnh đề bài"
          className={cn(
            "border-2 border-dashed rounded-2xl p-6 text-center flex flex-col items-center justify-center gap-3 transition-all bg-pure-surface select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            isOcrBusy ? "opacity-60 cursor-not-allowed" : "cursor-pointer",
            isDragging ? "border-primary bg-primary/5 scale-[1.01]" : "border-outline-variant hover:border-primary/50",
            ocrFile && !isDragging ? "border-primary bg-primary/5" : ""
          )}
        >
          <span className={cn(
            "material-symbols-outlined text-[48px]",
            ocrFile || isDragging ? "text-primary animate-pulse" : "text-on-surface-variant"
          )}>
            add_photo_alternate
          </span>

          {ocrFile ? (
            <div className="space-y-1 w-full">
              <p className="text-sm font-bold text-on-surface truncate px-4">{ocrFile.name}</p>
              <p className="text-xs text-on-surface-variant font-medium">
                Dung lượng: {(ocrFile.size / 1024 / 1024).toFixed(2)} MB
              </p>
              {!isOcrBusy && (
                <span className="text-[10px] text-primary hover:underline font-bold mt-1.5 inline-block">
                  Kéo thả hoặc nhấp để thay ảnh mới
                </span>
              )}
            </div>
          ) : (
            <div className="space-y-1">
              <p className="text-sm font-bold text-on-surface">Kéo thả ảnh đề bài tại đây</p>
              <p className="text-xs text-on-surface-variant font-medium">hoặc nhấp chuột để duyệt tìm tệp tin</p>
            </div>
          )}

          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg, image/png, image/webp"
            onChange={handleFileChange}
            disabled={isOcrBusy}
            className="hidden"
            id="ocr-upload-file-picker"
          />
        </div>

        <p className="text-[10px] text-on-surface-variant/80 font-medium text-center">
          * Hỗ trợ tệp định dạng JPG, PNG, WebP (Tối đa 5MB)
        </p>

        {/* Scan Error Message */}
        {ocrScanError && (
          <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-xl flex items-start gap-2" role="alert">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span className="leading-relaxed font-semibold">{ocrScanError}</span>
          </div>
        )}

        {/* Thumbnail Preview Area */}
        {ocrPreviewUrl && (
          <div className="space-y-2">
            <div className="flex items-center justify-between border-b border-outline-variant pb-1.5 select-none">
              <h4 className="text-xs font-bold text-on-surface">Ảnh đề bài đã chọn:</h4>
              {!isOcrBusy && (
                <button
                  type="button"
                  onClick={onFileClear}
                  className="text-xs font-bold text-error hover:underline flex items-center gap-0.5 cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[16px]">delete</span>
                  Xóa ảnh
                </button>
              )}
            </div>
            <div className="border border-outline-variant rounded-xl p-2.5 bg-surface-container-low max-w-full flex items-center justify-center">
              <img
                src={ocrPreviewUrl}
                alt="OCR Upload Preview"
                className="max-h-64 max-w-full w-auto object-contain rounded shadow-sm bg-pure-surface"
              />
            </div>
          </div>
        )}
      </DialogContent>

      <DialogFooter className="shrink-0 pt-4 border-t border-outline-variant bg-surface-container-lowest flex items-center justify-between">
        <Button
          type="button"
          variant="outline"
          onClick={onClose}
          disabled={isOcrBusy}
          className="normal-case text-xs font-bold cursor-pointer"
        >
          Hủy
        </Button>
        <Button
          type="button"
          variant="primary"
          onClick={onScan}
          disabled={isOcrBusy || !ocrFile}
          className="normal-case text-xs font-bold flex items-center gap-1.5 cursor-pointer"
        >
          {ocrScanning ? (
            <>
              <span className="animate-spin w-4 h-4 border-2 border-white border-t-transparent rounded-full mr-0.5"></span>
              Đang nhận diện đề...
            </>
          ) : (
            <>
              <span className="material-symbols-outlined text-[16px]">document_scanner</span>
              Quét tạo bản nháp
            </>
          )}
        </Button>
      </DialogFooter>
    </Dialog>
  );
}
