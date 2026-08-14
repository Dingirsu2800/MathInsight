import ReactMarkdown from 'react-markdown';
import remarkMath from 'remark-math';
import remarkGfm from 'remark-gfm';
import rehypeKatex from 'rehype-katex';
import { preprocessLatex } from '../../utils/latex';

/**
 * Renders Markdown + LaTeX content with support for KaTeX math and LaTeX tables (tabular, array, etc.).
 *
 * @param {{ content: string, className?: string, components?: object, as?: any }} props
 */
export default function MathMarkdown({ content, className = '', components, as: Component = 'div' }) {
  const processed = preprocessLatex(content || '');

  return (
    <Component className={className}>
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[[rehypeKatex, { throwOnError: false }]]}
        components={components}
      >
        {processed}
      </ReactMarkdown>
    </Component>
  );
}
