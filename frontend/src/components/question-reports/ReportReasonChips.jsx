import React from 'react';

export const REPORT_REASONS = [
  'Nội dung sai hoặc thiếu',
  'Đáp án chưa chính xác',
  'Lời giải chưa phù hợp',
  'Công thức hoặc hình ảnh bị lỗi',
  'Sai chủ đề hoặc độ khó',
  'Khác',
];

export function formatReportReason(selectedReasons, otherText) {
  const lines = selectedReasons
    .filter((reason) => reason !== 'Khác')
    .map((reason) => `• ${reason}`);
  if (selectedReasons.includes('Khác') && otherText.trim()) {
    lines.push(`Chi tiết: ${otherText.trim()}`);
  }
  return lines.join('\n');
}

export default function ReportReasonChips({
  selectedReasons = [],
  onChange,
  otherText = '',
  onOtherTextChange,
  maxLength = 1000,
  className = '',
}) {
  const toggleReason = (reason) => {
    if (reason === 'Khác' && selectedReasons.includes(reason)) {
      onOtherTextChange('');
    }
    onChange(selectedReasons.includes(reason)
      ? selectedReasons.filter((item) => item !== reason)
      : [...selectedReasons, reason]);
  };

  const remainingLength = maxLength - (formatReportReason(selectedReasons, 'x').length - 1);

  return (
    <div className={className}>
      <div className="flex flex-wrap gap-1.5">
        {REPORT_REASONS.map((reason) => {
          const selected = selectedReasons.includes(reason);
          return (
            <button
              key={reason}
              type="button"
              aria-pressed={selected}
              onClick={() => toggleReason(reason)}
              className={`px-2.5 py-1 text-xs font-semibold rounded-lg border transition-colors cursor-pointer select-none ${selected
                ? 'border-primary bg-primary/10 text-primary'
                : 'border-whisper-border bg-surface-container-low hover:bg-surface-container hover:border-primary/40 text-on-surface-variant'}`}
            >
              {reason === 'Khác' ? 'Khác / bổ sung chi tiết' : reason}
            </button>
          );
        })}
      </div>
      {selectedReasons.includes('Khác') && (
        <div className="mt-3">
          <label htmlFor="report-other-detail" className="block text-sm font-semibold text-on-surface mb-1">
            Mô tả chi tiết
          </label>
          <textarea
            id="report-other-detail"
            rows={4}
            maxLength={Math.max(0, remainingLength)}
            value={otherText}
            onChange={(event) => onOtherTextChange(event.target.value)}
            className="w-full resize-y rounded-lg border border-outline-variant bg-pure-surface p-3 text-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
        </div>
      )}
    </div>
  );
}
