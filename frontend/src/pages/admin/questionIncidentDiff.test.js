import { describe, expect, it } from 'vitest';
import { compareVersions, diffText } from './questionIncidentDiff';

const version = (overrides = {}) => ({
  questionContent: 'Một câu hỏi cũ',
  questionAnswer: 'Lời giải cũ',
  answersSnapshot: JSON.stringify({
    QuestionType: 'SINGLE_CHOICE',
    DifficultyId: 'easy',
    Grade: 10,
    Topics: [{ TagId: 'algebra' }],
    Answers: [
      { AnswerId: 'a', AnswerContent: 'Đáp án A', IsCorrect: true },
      { AnswerId: 'b', AnswerContent: 'Đáp án B', IsCorrect: false }
    ],
    Parts: [],
    SolutionContent: 'Lời giải cũ'
  }),
  ...overrides
});

describe('question incident diff', () => {
  it('marks unchanged text', () => {
    expect(diffText('giữ nguyên', 'giữ nguyên').status).toBe('unchanged');
  });

  it('marks added, removed, and modified text', () => {
    expect(diffText('A B', 'A B C').segments.some((segment) => segment.type === 'added')).toBe(true);
    expect(diffText('A B C', 'A B').segments.some((segment) => segment.type === 'removed')).toBe(true);
    expect(diffText('A c', 'A b').status).toBe('changed');
  });

  it('compares options added, removed, and modified', () => {
    const original = version();
    const edited = version({
      questionContent: 'Một câu hỏi mới',
      answersSnapshot: JSON.stringify({
        QuestionType: 'SINGLE_CHOICE',
        DifficultyId: 'hard',
        Grade: 10,
        Topics: [{ TagId: 'geometry' }],
        Answers: [
          { AnswerId: 'a', AnswerContent: 'Đáp án A đã sửa', IsCorrect: false },
          { AnswerId: 'c', AnswerContent: 'Đáp án C', IsCorrect: true }
        ],
        Parts: [],
        SolutionContent: 'Lời giải mới'
      })
    });

    const result = compareVersions(original, edited);
    expect(result.textFields.find((field) => field.key === 'questionContent').status).toBe('changed');
    expect(result.options.items.map((item) => item.status)).toEqual(expect.arrayContaining(['changed', 'removed', 'added']));
    expect(result.metadataFields.find((field) => field.key === 'difficultyId').status).toBe('changed');
    expect(result.metadataFields.find((field) => field.key === 'topics').status).toBe('changed');
  });

  it('reports missing versions and malformed snapshots as missing data', () => {
    expect(compareVersions(null, version()).textFields[0].status).toBe('missing');
    expect(compareVersions(version({ answersSnapshot: 'not-json' }), version()).options.status).toBe('missing');
  });

  it('compares parts and multiple changed fields', () => {
    const original = version({ answersSnapshot: JSON.stringify({ Parts: [{ PartId: 'p1', PartContent: 'Phần cũ', PartType: 'TEXT' }], Answers: [] }) });
    const edited = version({ answersSnapshot: JSON.stringify({ Parts: [{ PartId: 'p1', PartContent: 'Phần mới', PartType: 'NUMERIC' }, { PartId: 'p2', PartContent: 'Phần thêm', PartType: 'TEXT' }], Answers: [] }) });
    const result = compareVersions(original, edited);
    expect(result.parts.status).toBe('changed');
    expect(result.parts.items.map((item) => item.status)).toEqual(expect.arrayContaining(['changed', 'added']));
  });
});
