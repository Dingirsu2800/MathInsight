import { describe, expect, it } from 'vitest';
import {
  mapOcrDraftToEditorStatePatch,
  mapBackendTypeToUiType,
  mapBackendDifficultyLevelToUi,
} from './questionMappers';

describe('questionMappers - OCR Draft mapping security & observation safety', () => {
  it('maps all options to isCorrect: false regardless of detected marks', () => {
    const ocrDraft = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Cho hàm số $y = f(x)$...',
      solutionContent: 'Lời giải chi tiết...',
      answers: [
        { content: 'A. 1', suggestedIsCorrect: true, detectedMark: 'Circled' },
        { content: 'B. 2', suggestedIsCorrect: false, detectedMark: 'None' },
        { content: 'C. 3', suggestedIsCorrect: null, detectedMark: 'Ticked' },
        { content: 'D. 4', suggestedIsCorrect: null, detectedMark: 'Crossed' },
      ],
    };

    const patch = mapOcrDraftToEditorStatePatch(ocrDraft);

    expect(patch.questionType).toBe('SINGLE_CHOICE');
    expect(patch.options).toHaveLength(4);
    patch.options.forEach((opt) => {
      expect(opt.isCorrect).toBe(false);
    });
  });

  it('preserves existing editor solution when OCR solutionContent is empty', () => {
    const ocrDraftWithoutSolution = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Tìm nguyên hàm...',
      solutionContent: '',
      answers: [
        { content: 'A. x + C' },
        { content: 'B. x^2 + C' },
      ],
    };

    const currentEditorState = {
      solutionContent: 'Lời giải cũ đã được nhập thủ công',
    };

    const patch = mapOcrDraftToEditorStatePatch(ocrDraftWithoutSolution, currentEditorState);

    expect(patch.solutionContent).toBe('Lời giải cũ đã được nhập thủ công');
  });

  it('updates solutionContent when OCR solutionContent is present', () => {
    const ocrDraftWithSolution = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Tìm nguyên hàm...',
      solutionContent: 'Lời giải mới từ OCR',
      answers: [
        { content: 'A. x + C' },
        { content: 'B. x^2 + C' },
      ],
    };

    const currentEditorState = {
      solutionContent: 'Lời giải cũ',
    };

    const patch = mapOcrDraftToEditorStatePatch(ocrDraftWithSolution, currentEditorState);

    expect(patch.solutionContent).toBe('Lời giải mới từ OCR');
  });
});
