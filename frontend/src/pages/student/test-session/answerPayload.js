import { isNumericAnswerPrecisionValid } from '../../../utils/shortAnswer';

export function toFiniteNumericAnswer(rawValue) {
  if (rawValue == null) return null;

  const normalized = String(rawValue).trim().replace(',', '.');
  if (normalized === '' || !isNumericAnswerPrecisionValid(normalized)) return null;

  const value = Number(normalized);
  return Number.isFinite(value) ? value : null;
}

export function sanitizeShortAnswerPayload(rawValue) {
  if (rawValue == null) return null;
  const str = String(rawValue).trim();
  return str.length > 0 ? str : null;
}

export function toAutoSavePayload(answers) {
  return Object.entries(answers).map(([questionId, answer]) => ({
    questionId,
    answerId: answer.answerId || null,
    shortAnswerText: sanitizeShortAnswerPayload(answer.shortAnswerText),
    timeSpent: answer.timeSpent || 0,
    selectedOptions: (answer.selectedOptions || []).map((answerId) => ({ answerId })),
    parts: (answer.parts || []).map((part) => ({
      partId: part.partId,
      booleanAnswer: part.booleanAnswer ?? null,
      textAnswer: sanitizeShortAnswerPayload(part.textAnswer),
      numericAnswer: toFiniteNumericAnswer(part.numericAnswer),
    })),
  }));
}
