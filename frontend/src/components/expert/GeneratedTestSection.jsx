import React from "react";
import GeneratedTestQuestionCard from "./GeneratedTestQuestionCard";
import LatexPreview from "./LatexPreview";

export default function GeneratedTestSection({ section, sectionIndex }) {
  if (!section) return null;

  const displayOrder = section.sectionOrder || sectionIndex + 1;
  const questions = section.questions || [];

  return (
    <section
      id={`section-${displayOrder}`}
      className="bg-pure-surface border border-whisper-border rounded-xl p-6 shadow-sm flex flex-col gap-5 scroll-mt-20"
    >
      {/* Section Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-whisper-border pb-4">
        <div>
          <div className="flex items-center gap-2">
            <h2 className="text-base font-bold text-on-surface">
              Phần {displayOrder}: {section.sectionName}
            </h2>
            {section.sectionCode && (
              <span className="bg-surface-container-low px-2.5 py-0.5 rounded border border-whisper-border text-[10px] font-bold text-on-surface font-mono">
                Mã: {section.sectionCode}
              </span>
            )}
          </div>
          <p className="text-xs text-on-surface-variant mt-1 font-medium">
            Số lượng: <strong className="text-on-surface font-mono">{questions.length} câu hỏi</strong>
          </p>
        </div>

        {/* Section Metrics */}
        <div className="flex flex-wrap items-center gap-2 text-xs">
          <span className="bg-primary/10 text-primary border border-primary/20 font-bold px-3 py-1 rounded-lg">
            Quỹ điểm: <strong className="font-mono">{section.scoreBudget} điểm</strong>
          </span>
          <span className="bg-surface-container-low border border-whisper-border text-on-surface-variant font-semibold px-3 py-1 rounded-lg">
            Quy tắc phần: <strong className="text-on-surface">{section.scoringRule}</strong>
          </span>
        </div>
      </div>

      {/* Instruction text */}
      {section.instructionText && (
        <div className="bg-surface-container-low border border-whisper-border p-4 rounded-xl text-xs text-on-surface-variant leading-relaxed select-text">
          <span className="font-bold block text-[10px] uppercase text-primary tracking-wider mb-1">
            Hướng dẫn phần thi:
          </span>
          <LatexPreview content={section.instructionText} />
        </div>
      )}

      {/* Questions list */}
      <div className="flex flex-col gap-5">
        {questions.length === 0 ? (
          <div className="p-8 text-center text-xs text-on-surface-variant italic bg-surface-container-low/50 rounded-xl border border-dashed">
            Không có câu hỏi nào trong phần thi này.
          </div>
        ) : (
          questions.map((q, idx) => (
            <GeneratedTestQuestionCard
              key={q.questionId || idx}
              question={q}
              index={idx}
            />
          ))
        )}
      </div>
    </section>
  );
}
