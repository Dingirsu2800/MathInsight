/**
 * Modal dialog to confirm test submission.
 * Warns the student about unanswered questions (BR-16b).
 *
 * @param {{
 *   isOpen: boolean,
 *   unansweredCount: number,
 *   totalQuestions: number,
 *   onConfirm: () => void,
 *   onCancel: () => void,
 *   submitting: boolean
 * }} props
 */
export default function SubmitConfirmModal({
  isOpen,
  unansweredCount,
  totalQuestions,
  onConfirm,
  onCancel,
  submitting,
}) {
  if (!isOpen) return null;

  const answeredCount = totalQuestions - unansweredCount;
  const allAnswered = unansweredCount === 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm" onClick={onCancel} />

      {/* Dialog */}
      <div className="relative bg-pure-surface border border-whisper-border rounded-2xl shadow-2xl w-full max-w-md mx-4 p-6 animate-in zoom-in-95">
        {/* Icon */}
        <div className={`mx-auto w-14 h-14 rounded-full flex items-center justify-center mb-4 ${
          allAnswered
            ? 'bg-emerald-success/10 text-emerald-success'
            : 'bg-amber-100 text-amber-600'
        }`}>
          <span className="material-symbols-outlined text-2xl">
            {allAnswered ? 'check_circle' : 'warning'}
          </span>
        </div>

        <h3 className="text-lg font-bold text-on-surface text-center mb-2">
          Xác nhận nộp bài
        </h3>

        {allAnswered ? (
          <p className="text-sm text-on-surface-variant text-center mb-6">
            Bạn đã trả lời <span className="font-bold text-emerald-success">{answeredCount}/{totalQuestions}</span> câu.
            Bạn có chắc chắn muốn nộp bài không?
          </p>
        ) : (
          <div className="mb-6">
            <p className="text-sm text-on-surface-variant text-center mb-3">
              Bạn còn <span className="font-bold text-amber-600">{unansweredCount}</span> câu chưa trả lời.
              Những câu chưa trả lời sẽ được tính là <span className="font-bold text-deep-rose">bỏ qua</span>.
            </p>
            <div className="flex items-center justify-center gap-4 text-sm">
              <div className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded bg-emerald-success/30" />
                <span className="text-on-surface-variant">Đã trả lời: <strong>{answeredCount}</strong></span>
              </div>
              <div className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded bg-surface-container border border-whisper-border" />
                <span className="text-on-surface-variant">Bỏ trống: <strong>{unansweredCount}</strong></span>
              </div>
            </div>
          </div>
        )}

        {/* Actions */}
        <div className="flex gap-3">
          <button
            onClick={onCancel}
            disabled={submitting}
            className="flex-1 px-4 py-2.5 rounded-xl border border-whisper-border text-sm font-bold text-on-surface-variant hover:bg-surface-container transition-colors disabled:opacity-50"
          >
            Quay lại
          </button>
          <button
            onClick={onConfirm}
            disabled={submitting}
            className="flex-1 px-4 py-2.5 rounded-xl bg-primary text-white text-sm font-bold hover:bg-primary/90 transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
          >
            {submitting ? (
              <>
                <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                Đang nộp...
              </>
            ) : (
              'Nộp bài'
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
