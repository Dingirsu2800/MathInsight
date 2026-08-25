import React from 'react';

export const REPORT_REASONS = [
  'Nội dung sai hoặc thiếu',
  'Đáp án chưa chính xác',
  'Lời giải chưa phù hợp',
  'Công thức hoặc hình ảnh bị lỗi',
  'Sai chủ đề hoặc độ khó',
  'Khác',
];

export default function ReportReasonChips({ value = '', onChange, className = '' }) {
  const handleChipClick = (reason) => {
    if (reason === 'Khác') {
      return;
    }

    const currentText = (value || '').trim();
    if (!currentText) {
      onChange(reason);
      return;
    }

    const existingParts = currentText.split(';').map((s) => s.trim());
    if (existingParts.includes(reason)) {
      return;
    }

    const separator = currentText.endsWith(';') ? ' ' : '; ';
    onChange(`${currentText}${separator}${reason}`);
  };

  return (
    <div className={`flex flex-wrap gap-1.5 ${className}`}>
      {REPORT_REASONS.map((reason) => (
        <button
          key={reason}
          type="button"
          onClick={() => handleChipClick(reason)}
          className="px-2.5 py-1 text-xs font-semibold rounded-lg border border-whisper-border bg-surface-container-low hover:bg-surface-container hover:border-primary/40 text-on-surface-variant transition-colors cursor-pointer select-none"
        >
          {reason}
        </button>
      ))}
    </div>
  );
}
