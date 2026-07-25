import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';

/**
 * Individual MCQ question result card with expandable solution.
 * @param {{ index: number, question: string, difficulty: string, difficultyClass: string, options: Array<{label: string, text: string, isCorrect?: boolean, isSelected?: boolean}>, solution: string[] }} props
 */
export default function QuestionAnswerCard({
  index,
  question,
  difficulty,
  difficultyClass = 'bg-primary-fixed text-primary',
  options = [],
  solution = [],
  machinePoints = 0,
  effectivePoints = 0,
  maxPoints = 0,
  isScoreInvalidated = false,
  reportReason,
  scoreAdjustedTime,
  onReport,
}) {
  const [showSolution, setShowSolution] = useState(false);

  return (
    <div className="bg-pure-surface rounded-xl border border-whisper-border overflow-hidden shadow-sm">
      {/* Header */}
      <div className="p-4 bg-surface-container-low border-b border-whisper-border flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span className="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center font-bold text-sm">
            {index}
          </span>
          <span className="text-sm font-bold text-on-surface-variant">Câu hỏi trắc nghiệm</span>
        </div>
        <span className={`px-2 py-0.5 text-[11px] font-bold rounded ${difficultyClass}`}>
          {difficulty}
        </span>
      </div>

      {/* Body */}
      <div className="p-6">
        {isScoreInvalidated && (
          <div className="mb-4 border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
            <div className="flex items-center gap-2 font-bold">
              <MaterialIcon name="warning" size={18} />
              Câu hỏi đã bị vô hiệu hóa sau khi chấm
            </div>
            <p className="mt-1">{reportReason || 'Câu hỏi hoặc đáp án của phiên bản này đã được xác nhận có lỗi.'}</p>
            <p className="mt-2 font-mono text-xs">
              Điểm máy chấm: {Number(machinePoints).toFixed(2)} · Điểm hiệu lực: {Number(effectivePoints).toFixed(2)} / {Number(maxPoints).toFixed(2)}
            </p>
            {scoreAdjustedTime && (
              <p className="mt-1 text-xs">Điều chỉnh lúc {new Date(scoreAdjustedTime).toLocaleString('vi-VN')}</p>
            )}
          </div>
        )}
        <p className="text-base mb-4 text-on-surface">{question}</p>

        {/* Options grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {options.map((opt) => {
            let borderClass = 'border-whisper-border';
            let bgClass = '';
            let iconEl = null;

            if (opt.isCorrect && opt.isSelected) {
              borderClass = 'border-emerald-success/30';
              bgClass = 'bg-emerald-success/10';
              iconEl = <MaterialIcon name="check_circle" className="text-emerald-success" />;
            } else if (opt.isCorrect && !opt.isSelected) {
              borderClass = 'border-emerald-success/30';
              bgClass = 'bg-emerald-success/5';
              iconEl = <MaterialIcon name="check_circle" className="text-emerald-success/60" />;
            } else if (!opt.isCorrect && opt.isSelected) {
              borderClass = 'border-deep-rose/30';
              bgClass = 'bg-deep-rose/10';
              iconEl = <MaterialIcon name="cancel" className="text-deep-rose" />;
            }

            return (
              <div
                key={opt.label}
                className={`p-4 border rounded-lg flex items-center justify-between text-sm ${borderClass} ${bgClass}`}
              >
                <span>{opt.label}. {opt.text}</span>
                {iconEl}
              </div>
            );
          })}
        </div>

        {/* Solution toggle */}
        <div className="mt-6 pt-6 border-t border-whisper-border">
          <div className="mb-4 flex items-center justify-between gap-4 text-sm">
            <span className="text-on-surface-variant">Điểm</span>
            <span className="font-mono font-bold text-primary">
              {Number(effectivePoints).toFixed(2)} / {Number(maxPoints).toFixed(2)}
            </span>
          </div>
          <button
            className="flex items-center gap-2 text-primary font-bold text-sm hover:underline"
            onClick={() => setShowSolution(!showSolution)}
          >
            <MaterialIcon
              name={showSolution ? 'expand_less' : 'expand_more'}
              size={18}
            />
            Xem lời giải chi tiết
          </button>

          {showSolution && (
            <div className="mt-4 p-5 bg-surface-container-low rounded-xl border border-whisper-border">
              <h4 className="font-bold text-on-surface mb-3">Lời giải chi tiết:</h4>
              <div className="space-y-3 text-sm text-on-surface-variant">
                {solution.map((step, i) => (
                  <p key={i}>{step}</p>
                ))}
              </div>
              <button className="mt-6 flex items-center gap-2 bg-primary text-white px-5 py-2.5 rounded-lg text-sm font-bold hover:opacity-90 transition-all shadow-sm">
                <MaterialIcon name="smart_toy" size={18} />
                💬 Hỏi AI giải thích câu này
              </button>
            </div>
          )}
          {!isScoreInvalidated && onReport && (
            <button
              type="button"
              className="mt-4 flex items-center gap-2 text-sm font-bold text-error hover:underline"
              onClick={onReport}
            >
              <MaterialIcon name="flag" size={18} />
              Báo cáo câu hỏi
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
