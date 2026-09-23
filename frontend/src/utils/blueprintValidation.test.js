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

  describe('Mixed section validation and mapping', () => {
    const validMixedState = {
      blueprintName: 'Đề kiểm tra Hỗn hợp 12',
      grade: 12,
      totalQuestions: 4,
      totalScore: 10,
      durationMinutes: 45,
      sections: [
        {
          sectionName: 'Phần 1 - Hỗn hợp',
          totalQuestions: 4,
          scoreBudget: 10,
          questionType: 'Mixed',
          scoringRule: null,
          partCountPerQuestion: null,
          details: [
            { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' },
            { tagId: 'tag-1', difficultyId: 'diff-2', quantity: 1, questionType: 'ShortAnswer', scoringRule: 'AllOrNothing' },
            { tagId: 'tag-2', difficultyId: 'diff-2', quantity: 1, questionType: 'Composite', scoringRule: 'TieredTrueFalse' },
            { tagId: 'tag-2', difficultyId: 'diff-3', quantity: 1, questionType: 'Composite', scoringRule: 'WeightedParts' },
          ],
        },
      ],
    };

    it('validates a valid Mixed section with distinct row policies', () => {
      const result = validateBlueprint(validMixedState, true);
      expect(result.isValid).toBe(true);
      expect(result.errors).toEqual([]);
    });

    it('rejects a Mixed section with a non-null section scoringRule', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            scoringRule: 'AllOrNothing',
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(false);
      expect(result.errors.some((e) => e.includes('Phần thi Hỗn hợp không được thiết lập quy tắc chấm'))).toBe(true);
    });

    it('rejects Mixed detail row with invalid questionType', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            details: [
              ...validMixedState.sections[0].details.slice(0, 3),
              { tagId: 'tag-2', difficultyId: 'diff-3', quantity: 1, questionType: 'Mixed', scoringRule: 'AllOrNothing' },
            ],
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(false);
      expect(result.errors.some((e) => e.includes('Loại câu hỏi không hợp lệ'))).toBe(true);
    });

    it('rejects Mixed detail Composite row with AllOrNothing', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            details: [
              ...validMixedState.sections[0].details.slice(0, 3),
              { tagId: 'tag-2', difficultyId: 'diff-3', quantity: 1, questionType: 'Composite', scoringRule: 'AllOrNothing' },
            ],
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(false);
      expect(result.errors.some((e) => e.includes('Quy tắc chấm Composite không hợp lệ'))).toBe(true);
    });

    it('rejects Mixed detail non-Composite row with WeightedParts', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            details: [
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'WeightedParts' },
              ...validMixedState.sections[0].details.slice(1),
            ],
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(false);
      expect(result.errors.some((e) => e.includes('Câu không phải Composite phải dùng AllOrNothing'))).toBe(true);
    });

    it('allows same topic and difficulty in Mixed section when questionType or scoringRule differ', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            details: [
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' },
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'ShortAnswer', scoringRule: 'AllOrNothing' },
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'Composite', scoringRule: 'TieredTrueFalse' },
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'Composite', scoringRule: 'WeightedParts' },
            ],
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(true);
      expect(result.errors).toEqual([]);
    });

    it('rejects duplicate topic + difficulty + questionType + scoringRule in Mixed section', () => {
      const state = {
        ...validMixedState,
        sections: [
          {
            ...validMixedState.sections[0],
            details: [
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' },
              { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' },
              ...validMixedState.sections[0].details.slice(2),
            ],
          },
        ],
      };
      const result = validateBlueprint(state, true);
      expect(result.isValid).toBe(false);
      expect(result.errors.some((e) => e.includes('Không được trùng chủ đề, độ khó, loại câu hỏi và quy tắc chấm'))).toBe(true);
    });

    it('maps Mixed editorState to BE request with section scoringRule null and row-level policies', () => {
      const request = editorStateToBlueprintRequest(validMixedState);
      expect(request.sections[0].questionType).toBe('Mixed');
      expect(request.sections[0].scoringRule).toBeNull();
      expect(request.sections[0].partCountPerQuestion).toBeNull();
      expect(request.sections[0].details).toEqual([
        { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' },
        { tagId: 'tag-1', difficultyId: 'diff-2', quantity: 1, questionType: 'ShortAnswer', scoringRule: 'AllOrNothing' },
        { tagId: 'tag-2', difficultyId: 'diff-2', quantity: 1, questionType: 'Composite', scoringRule: 'TieredTrueFalse' },
        { tagId: 'tag-2', difficultyId: 'diff-3', quantity: 1, questionType: 'Composite', scoringRule: 'WeightedParts' },
      ]);
    });

    it('maps homogeneous editorState to BE request with detail questionType and scoringRule null', () => {
      const request = editorStateToBlueprintRequest(baseValidState);
      expect(request.sections[0].questionType).toBe('SingleChoice');
      expect(request.sections[0].scoringRule).toBe('AllOrNothing');
      expect(request.sections[0].details[0].questionType).toBeNull();
      expect(request.sections[0].details[0].scoringRule).toBeNull();

      expect(request.sections[1].questionType).toBe('Composite');
      expect(request.sections[1].scoringRule).toBe('TieredTrueFalse');
      expect(request.sections[1].details[0].questionType).toBeNull();
      expect(request.sections[1].details[0].scoringRule).toBeNull();
    });

    it('roundtrips Mixed BE response through detailToEditorState and editorStateToBlueprintRequest preserving policies', () => {
      const beResponse = {
        blueprintId: 'bp-mixed-1',
        blueprintName: 'Mixed Practice',
        grade: 12,
        totalQuestions: 3,
        totalScore: 10,
        durationMinutes: 45,
        status: 'Draft',
        sections: [
          {
            blueprintSectionId: 'sec-1',
            sectionOrder: 1,
            sectionCode: 'M1',
            sectionName: 'Hỗn hợp',
            questionType: 'Mixed',
            scoringRule: null,
            partCountPerQuestion: null,
            totalQuestions: 3,
            scoreBudget: 10,
            details: [
              {
                blueprintDetailId: 'det-1',
                tagId: 'tag-algebra',
                difficultyId: 'diff-1',
                quantity: 1,
                questionType: 'SingleChoice',
                scoringRule: 'AllOrNothing',
              },
              {
                blueprintDetailId: 'det-2',
                tagId: 'tag-geometry',
                difficultyId: 'diff-2',
                quantity: 2,
                questionType: 'Composite',
                scoringRule: 'WeightedParts',
              },
            ],
          },
        ],
      };

      const editorState = detailToEditorState(beResponse);
      expect(editorState.sections[0].questionType).toBe('Mixed');
      expect(editorState.sections[0].scoringRule).toBeNull();
      expect(editorState.sections[0].details[0].questionType).toBe('SingleChoice');
      expect(editorState.sections[0].details[0].scoringRule).toBe('AllOrNothing');
      expect(editorState.sections[0].details[1].questionType).toBe('Composite');
      expect(editorState.sections[0].details[1].scoringRule).toBe('WeightedParts');

      const request = editorStateToBlueprintRequest(editorState);
      expect(request.sections[0].scoringRule).toBeNull();
      expect(request.sections[0].details[0].questionType).toBe('SingleChoice');
      expect(request.sections[0].details[0].scoringRule).toBe('AllOrNothing');
      expect(request.sections[0].details[1].questionType).toBe('Composite');
      expect(request.sections[0].details[1].scoringRule).toBe('WeightedParts');
    });
  });
});

