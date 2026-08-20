import { describe, expect, it } from 'vitest';
import { toAutoSavePayload } from './answerPayload';

function payloadFor(numericAnswer) {
  return toAutoSavePayload({
    'question-1': {
      shortAnswerText: '  π  ',
      parts: [{
        partId: 'part-1',
        textAnswer: '  vô nghiệm  ',
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
  ])('normalizes numeric answer %s', (raw, expected) => {
    expect(payloadFor(raw).parts[0].numericAnswer).toBe(expected);
  });

  it.each(['', ' ', '-', '.', ',', '1,2,3', '1..2', 'Infinity'])
    ('serializes incomplete or invalid numeric answer %s as null', (raw) => {
      expect(payloadFor(raw).parts[0].numericAnswer).toBeNull();
    });

  it('trims text answers without changing their content', () => {
    const payload = payloadFor('1');
    expect(payload.shortAnswerText).toBe('π');
    expect(payload.parts[0].textAnswer).toBe('vô nghiệm');
  });
});
