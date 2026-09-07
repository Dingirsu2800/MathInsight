import { describe, expect, it } from 'vitest';
import { toAutoSavePayload, toFiniteNumericAnswer } from './answerPayload';

function payloadFor(shortAnswerText, partTextAnswer, numericAnswer) {
  return toAutoSavePayload({
    'question-1': {
      shortAnswerText,
      parts: [{
        partId: 'part-1',
        textAnswer: partTextAnswer,
        numericAnswer,
      }],
    },
  })[0];
}

describe('toAutoSavePayload', () => {
  it.each([
    ['1,5', 1.5],
    ['-2,75', -2.75],
    ['.5', 0.5],
    ['0.125', 0.125],
    ['123456789012.123456', 123456789012.123456],
  ])('normalizes valid numeric part answer %s to %s', (raw, expected) => {
    expect(payloadFor('1', '1', raw).parts[0].numericAnswer).toBe(expected);
  });

  it.each([
    '', ' ', '-', '.', ',', '1,2,3', '1..2', 'Infinity',
    '1e5', // Exponent notation rejected
    '1000000000000', // Exceeds 12 integer digits
    '1.1234567', // Exceeds 6 fractional digits
  ])('serializes incomplete, exponent, or out-of-precision numeric answer %s as null', (raw) => {
    expect(payloadFor('1', '1', raw).parts[0].numericAnswer).toBeNull();
  });

  it.each([
    '1,5',
    '-3,25',
    '12',
    '-5',
    '2π',
    '√(2)',
    'Hà Nội',
    'x > 5',
  ])('preserves shortAnswerText and composite textAnswer string %s in payload', (raw) => {
    const payload = payloadFor(raw, raw, '1');
    expect(payload.shortAnswerText).toBe(raw);
    expect(payload.parts[0].textAnswer).toBe(raw);
  });

  it.each(['', '   ', null, undefined])
    ('serializes empty or whitespace-only short answers (%s) as null', (raw) => {
      const payload = payloadFor(raw, raw, '1');
      expect(payload.shortAnswerText).toBeNull();
      expect(payload.parts[0].textAnswer).toBeNull();
    });
});
