import ReactMarkdown from 'react-markdown';
import remarkMath from 'remark-math';
import remarkGfm from 'remark-gfm';
import rehypeKatex from 'rehype-katex';
import { preprocessLatex } from '../../utils/latex';

import { cn } from '../../utils/cn';

const defaultComponents = {
  table: ({ node, ...props }) => (
    <div className="overflow-x-auto max-w-full my-2">
      <table className="min-w-max border-collapse border border-whisper-border text-xs text-on-surface rounded-lg overflow-hidden" {...props} />
    </div>
  ),
  thead: ({ node, ...props }) => (
    <thead className="bg-surface-container-low border-b border-whisper-border" {...props} />
  ),
  th: ({ node, ...props }) => (
    <th className="px-3 py-2 text-left font-bold border border-whisper-border/60 text-xs text-on-surface" {...props} />
  ),
  td: ({ node, ...props }) => (
    <td className="px-3 py-2 border border-whisper-border/60 text-xs text-on-surface" {...props} />
  ),
};

/**
 * Renders Markdown + LaTeX content with support for KaTeX math and LaTeX tables (tabular, array, etc.).
 *
 * @param {{ content: string, className?: string, components?: object, as?: any }} props
 */
export default function MathMarkdown({ content, className = '', components, as: Component = 'div' }) {
  const processed = preprocessLatex(content || '');
  const mergedComponents = components ? { ...defaultComponents, ...components } : defaultComponents;

  return (
    <Component className={cn('math-markdown', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[[rehypeKatex, { throwOnError: false }]]}
        components={mergedComponents}
      >
        {processed}
      </ReactMarkdown>
    </Component>
  );
}
