import { describe, expect, it } from 'vitest';
import {
  countUnicodeScalars,
  formatMathPreviewLatex,
  hasForbiddenCharacters,
  hasMathTokens,
  isNumericAnswerPrecisionValid,
  normalizeShortAnswer,
  normalizeSqrtAliases,
  validateShortAnswer,
} from './shortAnswer';

describe('shortAnswer utility', () => {
  describe('countUnicodeScalars', () => {
    it('counts ASCII characters', () => {
      expect(countUnicodeScalars('abc')).toBe(3);
    });

    it('counts accented Vietnamese characters properly', () => {
      expect(countUnicodeScalars('Hà Nội')).toBe(6);
    });

    it('counts mathematical Unicode symbols', () => {
      expect(countUnicodeScalars('2π+√(3)')).toBe(7);
    });
  });

  describe('hasForbiddenCharacters', () => {
    it('returns false for valid math and text characters', () => {
      expect(hasForbiddenCharacters('2π + √(2) / 3')).toBe(false);
      expect(hasForbiddenCharacters('Hà Nội')).toBe(false);
      expect(hasForbiddenCharacters('x > 0 < 5')).toBe(false);
    });

    it('returns true for newlines and carriage returns', () => {
      expect(hasForbiddenCharacters('line1\nline2')).toBe(true);
      expect(hasForbiddenCharacters('line1\r\nline2')).toBe(true);
    });

    it('returns true for control characters', () => {
      expect(hasForbiddenCharacters('abc\u0000def')).toBe(true);
      expect(hasForbiddenCharacters('test\u0007bell')).toBe(true);
    });
  });

  describe('normalizeSqrtAliases', () => {
    it('converts sqrt(2) to √(2)', () => {
      expect(normalizeSqrtAliases('sqrt(2)')).toBe('√(2)');
    });

    it('handles nested sqrt(sqrt(3))', () => {
      expect(normalizeSqrtAliases('sqrt(sqrt(3))')).toBe('√(√(3))');
    });

    it('does not convert word ending with sqrt like asqrt(2)', () => {
      expect(normalizeSqrtAliases('asqrt(2)')).toBe('asqrt(2)');
    });
  });

  describe('normalizeShortAnswer', () => {
    it('normalizes spaces and trims ends', () => {
      expect(normalizeShortAnswer('  Hà   Nội  ')).toBe('Hà Nội');
    });

    it('converts standalone pi to π', () => {
      expect(normalizeShortAnswer('2 pi + 3')).toBe('2 π + 3');
      expect(normalizeShortAnswer('PI')).toBe('π');
    });

    it('does not replace pi inside words like spin or piano', () => {
      expect(normalizeShortAnswer('spin')).toBe('spin');
      expect(normalizeShortAnswer('piano')).toBe('piano');
    });

    it('converts sqrt alias to symbol', () => {
      expect(normalizeShortAnswer('sqrt(2)')).toBe('√(2)');
    });
  });

  describe('validateShortAnswer', () => {
    it('passes for empty when not required', () => {
      expect(validateShortAnswer('', { required: false })).toEqual({ isValid: true });
      expect(validateShortAnswer(null, { required: false })).toEqual({ isValid: true });
    });

    it('fails for empty when required', () => {
      const res = validateShortAnswer('', { required: true });
      expect(res.isValid).toBe(false);
      expect(res.error).toBe('Vui lòng nhập câu trả lời.');
    });

    it('fails when exceeding 100 Unicode scalar values', () => {
      const longStr = 'a'.repeat(101);
      const res = validateShortAnswer(longStr);
      expect(res.isValid).toBe(false);
      expect(res.error).toMatch(/không được vượt quá 100 ký tự/);
    });

    it('fails when containing newline or control characters', () => {
      const res = validateShortAnswer('answer\nwith newline');
      expect(res.isValid).toBe(false);
      expect(res.error).toMatch(/không được chứa ký tự xuống dòng/);
    });
  });

  describe('hasMathTokens', () => {
    it('detects math tokens', () => {
      expect(hasMathTokens('2π')).toBe(true);
      expect(hasMathTokens('√(5)')).toBe(true);
      expect(hasMathTokens('sqrt(2)')).toBe(true);
      expect(hasMathTokens('1/2')).toBe(true);
      expect(hasMathTokens('x^2')).toBe(true);
    });

    it('returns false for plain text', () => {
      expect(hasMathTokens('Hà Nội')).toBe(false);
      expect(hasMathTokens('123')).toBe(false);
      expect(hasMathTokens('')).toBe(false);
    });
  });

  describe('formatMathPreviewLatex', () => {
    it('converts π and root to KaTeX friendly format', () => {
      expect(formatMathPreviewLatex('2π')).toBe('2 \\pi ');
      expect(formatMathPreviewLatex('√(2)')).toBe('\\sqrt{2}');
    });
  });

  describe('isNumericAnswerPrecisionValid', () => {
    it('accepts valid decimal numbers within 12 integer and 6 fractional digits', () => {
      expect(isNumericAnswerPrecisionValid('123456789012.123456')).toBe(true);
      expect(isNumericAnswerPrecisionValid('-999999999999.999999')).toBe(true);
      expect(isNumericAnswerPrecisionValid('0.5')).toBe(true);
      expect(isNumericAnswerPrecisionValid('1,5')).toBe(true);
      expect(isNumericAnswerPrecisionValid('-2')).toBe(true);
    });

    it('rejects numbers with more than 12 integer digits', () => {
      expect(isNumericAnswerPrecisionValid('1000000000000')).toBe(false);
      expect(isNumericAnswerPrecisionValid('1234567890123.45')).toBe(false);
    });

    it('rejects numbers with more than 6 fractional digits', () => {
      expect(isNumericAnswerPrecisionValid('1.1234567')).toBe(false);
    });

    it('rejects exponent format or invalid strings', () => {
      expect(isNumericAnswerPrecisionValid('1e5')).toBe(false);
      expect(isNumericAnswerPrecisionValid('abc')).toBe(false);
      expect(isNumericAnswerPrecisionValid('1..2')).toBe(false);
    });
  });
});
