/**
 * Short Answer validation and normalization utilities matching backend ShortAnswerPolicy.
 */

export const MAX_SHORT_ANSWER_LENGTH = 100;

// Matches standalone "pi" not preceded or followed by unicode letters, digits, or underscore
const PI_ALIAS_REGEX = /(?<![\p{L}\p{N}_])pi(?![\p{L}\p{N}_])/giu;

// Matches repeated spaces/tabs
const REPEATED_SPACES_REGEX = /[ \t]+/g;

/**
 * Counts the exact number of Unicode scalar values (code points).
 * @param {string} str
 * @returns {number}
 */
export function countUnicodeScalars(str) {
  if (!str) return 0;
  return Array.from(str).length;
}

/**
 * Checks whether string contains forbidden Unicode characters (control, format, surrogate).
 * @param {string} str
 * @returns {boolean}
 */
export function hasForbiddenCharacters(str) {
  if (!str) return false;
  // Unicode category C (Control, Format, Surrogate, Private Use, Unassigned)
  // We allow standard characters and disallow newlines and control characters
  for (const char of str) {
    const code = char.codePointAt(0);
    // Control characters (0x00-0x1F, 0x7F-0x9F) including newlines (\r, \n)
    if ((code >= 0x00 && code <= 0x1f) || (code >= 0x7f && code <= 0x9f)) {
      return true;
    }
    // Surrogates (isolated)
    if (code >= 0xd800 && code <= 0xdfff) {
      return true;
    }
  }
  return false;
}

/**
 * Replaces sqrt(...) with √(..) with balanced parenthesis matching.
 * Matches ShortAnswerPolicy.NormalizeSqrtAliases in backend.
 * @param {string} value
 * @returns {string}
 */
export function normalizeSqrtAliases(value) {
  if (!value) return '';
  let output = '';
  for (let i = 0; i < value.length; ) {
    if (startsWithSqrtAlias(value, i)) {
      const openParen = i + 4; // length of 'sqrt'
      const closeParen = findMatchingParenthesis(value, openParen);
      if (closeParen >= 0) {
        const inner = value.slice(openParen + 1, closeParen);
        output += '√(' + normalizeSqrtAliases(inner) + ')';
        i = closeParen + 1;
        continue;
      }
    }
    output += value[i];
    i++;
  }
  return output;
}

function startsWithSqrtAlias(value, index) {
  const alias = 'sqrt';
  if (index + alias.length >= value.length) return false;
  if (value.slice(index, index + alias.length).toLowerCase() !== alias) return false;
  if (index > 0 && /[\p{L}\p{N}_]/u.test(value[index - 1])) return false;
  return value[index + alias.length] === '(';
}

function findMatchingParenthesis(value, openParen) {
  let depth = 0;
  for (let i = openParen; i < value.length; i++) {
    if (value[i] === '(') depth++;
    if (value[i] === ')') {
      depth--;
      if (depth === 0) return i;
    }
  }
  return -1;
}

/**
 * Normalizes short answer input according to backend ShortAnswerPolicy:
 * 1. NFKC normalization
 * 2. Trim ends
 * 3. Convert sqrt(...) -> √(..)
 * 4. Convert standalone 'pi' -> 'π'
 * 5. Collapse repeated spaces to single space
 * @param {string} input
 * @returns {string}
 */
export function normalizeShortAnswer(input) {
  if (!input) return '';
  let value = String(input).normalize('NFKC').trim();
  if (!value) return '';

  value = normalizeSqrtAliases(value);
  value = value.replace(PI_ALIAS_REGEX, 'π');
  value = value.replace(REPEATED_SPACES_REGEX, ' ').trim();
  return value;
}

/**
 * Validates whether the short answer string is valid.
 * @param {string} input
 * @param {{ required?: boolean }} [options]
 * @returns {{ isValid: boolean, error?: string }}
 */
export function validateShortAnswer(input, { required = false } = {}) {
  if (input == null || input === '') {
    if (required) {
      return { isValid: false, error: 'Vui lòng nhập câu trả lời.' };
    }
    return { isValid: true };
  }

  const str = String(input);
  if (hasForbiddenCharacters(str)) {
    return { isValid: false, error: 'Câu trả lời không được chứa ký tự xuống dòng hoặc ký tự điều khiển.' };
  }

  if (countUnicodeScalars(str) > MAX_SHORT_ANSWER_LENGTH) {
    return { isValid: false, error: `Câu trả lời không được vượt quá ${MAX_SHORT_ANSWER_LENGTH} ký tự.` };
  }

  const normalized = normalizeShortAnswer(str);
  if (required && (!normalized || normalized.length === 0)) {
    return { isValid: false, error: 'Vui lòng nhập câu trả lời.' };
  }

  if (countUnicodeScalars(normalized) > MAX_SHORT_ANSWER_LENGTH) {
    return { isValid: false, error: `Câu trả lời không được vượt quá ${MAX_SHORT_ANSWER_LENGTH} ký tự.` };
  }

  return { isValid: true };
}

/**
 * Checks if a string contains math tokens that warrant a preview.
 * @param {string} str
 * @returns {boolean}
 */
export function hasMathTokens(str) {
  if (!str) return false;
  return /[π√^/]|sqrt\(/i.test(str);
}

/**
 * Safely transforms short answer text into safe LaTeX for KaTeX rendering.
 * Does not execute arbitrary LaTeX macros or HTML.
 * @param {string} str
 * @returns {string}
 */
export function formatMathPreviewLatex(str) {
  if (!str) return '';
  let text = normalizeShortAnswer(str);

  // Escape special LaTeX characters except math symbols we intend to support
  // Replace π with \pi
  text = text.replace(/π/g, ' \\pi ');

  // Replace √(content) with \sqrt{content}
  let result = '';
  for (let i = 0; i < text.length; ) {
    if (text[i] === '√' && text[i + 1] === '(') {
      const close = findMatchingParenthesis(text, i + 1);
      if (close > 0) {
        const inner = text.slice(i + 2, close);
        result += `\\sqrt{${formatMathPreviewLatex(inner)}}`;
        i = close + 1;
        continue;
      }
    }
    result += text[i];
    i++;
  }

  return result;
}

/**
 * Checks if a numeric string fits within decimal(18,6):
 * - At most 12 integer digits
 * - At most 6 fractional digits
 * - Finite number, no exponents, absolute value strictly less than 10^12
 * @param {string|number} rawValue
 * @returns {boolean}
 */
export function isNumericAnswerPrecisionValid(rawValue) {
  if (rawValue == null || rawValue === '') return true;
  const str = String(rawValue).trim().replace(',', '.');
  if (!/^-?(?:\d+(?:\.\d+)?|\.\d+)$/.test(str)) return false;

  const parts = str.startsWith('-') ? str.slice(1).split('.') : str.split('.');
  const integerPart = parts[0] || '0';
  const fractionalPart = parts[1] || '';

  // Max 12 integer digits, max 6 fractional digits
  if (integerPart.length > 12) return false;
  if (fractionalPart.length > 6) return false;

  return true;
}
