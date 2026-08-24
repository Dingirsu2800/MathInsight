/**
 * Converts a LaTeX tabular string into a GFM Markdown Table.
 * Handles \begin{tabular}{spec} ... \end{tabular} by parsing rows and cells,
 * stripping \hline, and outputting clean Markdown table syntax.
 */
export function tabularToMarkdownTable(tabularStr) {
  if (!tabularStr || typeof tabularStr !== 'string') return tabularStr || '';

  // Extract alignment specifier if present, e.g. {|c|c|c|c|c|}
  const alignMatch = tabularStr.match(/\\begin\{tabular\}\s*\{([^}]+)\}/);
  const spec = alignMatch ? alignMatch[1] : '';

  // Extract content between \begin{tabular}{...} and \end{tabular}
  const bodyMatch = tabularStr.match(/\\begin\{tabular\}(?:\s*\{[^}]*\})?([\s\S]*?)\\end\{tabular\}/);
  if (!bodyMatch) return tabularStr;

  const rawBody = bodyMatch[1];

  // Split rows by \\
  const rawRows = rawBody.split(/\\\\/);

  const parsedRows = [];
  for (let r of rawRows) {
    // Remove \hline, \toprule, \bottomrule, \midrule
    let cleanRow = r.replace(/\\(hline|toprule|bottomrule|midrule)/g, '').trim();
    if (!cleanRow) continue;

    // Split cells by &
    const cells = cleanRow.split('&').map((c) => c.trim());
    parsedRows.push(cells);
  }

  if (parsedRows.length === 0) return tabularStr;

  // Determine maximum number of columns
  const numCols = Math.max(...parsedRows.map((r) => r.length));

  // Build header row (Row 0)
  const headerRow = [...parsedRows[0]];
  while (headerRow.length < numCols) headerRow.push('');
  const headerStr = '| ' + headerRow.join(' | ') + ' |';

  // Build separator row based on alignment specifier
  const cleanSpec = spec.replace(/[^lcr]/g, '');
  const separators = [];
  for (let i = 0; i < numCols; i++) {
    const alignChar = cleanSpec[i] || 'c';
    if (alignChar === 'l') separators.push(':---');
    else if (alignChar === 'r') separators.push('---:');
    else separators.push(':---:');
  }
  const sepStr = '| ' + separators.join(' | ') + ' |';

  // Build data rows
  const dataStrs = [];
  for (let i = 1; i < parsedRows.length; i++) {
    const row = [...parsedRows[i]];
    while (row.length < numCols) row.push('');
    dataStrs.push('| ' + row.join(' | ') + ' |');
  }

  return '\n\n' + [headerStr, sepStr, ...dataStrs].join('\n') + '\n\n';
}

/**
 * Preprocesses LaTeX content before passing it to ReactMarkdown / remarkMath / rehypeKatex / remarkGfm.
 * - Converts unwrapped \begin{tabular}...\end{tabular} into Markdown tables so KaTeX/GFM renders them as tables.
 * - Auto-wraps other unwrapped LaTeX environments (array, matrix, pmatrix, bmatrix, vmatrix, Vmatrix, cases, align, equation, gather) in $$...$$
 *
 * @param {string} content
 * @returns {string}
 */
export function preprocessLatex(content) {
  if (!content || typeof content !== 'string') return content || '';

  // 1. Escape leading "number." patterns so Markdown does not parse them as ordered lists
  //    e.g. "1." → "1\.", "25." → "25\."  (only at the start of a line)
  let processed = content.replace(/^(\d+)\./gm, '$1\\.');

  // 2. Convert all \begin{tabular}...\end{tabular} into Markdown tables
  processed = processed.replace(/\\begin\{tabular\}[\s\S]*?\\end\{tabular\}/g, (match) => {
    return tabularToMarkdownTable(match);
  });

  // 3. Auto-wrap other unwrapped LaTeX environments in $$...$$
  const envPattern = /\\begin\{(array|matrix|pmatrix|bmatrix|vmatrix|Vmatrix|cases|align\*?|equation\*?|gather\*?)\}[\s\S]*?\\end\{\1\}/g;

  processed = processed.replace(envPattern, (match, envName, offset, fullString) => {
    const prefix = fullString.slice(Math.max(0, offset - 5), offset);
    const suffix = fullString.slice(offset + match.length, offset + match.length + 5);

    if (prefix.includes('$') || suffix.includes('$')) {
      return match;
    }
    return `\n\n$$\n${match}\n$$\n\n`;
  });

  return processed;
}
