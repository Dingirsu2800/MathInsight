# Short Answer Input UX Design

**Status:** Approved for implementation planning
**Scope:** Student test-session answer inputs only

## Problem

The current Student test session labels `SHORT_ANSWER` as `Tu luan` and renders
it as a three-line textarea. That wording suggests free-form essay grading, while
the grading engine only compares a short submitted string with one stored correct
answer after trimming whitespace and ignoring letter case.

Composite questions already distinguish text parts from numeric parts, but the
numeric browser input does not explicitly support Vietnamese decimal commas.

## Decision

Use answer controls based on the existing answer contract instead of forcing all
short answers to be numeric.

### Question-level `SHORT_ANSWER`

- Display the Vietnamese label `Tra loi ngan`, not `Tu luan`.
- Render a single-line text input.
- Accept letters, digits, spaces, and ordinary mathematical symbols.
- Limit input to 100 characters, matching QuestionBank validation.
- Use the placeholder `Nhap dap an ngan...`.

### Composite `ShortAnswer` part

- Render a single-line text input.
- Limit input to 255 characters, matching persistence constraints.
- Use the placeholder `Nhap dap an ngan...`.

### Composite `NumericAnswer` part

- Render a single compact text input with `inputMode="decimal"` rather than an
  OTP-style control.
- Accept an optional leading minus sign and one decimal separator (`.` or `,`).
- Normalize a decimal comma to a decimal point at the API payload boundary.
- Submit only a finite numeric value; an empty or incomplete value remains
  unanswered instead of becoming `NaN` or an invalid JSON value.
- Preserve the existing `decimal(18,6)` backend contract.

## Why Not OTP Inputs

Math answers have variable length and may contain a sign or decimal separator.
Segmented OTP boxes make editing, pasting, keyboard navigation, and accessibility
worse without improving grading correctness.

## Grading Boundary

This checkpoint does not change grading semantics. Text answers remain exact
trimmed, case-insensitive matches. Equivalent aliases such as `pi` and `π`, or
accented and unaccented Vietnamese phrases, are not treated as equal.

Questions expecting symbolic text must state the required answer format clearly,
for example `Nhap "vo nghiem"`.

Accepted-answer aliases, symbolic canonicalization, and a question-level
`AnswerFormat` contract are deferred to a separate grading design.

## Compatibility

- No database or schema change.
- No Testing or Grading API shape change.
- Existing autosave and submit flows remain unchanged.
- Existing text answers such as `pi`, `A`, and `vo nghiem` remain representable.
- THPT-style numeric short answers remain supported through the current text
  question contract and the explicit numeric composite-part contract.

## Verification

- Component tests for question-level text, part text, and part numeric inputs.
- Numeric comma normalization test (`1,5` becomes `1.5`).
- Negative and decimal value tests.
- Empty and incomplete numeric input must serialize as `null`.
- Existing autosave, hydration, answered-count, and submit tests remain green.
- Frontend production build and browser smoke for one question of each input type.
