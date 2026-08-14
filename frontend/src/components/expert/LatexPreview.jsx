import React from "react";
import MathMarkdown from "../ui/MathMarkdown";

export default function LatexPreview({ content }) {
  if (!content?.trim()) {
    return <p className="text-xs text-on-surface-variant italic">Chưa có nội dung để xem trước.</p>;
  }

  return (
    <MathMarkdown
      content={content}
      className="text-[13px] text-on-surface break-words leading-relaxed font-body"
    />
  );
}

