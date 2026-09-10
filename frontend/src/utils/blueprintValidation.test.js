import { describe, it, expect } from 'vitest';
import { validateBlueprint } from './blueprintValidation';
import { detailToEditorState, editorStateToBlueprintRequest } from './blueprintMappers';

describe('blueprintValidation with variable part count', () => {
  const baseValidState = {
    blueprintName: 'Đề kiểm tra Toán 12',
    grade: 12,
    totalQuestions: 10,
    totalScore: 10,
    durationMinutes: 45,
    sections: [
      {
        sectionName: 'Phần 1 - Trắc nghiệm',
        totalQuestions: 6,
        scoreBudget: 6,
        questionType: 'SingleChoice',
        scoringRule: 'AllOrNothing',
        partCountPerQuestion: null,
        details: [
          { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 6 },
        ],
      },
      {
        sectionName: 'Phần 2 - Đúng Sai Phân Bậc',
        totalQuestions: 4,
        scoreBudget: 4,
        questionType: 'Composite',
        scoringRule: 'TieredTrueFalse',
        partCountPerQuestion: null,
        details: [
          { tagId: 'tag-2', difficultyId: 'diff-2', quantity: 4 },
        ],
      },
    ],
  };

  it('validates a composite section with TieredTrueFalse and null partCountPerQuestion as valid', () => {
    const result = validateBlueprint(baseValidState, true);
    expect(result.isValid).toBe(true);
    expect(result.errors).toEqual([]);
  });

  it('validates a composite section with WeightedParts and null partCountPerQuestion as valid', () => {
    const state = {
      ...baseValidState,
      sections: [
        baseValidState.sections[0],
        {
          ...baseValidState.sections[1],
          scoringRule: 'WeightedParts',
        },
      ],
    };
    const result = validateBlueprint(state, true);
    expect(result.isValid).toBe(true);
    expect(result.errors).toEqual([]);
  });

  it('still enforces total questions matching sum of sections', () => {
    const state = {
      ...baseValidState,
      totalQuestions: 15, // sections sum to 10
    };
    const result = validateBlueprint(state, true);
    expect(result.isValid).toBe(false);
    expect(result.errors.some((err) => err.includes('Tổng số câu của các phần'))).toBe(true);
  });

  it('still enforces total score matching sum of section budgets', () => {
    const state = {
      ...baseValidState,
      totalScore: 9, // sections budget sum to 10
    };
    const result = validateBlueprint(state, true);
    expect(result.isValid).toBe(false);
    expect(result.errors.some((err) => err.includes('Tổng điểm của các phần'))).toBe(true);
  });

  it('maps legacy blueprint detail with partCountPerQuestion and sends null in request', () => {
    const legacyDetail = {
      blueprintName: 'Đề mẫu cũ',
      grade: 12,
      totalQuestions: 4,
      totalScore: 10,
      durationMinutes: 90,
      sections: [
        {
          sectionName: 'Phần cũ',
          questionType: 'Composite',
          scoringRule: 'TieredTrueFalse',
          partCountPerQuestion: 4,
          totalQuestions: 4,
          scoreBudget: 10,
          details: [
            { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 4 },
          ],
        },
      ],
    };

    const editorState = detailToEditorState(legacyDetail);
    expect(editorState.sections[0].partCountPerQuestion).toBe(4);

    const request = editorStateToBlueprintRequest(editorState);
    expect(request.sections[0].partCountPerQuestion).toBeNull();
    expect(request.sections[0].scoringRule).toBe('TieredTrueFalse');
  });
});
