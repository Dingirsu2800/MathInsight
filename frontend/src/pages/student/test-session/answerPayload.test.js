import { describe, expect, it } from 'vitest';
import { toAutoSavePayload } from './answerPayload';

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
  ])('normalizes numeric part answer %s', (raw, expected) => {
    expect(payloadFor('1', '1', raw).parts[0].numericAnswer).toBe(expected);
  });

  it.each(['', ' ', '-', '.', ',', '1,2,3', '1..2', 'Infinity'])
    ('serializes incomplete or invalid numeric answer %s as null', (raw) => {
      expect(payloadFor('1', '1', raw).parts[0].numericAnswer).toBeNull();
    });

  it.each([
    ['1,5', '1.5'],
    ['-3,25', '-3.25'],
    ['12', '12'],
    ['-5', '-5'],
  ])('normalizes shortAnswerText and composite textAnswer from %s to %s', (raw, expected) => {
    const payload = payloadFor(raw, raw, '1');
    expect(payload.shortAnswerText).toBe(expected);
    expect(payload.parts[0].textAnswer).toBe(expected);
  });

  it.each(['-', '.', ',', '  -  ', '  ,  ', 'π', 'abc', '', null, undefined])
    ('serializes incomplete or non-numeric short answers (%s) as null', (raw) => {
      const payload = payloadFor(raw, raw, '1');
      expect(payload.shortAnswerText).toBeNull();
      expect(payload.parts[0].textAnswer).toBeNull();
    });
});
