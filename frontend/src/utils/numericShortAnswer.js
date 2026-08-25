const EDITING_PATTERN = /^-?\d*(?:[.,]\d*)?$/;
const COMPLETE_PATTERN = /^-?\d+(?:[.,]\d+)?$/;

export function isNumericShortAnswerEditingValue(value) {
  return value === '' || (value.length <= 100 && EDITING_PATTERN.test(value));
}

export function isCompleteNumericShortAnswer(value) {
  return COMPLETE_PATTERN.test(String(value ?? '').trim());
}

export function normalizeNumericShortAnswer(value) {
  const trimmed = String(value ?? '').trim();
  return isCompleteNumericShortAnswer(trimmed) ? trimmed.replace(',', '.') : null;
}
