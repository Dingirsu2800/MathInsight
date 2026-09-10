import React, { useRef } from 'react';
import {
  MAX_SHORT_ANSWER_LENGTH,
  countUnicodeScalars,
  formatMathPreviewLatex,
  hasMathTokens,
} from '../../utils/shortAnswer';
import LatexPreview from '../expert/LatexPreview';

/**
 * ShortAnswerInput component for Student and Expert roles.
 * Provides accessible π and √ insertion, character counting, safe math preview, and IME support.
 */
export default function ShortAnswerInput({
  value = '',
  onChange,
  disabled = false,
  error = '',
  id,
  placeholder = 'Nhập đáp án ngắn...',
  className = '',
  inputClassName = '',
  example = 'Ví dụ: 2π hoặc √(2)',
  showExample = true,
  showPreview = true,
  maxLength = MAX_SHORT_ANSWER_LENGTH,
}) {
  const inputRef = useRef(null);
  const stringValue = value == null ? '' : String(value);
  const scalarCount = countUnicodeScalars(stringValue);
  const displayPreview = showPreview && hasMathTokens(stringValue);
  const previewLatex = displayPreview ? formatMathPreviewLatex(stringValue) : '';

  const insertSymbol = (symbolType) => {
    if (disabled || !inputRef.current) return;
    const input = inputRef.current;
    const start = input.selectionStart ?? stringValue.length;
    const end = input.selectionEnd ?? stringValue.length;

    const before = stringValue.slice(0, start);
    const selection = stringValue.slice(start, end);
    const after = stringValue.slice(end);

    let insertedText = '';
    let newCursorPos = start;

    if (symbolType === 'pi') {
      insertedText = 'π';
      newCursorPos = start + 1;
    } else if (symbolType === 'sqrt') {
      if (selection.length > 0) {
        insertedText = `√(${selection})`;
        newCursorPos = start + insertedText.length;
      } else {
        insertedText = '√()';
        newCursorPos = start + 2; // Position inside the parentheses
      }
    }

    const nextValue = before + insertedText + after;
    if (countUnicodeScalars(nextValue) <= maxLength) {
      onChange(nextValue);

      // Restore focus and cursor position
      setTimeout(() => {
        if (inputRef.current) {
          inputRef.current.focus();
          inputRef.current.setSelectionRange(newCursorPos, newCursorPos);
        }
      }, 0);
    }
  };

  const handleChange = (e) => {
    const nextVal = e.target.value;
    // Allow typing, but warn or cap when exceeding scalar length
    if (countUnicodeScalars(nextVal) <= maxLength + 10) {
      onChange(nextVal);
    }
  };

  return (
    <div className={`space-y-1.5 ${className}`}>
      <div className="relative flex items-center">
        <input
          ref={inputRef}
          id={id}
          type="text"
          value={stringValue}
          onChange={handleChange}
          disabled={disabled}
          placeholder={placeholder}
          aria-invalid={Boolean(error)}
          className={`w-full border rounded-xl px-4 py-2.5 text-sm text-on-surface bg-pure-surface transition-colors outline-none pr-24 ${
            error
              ? 'border-error focus:border-error focus:ring-1 focus:ring-error'
              : 'border-whisper-border focus:border-primary focus:ring-1 focus:ring-primary'
          } ${inputClassName}`}
        />

        {/* Symbol insertion toolbar inside the right side of the input */}
        <div className="absolute right-2 flex items-center gap-1">
          <button
            type="button"
            onClick={() => insertSymbol('pi')}
            disabled={disabled}
            aria-label="Chèn ký hiệu pi (π)"
            title="Chèn ký hiệu pi (π)"
            className="w-7 h-7 flex items-center justify-center rounded-lg border border-whisper-border bg-surface-container-low hover:bg-surface-container hover:border-primary/40 text-on-surface font-serif font-bold text-sm transition-colors disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer select-none"
          >
            π
          </button>
          <button
            type="button"
            onClick={() => insertSymbol('sqrt')}
            disabled={disabled}
            aria-label="Chèn căn bậc hai (√)"
            title="Chèn căn bậc hai (√)"
            className="w-7 h-7 flex items-center justify-center rounded-lg border border-whisper-border bg-surface-container-low hover:bg-surface-container hover:border-primary/40 text-on-surface font-sans font-bold text-sm transition-colors disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer select-none"
          >
            √
          </button>
        </div>
      </div>

      {/* Auxiliary bar: Helper example, character counter, error message */}
      <div className="flex items-center justify-between text-xs text-on-surface-variant px-1 gap-2">
        <div className="flex-1 min-w-0">
          {error ? (
            <span className="text-error font-medium">{error}</span>
          ) : showExample && example ? (
            <span className="text-on-surface-variant/70 italic truncate block">{example}</span>
          ) : null}
        </div>
        <span className={`tabular-nums shrink-0 ${scalarCount > maxLength ? 'text-error font-bold' : ''}`}>
          {scalarCount}/{maxLength}
        </span>
      </div>

      {/* Safe Math Preview */}
      {displayPreview && (
        <div className="p-2.5 bg-surface-container-low rounded-lg border border-whisper-border/60 text-xs text-on-surface space-y-1">
          <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider block">
            Xem trước công thức:
          </span>
          <div className="overflow-x-auto py-0.5" data-testid="short-answer-math-preview">
            <LatexPreview content={previewLatex} />
          </div>
        </div>
      )}
    </div>
  );
}
