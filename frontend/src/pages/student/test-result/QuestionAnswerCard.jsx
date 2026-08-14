import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import MathMarkdown from '../../../components/ui/MathMarkdown';

/**
 * Individual MCQ / Short Answer question result card with expandable solution.
 */
export default function QuestionAnswerCard({
  index,
  question,
  questionType,
  pictureUrl,
  difficulty,
  difficultyClass = 'bg-primary-fixed text-primary',
  topicName,
  options = [],
  solution = [],
  machinePoints = 0,
  effectivePoints = 0,
  maxPoints = 0,
  isScoreInvalidated = false,
  reportReason,
  scoreAdjustedTime,
  shortAnswerText,
  isCorrect,
  onReport,
  onAskChatbot,
}) {
  const [showSolution, setShowSolution] = useState(false);

  const correctOptions = options.filter((o) => o.isCorrect);
  const isShortAnswer = questionType === 'SHORT_ANSWER';
  const isShortAnswerEmpty = !shortAnswerText || shortAnswerText.trim() === '';

  return (
    <div className="bg-pure-surface rounded-xl border border-whisper-border overflow-hidden shadow-sm">
      {/* Header */}
      <div className="p-4 bg-surface-container-low border-b border-whisper-border flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <span className="w-8 h-8 shrink-0 rounded-full bg-primary text-white flex items-center justify-center font-bold text-sm">
            {index}
          </span>
          <span className="text-sm font-bold text-on-surface-variant truncate">
            {isShortAnswer ? 'Câu hỏi trả lời ngắn' : 'Câu hỏi trắc nghiệm'}
          </span>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {topicName && (
            <span className="px-2 py-0.5 text-[11px] font-medium rounded-full bg-primary/10 text-primary border border-primary/20 flex items-center gap-1">
              <span className="material-symbols-outlined" style={{ fontSize: 12 }}>tag</span>
              {topicName}
            </span>
          )}
          <span className={`px-2 py-0.5 text-[11px] font-bold rounded ${difficultyClass}`}>
            {difficulty}
          </span>
        </div>
      </div>

      {/* Body */}
      <div className="p-6">
        {isScoreInvalidated && (
          <div className="mb-4 border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 rounded-lg">
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

        <MathMarkdown content={question || ''} className="text-base mb-4 text-on-surface prose prose-sm max-w-none" />

        {/* Illustration Picture */}
        {pictureUrl && (
          <div className="mb-6 flex justify-center">
            <img
              src={pictureUrl}
              alt={`Minh họa câu ${index}`}
              className="max-h-80 w-auto max-w-full object-contain rounded-lg border border-whisper-border shadow-xs"
            />
          </div>
        )}

        {/* SHORT ANSWER section */}
        {isShortAnswer ? (
          <div className="space-y-3 my-4">
            <div className={`p-4 border rounded-lg text-sm ${
              isShortAnswerEmpty
                ? 'border-whisper-border bg-surface-container-lowest'
                : isCorrect
                  ? 'border-emerald-success/30 bg-emerald-success/10'
                  : 'border-deep-rose/30 bg-deep-rose/10'
            }`}>
              <div className="flex items-center justify-between mb-1">
                <span className="font-bold text-xs uppercase text-on-surface-variant">Câu trả lời của bạn:</span>
                {isShortAnswerEmpty ? (
                  <span className="text-xs px-2 py-0.5 rounded bg-surface-container text-outline">Chưa trả lời</span>
                ) : isCorrect ? (
                  <span className="text-xs px-2 py-0.5 rounded bg-emerald-success text-white font-bold">Chính xác</span>
                ) : (
                  <span className="text-xs px-2 py-0.5 rounded bg-deep-rose text-white font-bold">Chưa chính xác</span>
                )}
              </div>
              <div className="font-medium text-on-surface">
                {isShortAnswerEmpty ? (
                  <span className="italic text-outline font-normal">(Chưa trả lời)</span>
                ) : (
                  <MathMarkdown content={shortAnswerText} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline" />
                )}
              </div>
            </div>

            <div className="p-4 border border-emerald-200 bg-emerald-50 rounded-lg text-sm text-emerald-900">
              <span className="font-bold block mb-1 text-xs uppercase text-emerald-800">Đáp án đúng:</span>
              <div className="font-medium">
                {correctOptions.length > 0 ? (
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    {correctOptions.map((o, idx) => (
                      <div key={idx} className="inline-flex items-center gap-1">
                        {idx > 0 && <span className="text-emerald-600 font-normal mx-1">hoặc</span>}
                        <MathMarkdown content={o.text || ''} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline font-bold" />
                      </div>
                    ))}
                  </div>
                ) : (
                  'Xem lời giải chi tiết'
                )}
              </div>
            </div>
          </div>
        ) : (
          <>
            {/* Options grid (kept as current implementation for single choice / options) */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {options.map((opt) => {
                let borderClass = 'border-whisper-border';
                let bgClass = '';
                let iconEl = null;
                let badgeEl = null;

                if (opt.isCorrect && opt.isSelected) {
                  borderClass = 'border-emerald-success/50';
                  bgClass = 'bg-emerald-success/10';
                  iconEl = <MaterialIcon name="check_circle" className="text-emerald-success" />;
                  badgeEl = <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-emerald-success text-white shrink-0">Bạn chọn (Đúng)</span>;
                } else if (opt.isCorrect && !opt.isSelected) {
                  borderClass = 'border-emerald-success/50';
                  bgClass = 'bg-emerald-50';
                  iconEl = <MaterialIcon name="check_circle" className="text-emerald-600" />;
                  badgeEl = <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 shrink-0">Đáp án đúng</span>;
                } else if (!opt.isCorrect && opt.isSelected) {
                  borderClass = 'border-deep-rose/50';
                  bgClass = 'bg-deep-rose/10';
                  iconEl = <MaterialIcon name="cancel" className="text-deep-rose" />;
                  badgeEl = <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-deep-rose/20 text-deep-rose shrink-0">Bạn chọn (Sai)</span>;
                }

                return (
                  <div
                    key={opt.label}
                    className={`p-4 border rounded-lg flex items-center justify-between gap-3 text-sm ${borderClass} ${bgClass}`}
                  >
                    <div className="flex items-center gap-2 flex-1">
                      <span className="font-bold shrink-0">{opt.label}.</span>
                      <MathMarkdown content={opt.text || ''} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline" />
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
                      {badgeEl}
                      {iconEl}
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Summary correct answer banner */}
            {correctOptions.length > 0 && (
              <div className="mt-4 p-3 bg-emerald-50 border border-emerald-200 rounded-lg flex items-start gap-2 text-sm text-emerald-900">
                <MaterialIcon name="check_circle" className="text-emerald-600 shrink-0 mt-0.5" size={18} />
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <span className="font-bold shrink-0">Đáp án đúng:</span>
                  {correctOptions.map((o, idx) => (
                    <div key={o.label} className="inline-flex items-center gap-1">
                      {idx > 0 && <span className="text-emerald-400 font-bold mx-1">|</span>}
                      <span className="font-bold">{o.label}.</span>
                      <MathMarkdown content={o.text || ''} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline font-bold" />
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}

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
                {solution.length > 0 ? (
                  solution.map((step, i) => (
                    <MathMarkdown key={i} content={step} className="prose prose-sm max-w-none" />
                  ))
                ) : (
                  <p className="italic text-outline">Chưa có lời giải chi tiết cho câu hỏi này.</p>
                )}
              </div>
              <button
                className="mt-6 flex items-center gap-2 bg-primary text-white px-5 py-2.5 rounded-lg text-sm font-bold hover:opacity-90 transition-all shadow-sm"
                onClick={onAskChatbot}
              >
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
