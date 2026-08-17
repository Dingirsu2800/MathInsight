import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import MathMarkdown from './MathMarkdown';

afterEach(() => {
  cleanup();
});

describe('MathMarkdown - GFM Table and KaTeX Responsive Overflow', () => {
  it('renders GFM markdown table with semantic elements and responsive overflow container', () => {
    const tableMarkdown = `
| Tiêu đề 1 | Tiêu đề 2 |
| :--- | :--- |
| Giá trị A | Giá trị B |
| Giá trị C | Giá trị D |
`;

    const { container } = render(<MathMarkdown content={tableMarkdown} />);

    // Semantic table elements
    const table = container.querySelector('table');
    expect(table).toBeInTheDocument();

    const thead = container.querySelector('thead');
    expect(thead).toBeInTheDocument();

    const ths = container.querySelectorAll('th');
    expect(ths.length).toBe(2);
    expect(screen.getByText('Tiêu đề 1')).toBeInTheDocument();
    expect(screen.getByText('Tiêu đề 2')).toBeInTheDocument();

    const tds = container.querySelectorAll('td');
    expect(tds.length).toBe(4);
    expect(screen.getByText('Giá trị A')).toBeInTheDocument();

    // Overflow wrapper
    const overflowWrapper = table.closest('.overflow-x-auto');
    expect(overflowWrapper).toBeInTheDocument();
  });

  it('renders root with math-markdown class to support scoped KaTeX display overflow', () => {
    const mathContent = '$$\\begin{matrix} a & b \\\\ c & d \\end{matrix}$$';
    const { container } = render(<MathMarkdown content={mathContent} className="custom-class" />);

    const root = container.firstChild;
    expect(root).toHaveClass('math-markdown');
    expect(root).toHaveClass('custom-class');
  });
});
