import React, { useState } from "react";
import LatexPreview from "./LatexPreview";
import { cn } from "../../utils/cn";

export default function GeneratedTestQuestionCard({ question, index }) {
  const [showSolution, setShowSolution] = useState(true);

  if (!question) return null;

  const displayOrder = question.questionOrder || index + 1;
  const qType = question.questionType || "SingleChoice";

  const getScoringRuleLabel = (rule) => {
    switch (rule) {
      case "AllOrNothing":
        return "Tất cả hoặc không (All or Nothing)";
      case "TieredTrueFalse":
        return "Đúng/Sai theo bậc (Tiered True/False)";
      case "WeightedParts":
        return "Theo trọng số phần (Weighted Parts)";
      default:
        return rule || "AllOrNothing";
    }
  };

  const sortedParts = question.parts
    ? [...question.parts].sort((a, b) => (a.partOrder || 0) - (b.partOrder || 0))
    : [];

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm flex flex-col gap-4 transition-all">
      {/* Question Header & Snapshots */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-whisper-border pb-3">
        <div className="flex items-center gap-2.5">
          <span className="bg-primary text-on-primary text-xs font-black px-3 py-1 rounded-lg font-mono">
            Câu {displayOrder}
          </span>
          <span className="bg-surface-container-low border border-whisper-border text-on-surface-variant text-[11px] font-bold px-2.5 py-0.5 rounded">
            {qType}
          </span>
        </div>

        {/* Snapshots */}
        <div className="flex flex-wrap items-center gap-3 text-[11px] font-mono">
          <span className="bg-surface-container-low px-2 py-1 rounded border border-whisper-border text-on-surface-variant" title="Trọng số chốt trong đề">
            Trọng số: <strong className="text-on-surface">{question.weightSnapshot}</strong>
          </span>
          <span className="bg-primary/10 px-2 py-1 rounded border border-primary/20 text-primary font-bold" title="Điểm tối đa chốt cho câu hỏi này">
            Điểm tối đa: <strong className="text-primary">{question.maxPointsSnapshot} điểm</strong>
          </span>
          <span className="bg-surface-container-low px-2 py-1 rounded border border-whisper-border text-on-surface-variant" title="Quy tắc chấm chốt">
            Quy tắc chấm: <strong className="text-on-surface">{getScoringRuleLabel(question.scoringRuleSnapshot)}</strong>
          </span>
        </div>
      </div>

      {/* Question Content */}
      <div className="select-text space-y-3">
        <LatexPreview content={question.questionContent} />

        {/* Question Image if present */}
        {question.pictureUrl && (
          <div className="my-3 max-w-lg rounded-xl overflow-hidden border border-whisper-border bg-surface-container-low p-2">
            <img
              src={question.pictureUrl}
              alt={`Hình ảnh minh họa cho câu ${displayOrder}`}
              className="max-w-full h-auto object-contain mx-auto rounded-lg max-h-80"
              loading="lazy"
            />
          </div>
        )}
      </div>

      {/* Question Answers / Options depending on QuestionType */}
      {/* 1. Single Choice & 2. Multiple Choice & 3. Standalone TrueFalse */}
      {(qType === "SingleChoice" || qType === "MultipleChoice" || qType === "TrueFalse") && question.answers && question.answers.length > 0 && (
        <div className="space-y-2 mt-1">
          <h4 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">Các phương án trả lời:</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5">
            {question.answers.map((ans, ansIdx) => {
              const optionLabel = String.fromCharCode(65 + ansIdx);
              return (
                <div
                  key={ans.answerId || ansIdx}
                  className={cn(
                    "p-3 rounded-xl border flex items-start gap-3 transition-all select-text",
                    ans.isCorrect
                      ? "bg-emerald-success/10 border-emerald-success/30 shadow-sm"
                      : "bg-surface-container-lowest border-whisper-border"
                  )}
                >
                  <span
                    className={cn(
                      "w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 font-mono mt-0.5",
                      ans.isCorrect
                        ? "bg-emerald-success text-white"
                        : "bg-surface-container-high text-on-surface-variant border border-whisper-border"
                    )}
                  >
                    {optionLabel}
                  </span>
                  <div className="flex-1 min-w-0">
                    <LatexPreview content={ans.answerContent} />
                  </div>
                  {ans.isCorrect && (
                    <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-success shrink-0 bg-emerald-success/15 px-2 py-0.5 rounded-md">
                      <span className="material-symbols-outlined text-[14px]">check_circle</span>
                      Đáp án đúng
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* 4. Short Answer */}
      {qType === "ShortAnswer" && question.answers && question.answers.filter(ans => ans.isCorrect).length > 0 && (
        <div className="space-y-2 mt-1 select-text">
          <h4 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">Đáp án tự luận ngắn:</h4>
          <div className="p-3 bg-emerald-success/10 border border-emerald-success/30 rounded-xl space-y-2">
            {question.answers.filter(ans => ans.isCorrect).map((ans, ansIdx) => (
              <div key={ans.answerId || ansIdx} className="flex items-center gap-2">
                <span className="material-symbols-outlined text-emerald-success text-[18px]">check_circle</span>
                <span className="text-xs font-bold text-emerald-success">Đáp án chuẩn:</span>
                <div className="flex-1 font-mono text-xs text-on-surface font-semibold">
                  <LatexPreview content={ans.answerContent} />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 5. Composite Question Parts */}
      {qType === "Composite" && sortedParts.length > 0 && (
        <div className="space-y-3 mt-2 select-text">
          <div className="flex items-center justify-between border-b border-whisper-border/60 pb-2">
            <h4 className="text-[11px] font-bold text-primary uppercase tracking-wider">
              Các mệnh đề / Phần câu hỏi phụ ({sortedParts.length} phần):
            </h4>
            {question.scoringRuleSnapshot && (
              <span className="text-[10px] font-bold text-on-surface-variant bg-surface-container-low px-2 py-0.5 rounded border">
                Quy tắc: {question.scoringRuleSnapshot}
              </span>
            )}
          </div>

          <div className="space-y-2.5">
            {sortedParts.map((part, pIdx) => {
              const partLabelStr = part.partLabel || `${String.fromCharCode(97 + pIdx)})`;
              return (
                <div
                  key={part.partId || pIdx}
                  className="p-3.5 bg-surface-container-lowest border border-whisper-border rounded-xl space-y-2"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-start gap-2 flex-1 min-w-0">
                      <span className="font-bold text-primary text-xs font-mono shrink-0 mt-0.5">
                        {partLabelStr}
                      </span>
                      <div className="flex-1 min-w-0">
                        <LatexPreview content={part.partContent} />
                      </div>
                    </div>

                    {/* Part correct state */}
                    <div className="shrink-0 flex flex-col items-end gap-1">
                      {part.correctBoolean !== null && part.correctBoolean !== undefined && (
                        <span
                          className={cn(
                            "inline-flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-lg border",
                            part.correctBoolean
                              ? "bg-emerald-success/15 border-emerald-success/30 text-emerald-success"
                              : "bg-error/10 border-error/20 text-error"
                          )}
                        >
                          <span className="material-symbols-outlined text-[14px]">
                            {part.correctBoolean ? "check_circle" : "cancel"}
                          </span>
                          Mệnh đề: {part.correctBoolean ? "ĐÚNG" : "SAI"}
                        </span>
                      )}

                      {part.correctText !== null && part.correctText !== undefined && (
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold bg-emerald-success/15 border border-emerald-success/30 text-emerald-success px-2.5 py-1 rounded-lg">
                          <span className="material-symbols-outlined text-[14px]">check_circle</span>
                          Đáp án: {part.correctText}
                        </span>
                      )}

                      {part.correctNumeric !== null && part.correctNumeric !== undefined && (
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold bg-emerald-success/15 border border-emerald-success/30 text-emerald-success px-2.5 py-1 rounded-lg font-mono">
                          <span className="material-symbols-outlined text-[14px]">check_circle</span>
                          Giá trị: {part.correctNumeric}
                          {part.numericTolerance !== null && part.numericTolerance !== undefined && (
                            <span className="text-[10px] opacity-80">(±{part.numericTolerance})</span>
                          )}
                        </span>
                      )}

                      <span className="text-[10px] text-on-surface-variant font-mono">
                        Trọng số: {part.defaultWeight}
                      </span>
                    </div>
                  </div>

                  {/* Part explanation */}
                  {part.explanation && (
                    <div className="mt-2 pt-2 border-t border-whisper-border/40 text-xs text-on-surface-variant bg-surface-container-low/50 p-2.5 rounded-lg">
                      <span className="font-bold text-[10px] text-primary uppercase block mb-1">
                        Giải thích mệnh đề:
                      </span>
                      <LatexPreview content={part.explanation} />
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {/* Composite Scoring Rule Note */}
          {question.scoringRuleSnapshot === "TieredTrueFalse" && (
            <div className="p-3 bg-primary/5 border border-primary/20 rounded-xl text-xs text-primary leading-relaxed flex items-start gap-2">
              <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">info</span>
              <div>
                <strong>Quy tắc chấm Đúng/Sai theo bậc (Tiered True/False):</strong> Đúng 1 mệnh đề = 10% điểm, Đúng 2 mệnh đề = 25% điểm, Đúng 3 mệnh đề = 50% điểm, Đúng cả 4 mệnh đề = 100% điểm.
              </div>
            </div>
          )}
        </div>
      )}

      {/* Detailed Solution Box */}
      {question.solutionContent && (
        <div className="mt-2 border-t border-whisper-border/60 pt-3 select-text">
          <button
            type="button"
            onClick={() => setShowSolution(!showSolution)}
            className="flex items-center justify-between w-full text-xs font-bold text-primary hover:underline cursor-pointer py-1"
          >
            <span className="flex items-center gap-1.5">
              <span className="material-symbols-outlined text-[16px]">lightbulb</span>
              Lời giải chi tiết câu hỏi
            </span>
            <span className="material-symbols-outlined text-[18px]">
              {showSolution ? "keyboard_arrow_up" : "keyboard_arrow_down"}
            </span>
          </button>

          {showSolution && (
            <div className="mt-2 p-4 bg-surface-container-low border border-whisper-border rounded-xl text-xs text-on-surface leading-relaxed animate-in fade-in duration-150">
              <LatexPreview content={question.solutionContent} />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
