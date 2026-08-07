/**
 * Question navigation sidebar — shows numbered buttons for each question,
 * colored by answer status: answered (green), flagged (amber), current (primary), unanswered (gray).
 *
 * @param {{
 *   questions: Array<{ questionId: string, questionNo: number }>,
 *   answeredIds: Set<string>,
 *   flaggedIds: Set<string>,
 *   currentQuestionId: string,
 *   onSelect: (questionId: string) => void
 * }} props
 */
export default function QuestionNav({ questions, answeredIds, flaggedIds = new Set(), currentQuestionId, onSelect }) {
  const answeredCount = answeredIds.size;

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-4 shadow-sm">
      <div className="flex items-center justify-between mb-4">
        <h4 className="text-sm font-bold text-on-surface">Danh sách câu hỏi</h4>
        <span className="text-xs font-mono text-on-surface-variant">
          {answeredCount}/{questions.length}
        </span>
      </div>

      {/* Progress bar */}
      <div className="w-full h-1.5 bg-surface-container rounded-full mb-4 overflow-hidden">
        <div
          className="h-full bg-emerald-success rounded-full transition-all duration-300"
          style={{ width: `${questions.length > 0 ? (answeredCount / questions.length) * 100 : 0}%` }}
        />
      </div>

      {/* Question grid */}
      <div className="grid grid-cols-5 gap-2">
        {questions.map((q) => {
          const isCurrent = q.questionId === currentQuestionId;
          const isAnswered = answeredIds.has(q.questionId);
          const isFlagged = flaggedIds.has(q.questionId);

          let btnClass = 'border-whisper-border text-on-surface-variant bg-surface-container-low hover:bg-surface-container';
          if (isCurrent) {
            btnClass = 'border-primary bg-primary text-white shadow-sm';
          } else if (isFlagged && isAnswered) {
            btnClass = 'border-amber-400 bg-amber-400/10 text-amber-600';
          } else if (isFlagged) {
            btnClass = 'border-amber-400 bg-amber-400/10 text-amber-600';
          } else if (isAnswered) {
            btnClass = 'border-emerald-success/50 bg-emerald-success/10 text-emerald-success';
          }

          return (
            <button
              key={q.questionId}
              onClick={() => onSelect(q.questionId)}
              className={`relative w-full aspect-square rounded-lg border text-sm font-bold flex items-center justify-center transition-all hover:scale-105 ${btnClass}`}
            >
              {q.questionNo}
              {isFlagged && (
                <span
                  className="absolute -top-1 -right-1 w-3.5 h-3.5 flex items-center justify-center"
                  title="Phân vân"
                >
                  <span className="material-symbols-outlined text-[11px] text-amber-500" style={{ fontVariationSettings: "'FILL' 1" }}>flag</span>
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Legend */}
      <div className="mt-4 flex flex-wrap gap-3 text-[11px] text-on-surface-variant">
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded bg-primary" />
          Đang xem
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded bg-emerald-success/30 border border-emerald-success/50" />
          Đã trả lời
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded bg-amber-400/20 border border-amber-400" />
          Phân vân
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded bg-surface-container-low border border-whisper-border" />
          Chưa trả lời
        </div>
      </div>
    </div>
  );
}
