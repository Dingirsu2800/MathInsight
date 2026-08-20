export function toFiniteNumericAnswer(rawValue) {
  if (rawValue == null) return null;

  const normalized = String(rawValue).trim().replace(',', '.');
  if (!/^-?(?:\d+(?:\.\d+)?|\.\d+)$/.test(normalized)) return null;

  const value = Number(normalized);
  return Number.isFinite(value) ? value : null;
}

export function toAutoSavePayload(answers) {
  return Object.entries(answers).map(([questionId, answer]) => ({
    questionId,
    answerId: answer.answerId || null,
    shortAnswerText: answer.shortAnswerText?.trim() || null,
    timeSpent: answer.timeSpent || 0,
    selectedOptions: (answer.selectedOptions || []).map((answerId) => ({ answerId })),
    parts: (answer.parts || []).map((part) => ({
      partId: part.partId,
      booleanAnswer: part.booleanAnswer ?? null,
      textAnswer: part.textAnswer?.trim() || null,
      numericAnswer: toFiniteNumericAnswer(part.numericAnswer),
    })),
  }));
}
