/**
 * TabSwitchWarningModal
 * Shown when the student switches away from the test tab.
 * - isExamMode: shows incident count and stricter language.
 * - non-exam: friendly reminder only.
 */

export default function TabSwitchWarningModal({ isOpen, isExamMode, incidentCount, maxIncidents = 5, onClose }) {
  if (!isOpen) return null;

  const remaining = maxIncidents - incidentCount;

  return (
    <div
      className="fixed inset-0 z-[300] flex items-center justify-center"
      role="dialog"
      aria-modal="true"
      aria-labelledby="tab-warn-title"
    >
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onClose} />

      {/* Panel */}
      <div className="relative z-10 w-full max-w-md mx-4 bg-pure-surface rounded-2xl shadow-2xl overflow-hidden
        animate-[scaleIn_0.2s_ease-out]">

        {/* Top stripe */}
        <div className={`h-1.5 w-full ${isExamMode ? 'bg-deep-rose' : 'bg-amber-400'}`} />

        <div className="p-6">
          {/* Icon + title */}
          <div className="flex items-start gap-4 mb-4">
            <span
              className={`material-symbols-outlined text-4xl mt-0.5 ${isExamMode ? 'text-deep-rose' : 'text-amber-500'}`}
              style={{ fontVariationSettings: "'FILL' 1" }}
            >
              {isExamMode ? 'gpp_bad' : 'warning'}
            </span>
            <div>
              <h2 id="tab-warn-title" className="text-lg font-bold text-on-surface">
                {isExamMode ? 'Vi phạm quy chế thi!' : 'Cảnh báo chuyển tab'}
              </h2>
              <p className="text-sm text-on-surface-variant mt-1">
                {isExamMode
                  ? `Bạn đã rời khỏi trang thi. Hành vi này bị ghi lại.`
                  : 'Bạn vừa chuyển sang tab/cửa sổ khác trong khi đang làm bài.'}
              </p>
            </div>
          </div>

          {/* Exam mode: incident counter */}
          {isExamMode && (
            <div className={`rounded-xl px-4 py-3 mb-4 text-sm font-semibold flex items-center gap-3
              ${remaining <= 1
                ? 'bg-deep-rose/10 text-deep-rose border border-deep-rose/30'
                : 'bg-amber-400/10 text-amber-700 border border-amber-300'}`}
            >
              <span className="material-symbols-outlined text-xl">crisis_alert</span>
              <span>
                Lần vi phạm <strong>{incidentCount}/{maxIncidents}</strong>.&nbsp;
                {remaining > 0
                  ? <>Còn <strong>{remaining}</strong> lần trước khi bài thi bị nộp tự động.</>
                  : 'Đây là lần cuối cùng!'}
              </span>
            </div>
          )}

          {/* Advice */}
          <p className="text-sm text-on-surface-variant mb-5">
            {isExamMode
              ? 'Hãy tập trung vào bài thi. Tiếp tục vi phạm có thể dẫn đến nộp bài bắt buộc.'
              : 'Hãy quay lại và tiếp tục làm bài. Bài làm của bạn vẫn được lưu an toàn.'}
          </p>

          {/* CTA */}
          <button
            onClick={onClose}
            autoFocus
            className={`w-full py-3 rounded-xl font-bold text-sm text-white transition-all active:scale-[0.98]
              ${isExamMode ? 'bg-deep-rose hover:bg-deep-rose/90' : 'bg-primary hover:bg-primary/90'}`}
          >
            {isExamMode ? 'Tôi hiểu, quay lại thi' : 'Quay lại làm bài'}
          </button>
        </div>
      </div>
    </div>
  );
}
