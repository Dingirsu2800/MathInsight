import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';
import MaterialIcon from '../../../components/ui/MaterialIcon';

/**
 * Composite True/False question card.
 * @param {{ index: number, stem: string, difficulty: string, difficultyClass: string, statements: Array<{text: string, correctAnswer: boolean, studentAnswer: boolean}>, maxScore: number, earnedScore: number, solution: string[] }} props
 */
export default function CompositeQuestionCard({
  index,
  stem,
  difficulty = 'KHÓ',
  difficultyClass = 'bg-tertiary-fixed text-tertiary',
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

  const correctCount = statements.filter(
    (s) => s.isCorrect === true
  ).length;

  return (
    <div className="bg-pure-surface rounded-xl border border-whisper-border overflow-hidden shadow-sm">
      {/* Header */}
      <div className="p-4 bg-surface-container-low border-b border-whisper-border flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span className="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center font-bold text-sm">
            {index}
          </span>
          <span className="text-sm font-bold text-on-surface-variant">
            Câu hỏi Đúng/Sai (Composite)
          </span>
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
              Điểm máy chấm: {Number(machinePoints).toFixed(2)} · Điểm hiệu lực: {Number(earnedScore).toFixed(2)} / {Number(maxScore).toFixed(2)}
            </p>
            {scoreAdjustedTime && (
              <p className="mt-1 text-xs">Điều chỉnh lúc {new Date(scoreAdjustedTime).toLocaleString('vi-VN')}</p>
            )}
          </div>
        )}
        {/* Stem */}
        <div className="mb-6 p-4 bg-surface-container-low rounded-lg border-l-4 border-primary">
          <div className="text-base text-on-surface prose prose-sm max-w-none">
            <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
              {stem || ''}
            </ReactMarkdown>
          </div>
        </div>

        {/* Table */}
        <div className="overflow-hidden border border-whisper-border rounded-xl mb-6">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-surface-container-low text-xs font-bold uppercase text-on-surface-variant">
                <th className="px-6 py-4 border-b border-whisper-border">Khẳng định</th>
                <th className="px-6 py-4 border-b border-whisper-border w-36 text-center">Lựa chọn của bạn</th>
                <th className="px-6 py-4 border-b border-whisper-border w-32 text-center">Đáp án đúng</th>
                <th className="px-6 py-4 border-b border-whisper-border w-24 text-center">Kết quả</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-whisper-border text-sm">
              {statements.map((stmt, i) => {
                const isItemCorrect = stmt.isCorrect === true;
                const studentText = stmt.studentAnswer === true ? 'Đúng' : stmt.studentAnswer === false ? 'Sai' : 'Chưa chọn';
                const correctText = stmt.correctAnswer === true ? 'Đúng' : stmt.correctAnswer === false ? 'Sai' : (stmt.correctAnswer || '—');

                return (
                  <tr key={i} className={isItemCorrect ? 'bg-emerald-success/5' : 'bg-deep-rose/5'}>
                    <td className="px-6 py-4 italic font-medium text-on-surface">
                      <div className="prose prose-sm max-w-none">
                        <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                          {stmt.text || ''}
                        </ReactMarkdown>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-center font-bold">
                      <span className={`px-2.5 py-1 rounded text-xs inline-block ${
                        stmt.studentAnswer === null
                          ? 'bg-surface-container text-outline'
                          : isItemCorrect
                            ? 'bg-emerald-success/20 text-emerald-800'
                            : 'bg-deep-rose/20 text-deep-rose'
                      }`}>
                        {studentText}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-center font-bold">
                      <span className="px-2.5 py-1 rounded text-xs inline-block bg-emerald-100 text-emerald-900 border border-emerald-300">
                        {correctText}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-center">
                      {isItemCorrect ? (
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
        <div className="p-4 bg-surface-container-low rounded-lg border border-whisper-border space-y-2">
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
                {earnedScore.toFixed(2)} / {maxScore.toFixed(2)}
              </span>
            </div>
          </div>
          <div className="text-xs text-emerald-900 bg-emerald-50 p-2.5 rounded border border-emerald-200">
            <span className="font-bold">Đáp án chuẩn: </span>
            {statements.map((s, idx) => {
              const label = `Ý ${idx + 1}`;
              const ansText = s.correctAnswer === true ? 'Đúng' : s.correctAnswer === false ? 'Sai' : (s.correctAnswer || '—');
              return `${label}: ${ansText}`;
            }).join(' | ')}
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
            <div className="mt-4 p-5 bg-surface-container-low rounded-xl border border-whisper-border">
              <h4 className="font-bold text-on-surface mb-3">Lời giải chi tiết từng ý:</h4>
              <div className="space-y-4 text-sm text-on-surface-variant">
                {solution.map((step, i) => (
                  <div key={i} className="prose prose-sm max-w-none">
                    <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                      {step}
                    </ReactMarkdown>
                  </div>
                ))}
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

/** Small radio dot indicator */
function RadioDot({ filled }) {
  if (filled) {
    return (
      <div className="w-5 h-5 rounded-full border-2 border-primary bg-primary mx-auto flex items-center justify-center">
        <div className="w-2 h-2 rounded-full bg-white" />
      </div>
    );
  }
  return <div className="w-5 h-5 rounded-full border-2 border-outline mx-auto" />;
}
