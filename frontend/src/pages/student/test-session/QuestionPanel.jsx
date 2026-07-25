import ReactMarkdown from 'react-markdown';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';

/**
 * Renders a single question with its answer options.
 * Supports: SINGLE_CHOICE, TRUE_FALSE, MULTIPLE_SELECT, SHORT_ANSWER, COMPOSITE.
 *
 * @param {{
 *   question: { questionId, questionNo, questionContent, questionType, options?, parts? },
 *   answer: { answerId?, shortAnswerText?, selectedOptions?: string[], parts?: object[] },
 *   onAnswer: (questionId: string, answerUpdate: object) => void,
 *   totalQuestions: number
 * }} props
 */
export default function QuestionPanel({ question, answer, onAnswer, totalQuestions }) {
  if (!question) return null;

  const q = question;
  const type = (q.questionType || '').toUpperCase();

  const handleSingleSelect = (optionId) => {
    onAnswer(q.questionId, { answerId: optionId });
  };

  const handleMultiSelect = (optionId) => {
    const current = answer?.selectedOptions || [];
    const updated = current.includes(optionId)
      ? current.filter((id) => id !== optionId)
      : [...current, optionId];
    onAnswer(q.questionId, { selectedOptions: updated });
  };

  const handleShortAnswer = (text) => {
    onAnswer(q.questionId, { shortAnswerText: text });
  };

  const handlePartAnswer = (partId, field, value) => {
    const currentParts = answer?.parts || [];
    const existing = currentParts.find((p) => p.partId === partId);
    const updated = existing
      ? currentParts.map((p) => (p.partId === partId ? { ...p, [field]: value } : p))
      : [...currentParts, { partId, [field]: value }];
    onAnswer(q.questionId, { parts: updated });
  };

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm overflow-hidden">
      {/* Question header */}
      <div className="px-6 py-4 bg-surface-container-low border-b border-whisper-border flex items-center justify-between">
        <div className="flex items-center gap-3">
          <span className="w-9 h-9 rounded-lg bg-primary text-white flex items-center justify-center text-sm font-bold">
            {q.questionNo}
          </span>
          <span className="text-sm text-on-surface-variant">
            Câu {q.questionNo} / {totalQuestions}
          </span>
        </div>
        <span className="text-[11px] font-bold uppercase tracking-wider text-on-surface-variant px-2 py-1 rounded bg-surface-container">
          {formatType(type)}
        </span>
      </div>

      {/* Question content — supports LaTeX via remark-math + rehype-katex */}
      <div className="px-6 py-5">
        <div className="prose prose-sm max-w-none text-on-surface mb-6">
          <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
            {q.questionContent || '*(Nội dung câu hỏi)*'}
          </ReactMarkdown>
        </div>

        {/* SINGLE_CHOICE / TRUE_FALSE */}
        {(type === 'SINGLE_CHOICE' || type === 'SINGLECHOICE' || type === 'TRUE_FALSE' || type === 'TRUEFALSE') && (
          <div className="space-y-3">
            {(q.options || []).map((opt) => {
              const isSelected = answer?.answerId === opt.optionId;
              return (
                <button
                  key={opt.optionId}
                  onClick={() => handleSingleSelect(opt.optionId)}
                  className={`w-full text-left px-4 py-3 rounded-xl border-2 transition-all flex items-center gap-3 ${
                    isSelected
                      ? 'border-primary bg-primary/5 shadow-sm'
                      : 'border-whisper-border bg-pure-surface hover:border-primary/30 hover:bg-surface-container-low'
                  }`}
                >
                  <span
                    className={`w-5 h-5 rounded-full border-2 flex-shrink-0 flex items-center justify-center transition-colors ${
                      isSelected ? 'border-primary bg-primary' : 'border-outline-variant'
                    }`}
                  >
                    {isSelected && (
                      <span className="w-2 h-2 rounded-full bg-white" />
                    )}
                  </span>
                  <span className="text-sm text-on-surface">{opt.content}</span>
                </button>
              );
            })}
          </div>
        )}

        {/* MULTIPLE_SELECT */}
        {(type === 'MULTIPLE_SELECT' || type === 'MULTIPLESELECT') && (
          <div className="space-y-3">
            {(q.options || []).map((opt) => {
              const isSelected = (answer?.selectedOptions || []).includes(opt.optionId);
              return (
                <button
                  key={opt.optionId}
                  onClick={() => handleMultiSelect(opt.optionId)}
                  className={`w-full text-left px-4 py-3 rounded-xl border-2 transition-all flex items-center gap-3 ${
                    isSelected
                      ? 'border-primary bg-primary/5 shadow-sm'
                      : 'border-whisper-border bg-pure-surface hover:border-primary/30 hover:bg-surface-container-low'
                  }`}
                >
                  <span
                    className={`w-5 h-5 rounded flex-shrink-0 border-2 flex items-center justify-center transition-colors ${
                      isSelected ? 'border-primary bg-primary' : 'border-outline-variant'
                    }`}
                  >
                    {isSelected && (
                      <span className="material-symbols-outlined text-white text-sm">check</span>
                    )}
                  </span>
                  <span className="text-sm text-on-surface">{opt.content}</span>
                </button>
              );
            })}
          </div>
        )}

        {/* SHORT_ANSWER */}
        {(type === 'SHORT_ANSWER' || type === 'SHORTANSWER') && (
          <div>
            <textarea
              className="w-full border border-whisper-border rounded-xl px-4 py-3 text-sm text-on-surface bg-pure-surface focus:border-primary focus:ring-1 focus:ring-primary outline-none resize-none transition-colors"
              rows={3}
              placeholder="Nhập câu trả lời..."
              value={answer?.shortAnswerText || ''}
              onChange={(e) => handleShortAnswer(e.target.value)}
            />
          </div>
        )}

        {/* COMPOSITE */}
        {(type === 'COMPOSITE') && (
          <div className="space-y-4">
            {(q.parts || []).map((part, idx) => {
              const partAnswer = (answer?.parts || []).find((p) => p.partId === part.partId);
              return (
                <div key={part.partId} className="p-4 bg-surface-container-low rounded-xl border border-whisper-border">
                  <p className="text-sm text-on-surface mb-3 font-medium">
                    Phần {idx + 1}: {part.content || ''}
                  </p>
                  {part.answerType === 'BOOLEAN' ? (
                    <div className="flex gap-3">
                      {[{ label: 'Đúng', val: true }, { label: 'Sai', val: false }].map((opt) => (
                        <button
                          key={String(opt.val)}
                          onClick={() => handlePartAnswer(part.partId, 'booleanAnswer', opt.val)}
                          className={`px-6 py-2 rounded-lg border-2 text-sm font-bold transition-all ${
                            partAnswer?.booleanAnswer === opt.val
                              ? 'border-primary bg-primary/5 text-primary'
                              : 'border-whisper-border text-on-surface-variant hover:border-primary/30'
                          }`}
                        >
                          {opt.label}
                        </button>
                      ))}
                    </div>
                  ) : (
                    <input
                      type="text"
                      className="w-full border border-whisper-border rounded-lg px-3 py-2 text-sm text-on-surface bg-pure-surface focus:border-primary outline-none transition-colors"
                      placeholder="Nhập câu trả lời..."
                      value={partAnswer?.textAnswer || ''}
                      onChange={(e) => handlePartAnswer(part.partId, 'textAnswer', e.target.value)}
                    />
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

function formatType(type) {
  const map = {
    SINGLE_CHOICE: 'Trắc nghiệm',
    SINGLECHOICE: 'Trắc nghiệm',
    TRUE_FALSE: 'Đúng / Sai',
    TRUEFALSE: 'Đúng / Sai',
    MULTIPLE_SELECT: 'Chọn nhiều',
    MULTIPLESELECT: 'Chọn nhiều',
    SHORT_ANSWER: 'Tự luận',
    SHORTANSWER: 'Tự luận',
    COMPOSITE: 'Tổng hợp',
  };
  return map[type] || type;
}
