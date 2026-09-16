import { describe, expect, it } from 'vitest';
import {
  editorStateToAvailabilityRequest,
  editorStateToBlueprintRequest,
  isDetailRowComplete,
  hasIncompleteAllocationRows,
  generateClientId,
  detailToEditorState,
} from './blueprintMappers';

describe('Blueprint Availability Mappers and Completeness Check', () => {
  it('identifies complete and incomplete rows correctly for homogeneous and mixed sections', () => {
    const homogeneousSection = {
      questionType: 'SingleChoice',
      scoringRule: 'AllOrNothing',
    };

    // Incomplete: missing tagId or difficultyId or quantity <= 0
    expect(isDetailRowComplete(homogeneousSection, { tagId: '', difficultyId: 'diff-1', quantity: 1 })).toBe(false);
    expect(isDetailRowComplete(homogeneousSection, { tagId: 'tag-1', difficultyId: '', quantity: 1 })).toBe(false);
    expect(isDetailRowComplete(homogeneousSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 0 })).toBe(false);
    expect(isDetailRowComplete(homogeneousSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: -1 })).toBe(false);
    expect(isDetailRowComplete(homogeneousSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 'abc' })).toBe(false);

    // Complete homogeneous
    expect(isDetailRowComplete(homogeneousSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 2 })).toBe(true);

    // Mixed section
    const mixedSection = {
      questionType: 'Mixed',
      scoringRule: null,
    };

    // Mixed without row questionType or with invalid rule
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: null, scoringRule: null })).toBe(false);
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'WeightedParts' })).toBe(false);
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'Composite', scoringRule: 'AllOrNothing' })).toBe(false);

    // Complete mixed rows
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'SingleChoice', scoringRule: 'AllOrNothing' })).toBe(true);
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'Composite', scoringRule: 'WeightedParts' })).toBe(true);
    expect(isDetailRowComplete(mixedSection, { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1, questionType: 'Composite', scoringRule: 'TieredTrueFalse' })).toBe(true);
  });

  it('maps Homogeneous sections with section-level policy and omits questionType/scoringRule on rows', () => {
    const editorState = {
      grade: '12',
      sections: [
        {
          clientSectionId: 'sec-homo-1',
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          details: [
            {
              clientRowId: 'row-1',
              tagId: 'tag-math-1',
              difficultyId: 'diff-1',
              quantity: 5,
              questionType: null,
              scoringRule: null,
            },
          ],
        },
      ],
    };

    const request = editorStateToAvailabilityRequest(editorState);
    expect(request).toEqual({
      grade: 12,
      sections: [
        {
          clientSectionId: 'sec-homo-1',
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          rows: [
            {
              clientRowId: 'row-1',
              tagId: 'tag-math-1',
              difficultyId: 'diff-1',
              quantity: 5,
            },
          ],
        },
      ],
    });

    // Verify row has NO questionType or scoringRule properties
    expect('questionType' in request.sections[0].rows[0]).toBe(false);
    expect('scoringRule' in request.sections[0].rows[0]).toBe(false);
  });

  it('maps Mixed sections with section scoringRule null and row-level policy', () => {
    const editorState = {
      grade: '10',
      sections: [
        {
          clientSectionId: 'sec-mix-1',
          questionType: 'Mixed',
          scoringRule: null,
          details: [
            {
              clientRowId: 'row-single',
              tagId: 'tag-1',
              difficultyId: 'diff-1',
              quantity: 2,
              questionType: 'SingleChoice',
              scoringRule: 'AllOrNothing',
            },
            {
              clientRowId: 'row-comp',
              tagId: 'tag-2',
              difficultyId: 'diff-2',
              quantity: 1,
              questionType: 'Composite',
              scoringRule: 'WeightedParts',
            },
          ],
        },
      ],
    };

    const request = editorStateToAvailabilityRequest(editorState);
    expect(request).toEqual({
      grade: 10,
      sections: [
        {
          clientSectionId: 'sec-mix-1',
          questionType: 'Mixed',
          scoringRule: null,
          rows: [
            {
              clientRowId: 'row-single',
              tagId: 'tag-1',
              difficultyId: 'diff-1',
              quantity: 2,
              questionType: 'SingleChoice',
              scoringRule: 'AllOrNothing',
            },
            {
              clientRowId: 'row-comp',
              tagId: 'tag-2',
              difficultyId: 'diff-2',
              quantity: 1,
              questionType: 'Composite',
              scoringRule: 'WeightedParts',
            },
          ],
        },
      ],
    });
  });

  it('filters out incomplete rows and skips empty sections in availability request', () => {
    const editorState = {
      grade: '11',
      sections: [
        {
          clientSectionId: 'sec-1',
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          details: [
            { clientRowId: 'row-1', tagId: 'tag-1', difficultyId: 'diff-1', quantity: 2 },
            { clientRowId: 'row-incomplete', tagId: '', difficultyId: 'diff-2', quantity: 1 },
          ],
        },
        {
          clientSectionId: 'sec-empty',
          questionType: 'TrueFalse',
          scoringRule: 'AllOrNothing',
          details: [
            { clientRowId: 'row-empty', tagId: '', difficultyId: '', quantity: 1 },
          ],
        },
      ],
    };

    const request = editorStateToAvailabilityRequest(editorState);
    expect(request.sections).toHaveLength(1);
    expect(request.sections[0].clientSectionId).toBe('sec-1');
    expect(request.sections[0].rows).toHaveLength(1);
    expect(request.sections[0].rows[0].clientRowId).toBe('row-1');
  });

  it('returns null if there are no complete sections to preview', () => {
    const editorState = {
      grade: '12',
      sections: [
        {
          clientSectionId: 'sec-1',
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          details: [
            { clientRowId: 'row-1', tagId: '', difficultyId: '', quantity: 1 },
          ],
        },
      ],
    };
    expect(editorStateToAvailabilityRequest(editorState)).toBeNull();
  });

  it('never sends clientSectionId or clientRowId in editorStateToBlueprintRequest create/update payload', () => {
    const editorState = {
      blueprintName: 'Đề mẫu 2026',
      grade: '12',
      totalQuestions: '2',
      totalScore: '10',
      durationMinutes: '60',
      sections: [
        {
          clientSectionId: 'sec-client-123',
          sectionCode: 'P1',
          sectionName: 'Phần 1',
          questionType: 'Mixed',
          instructionText: '',
          totalQuestions: '2',
          scoreBudget: '10',
          scoringRule: null,
          details: [
            {
              clientRowId: 'row-client-456',
              tagId: 'tag-1',
              difficultyId: 'diff-1',
              quantity: '2',
              questionType: 'SingleChoice',
              scoringRule: 'AllOrNothing',
            },
          ],
        },
      ],
    };

    const payload = editorStateToBlueprintRequest(editorState);
    expect('clientSectionId' in payload.sections[0]).toBe(false);
    expect('clientRowId' in payload.sections[0].details[0]).toBe(false);
  });

  it('hasIncompleteAllocationRows returns true when any row is incomplete and false when all are complete', () => {
    const stateIncomplete = {
      grade: '12',
      sections: [
        {
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          details: [
            { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1 },
            { tagId: '', difficultyId: 'diff-2', quantity: 2 },
          ],
        },
      ],
    };
    expect(hasIncompleteAllocationRows(stateIncomplete)).toBe(true);

    const stateComplete = {
      grade: '12',
      sections: [
        {
          questionType: 'SingleChoice',
          scoringRule: 'AllOrNothing',
          details: [
            { tagId: 'tag-1', difficultyId: 'diff-1', quantity: 1 },
            { tagId: 'tag-2', difficultyId: 'diff-2', quantity: 2 },
          ],
        },
      ],
    };
    expect(hasIncompleteAllocationRows(stateComplete)).toBe(false);
  });
});
