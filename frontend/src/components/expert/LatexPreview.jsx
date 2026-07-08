import React from "react";
import ReactMarkdown from "react-markdown";
import remarkMath from "remark-math";
import rehypeKatex from "rehype-katex";

export default function LatexPreview({ content }) {
  if (!content?.trim()) {
    return <p className="text-xs text-on-surface-variant italic">Chưa có nội dung để xem trước.</p>;
  }

  return (
    <div className="text-[13px] text-on-surface break-words leading-relaxed font-body">
      <ReactMarkdown
        remarkPlugins={[remarkMath]}
        rehypePlugins={[[rehypeKatex, { throwOnError: false }]]}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
