import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import MathMarkdown from '../../../components/ui/MathMarkdown';

function isBooleanValue(val) {
  return val === true || val === false || val === 'True' || val === 'False';
}

function formatBooleanLabel(val) {
  if (val === true || val === 'True') return 'Đúng';
  if (val === false || val === 'False') return 'Sai';
  return String(val ?? '');
}

/**
 * Composite question card (Multiple statements / sub-parts).
 * Supports True/False, Short Answer, and Numeric sub-parts.
 */
export default function CompositeQuestionCard({
  index,
  stem,
  pictureUrl,
  difficulty = 'KHÓ',
  difficultyClass = 'bg-tertiary-fixed text-tertiary',
  topicName,
  statements = [],
  maxScore = 1,
  earnedScore = 0,
  solution = [],
  machinePoints = 0,
  isScoreInvalidated = false,
  reportReason,
  scoreAdjustedTime,
  onReport,
  onAskChatbot,
}) {
  const [showSolution, setShowSolution] = useState(false);

  const correctCount = statements.filter((s) => s.isCorrect === true).length;
  const hasPartExplanations = statements.some((s) => s.explanation && s.explanation.trim().length > 0);

  return (
    <div className="bg-pure-surface rounded-xl border border-whisper-border overflow-hidden shadow-sm">
      {/* Header */}
      <div className="p-4 bg-surface-container-low border-b border-whisper-border flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <span className="w-8 h-8 shrink-0 rounded-full bg-primary text-white flex items-center justify-center font-bold text-sm">
            {index}
          </span>
          <span className="text-sm font-bold text-on-surface-variant truncate">
            Câu hỏi nhiều mệnh đề (Composite)
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
              Điểm máy chấm: {Number(machinePoints).toFixed(2)} · Điểm hiệu lực: {Number(earnedScore).toFixed(2)} / {Number(maxScore).toFixed(2)}
            </p>
            {scoreAdjustedTime && (
              <p className="mt-1 text-xs">Điều chỉnh lúc {new Date(scoreAdjustedTime).toLocaleString('vi-VN')}</p>
            )}
          </div>
        )}

        {/* Stem */}
        <div className="mb-6 p-4 bg-surface-container-low rounded-lg border-l-4 border-primary">
          <MathMarkdown content={stem || ''} className="text-base text-on-surface prose prose-sm max-w-none" />
        </div>

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

        {/* Table of Sub-Parts */}
        <div className="overflow-x-auto border border-whisper-border rounded-xl mb-6">
          <table className="w-full text-left border-collapse min-w-[560px]">
            <thead>
              <tr className="bg-surface-container-low text-xs font-bold uppercase text-on-surface-variant">
                <th className="px-6 py-4 border-b border-whisper-border">Mệnh đề / Câu hỏi con</th>
                <th className="px-6 py-4 border-b border-whisper-border w-44 text-center">Lựa chọn của bạn</th>
                <th className="px-6 py-4 border-b border-whisper-border w-40 text-center">Đáp án đúng</th>
                <th className="px-6 py-4 border-b border-whisper-border w-24 text-center">Kết quả</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-whisper-border text-sm">
              {statements.map((stmt, i) => {
                const isItemCorrect = stmt.isCorrect === true;
                const isUnanswered = stmt.studentAnswer === null || stmt.studentAnswer === undefined || String(stmt.studentAnswer).trim() === '';
                const isBoolean = isBooleanValue(stmt.correctAnswer) || stmt.partType === 'TrueFalse' || stmt.partType === 'TRUE_FALSE';

                const label = stmt.partLabel || `Ý ${i + 1}`;

                return (
                  <tr key={stmt.partId || i} className={isUnanswered ? 'bg-surface-container-lowest' : isItemCorrect ? 'bg-emerald-success/5' : 'bg-deep-rose/5'}>
                    <td className="px-6 py-4 text-on-surface">
                      <div className="flex items-baseline gap-2">
                        <span className="font-bold text-primary shrink-0">{label}:</span>
                        <MathMarkdown content={stmt.text || ''} className="prose prose-sm max-w-none italic font-medium [&>p]:m-0 [&>p]:inline" />
                      </div>
                    </td>
                    <td className="px-6 py-4 text-center font-bold">
                      {isUnanswered ? (
                        <span className="px-2.5 py-1 rounded text-xs inline-block bg-surface-container text-outline font-normal">
                          Chưa trả lời
                        </span>
                      ) : isBoolean ? (
                        <span className={`px-2.5 py-1 rounded text-xs inline-block ${
                          isItemCorrect ? 'bg-emerald-success/20 text-emerald-800' : 'bg-deep-rose/20 text-deep-rose'
                        }`}>
                          {formatBooleanLabel(stmt.studentAnswer)}
                        </span>
                      ) : (
                        <span className={`px-2.5 py-1 rounded text-xs inline-block max-w-[150px] truncate ${
                          isItemCorrect ? 'bg-emerald-success/20 text-emerald-800 border border-emerald-300' : 'bg-deep-rose/20 text-deep-rose border border-deep-rose/30'
                        }`}>
                          <MathMarkdown content={String(stmt.studentAnswer)} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline" />
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-center font-bold">
                      <span className="px-2.5 py-1 rounded text-xs inline-block bg-emerald-100 text-emerald-900 border border-emerald-300">
                        {isBoolean ? (
                          formatBooleanLabel(stmt.correctAnswer)
                        ) : (
                          <MathMarkdown content={String(stmt.correctAnswer ?? '—')} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline font-bold text-emerald-900" />
                        )}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-center">
                      {isUnanswered ? (
                        <MaterialIcon name="remove_circle_outline" className="text-outline font-bold" size={20} />
                      ) : isItemCorrect ? (
                        <MaterialIcon name="check_circle" className="text-emerald-success font-bold" size={20} />
                      ) : (
                        <MaterialIcon name="cancel" className="text-deep-rose font-bold" size={20} />
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Summary */}
        <div className="p-4 bg-surface-container-low rounded-lg border border-whisper-border space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <MaterialIcon name="analytics" className="text-primary" />
              <span className="text-sm font-medium">
                Tóm tắt kết quả:{' '}
                <span className="font-bold text-on-surface">
                  Đúng {correctCount}/{statements.length} ý
                </span>
              </span>
            </div>
            <div className="flex items-center gap-4">
              <span className="text-sm text-on-surface-variant">Điểm nhận:</span>
              <span className="font-mono text-lg font-bold text-primary">
                {Number(earnedScore).toFixed(2)} / {Number(maxScore).toFixed(2)}
              </span>
            </div>
          </div>
          <div className="text-xs text-emerald-900 bg-emerald-50 p-2.5 rounded border border-emerald-200">
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="font-bold shrink-0">Đáp án chuẩn:</span>
              {statements.map((s, idx) => {
                const label = s.partLabel || `Ý ${idx + 1}`;
                const isBoolean = isBooleanValue(s.correctAnswer) || s.partType === 'TrueFalse' || s.partType === 'TRUE_FALSE';
                const ansText = isBoolean ? formatBooleanLabel(s.correctAnswer) : String(s.correctAnswer ?? '—');
                return (
                  <div key={s.partId || idx} className="inline-flex items-center gap-1">
                    {idx > 0 && <span className="text-emerald-400 font-bold mx-1">|</span>}
                    <span className="font-bold">{label}:</span>
                    <MathMarkdown content={ansText} className="prose prose-sm max-w-none inline-block [&>p]:m-0 [&>p]:inline" />
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Solution toggle */}
        <div className="mt-6 pt-6 border-t border-whisper-border">
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
            <div className="mt-4 p-5 bg-surface-container-low rounded-xl border border-whisper-border space-y-4">
              {solution.length > 0 && (
                <div>
                  <h4 className="font-bold text-on-surface mb-2">Lời giải tổng quát:</h4>
                  <div className="space-y-3 text-sm text-on-surface-variant">
                    {solution.map((step, i) => (
                      <MathMarkdown key={i} content={step} className="prose prose-sm max-w-none" />
                    ))}
                  </div>
                </div>
              )}

              {hasPartExplanations && (
                <div className="pt-3 border-t border-whisper-border">
                  <h4 className="font-bold text-on-surface mb-3">Giải thích chi tiết từng ý:</h4>
                  <div className="space-y-3 text-sm text-on-surface-variant">
                    {statements.map((stmt, i) => {
                      if (!stmt.explanation) return null;
                      const label = stmt.partLabel || `Ý ${i + 1}`;
                      return (
                        <div key={stmt.partId || i} className="p-3 bg-pure-surface rounded-lg border border-whisper-border">
                          <span className="font-bold text-primary block mb-1">{label}:</span>
                          <MathMarkdown content={stmt.explanation} className="prose prose-sm max-w-none" />
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {solution.length === 0 && !hasPartExplanations && (
                <p className="text-sm text-outline italic">Chưa có lời giải chi tiết cho câu hỏi này.</p>
              )}

              <button
                className="mt-4 flex items-center gap-2 bg-primary text-white px-5 py-2.5 rounded-lg text-sm font-bold hover:opacity-90 transition-all shadow-sm"
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
