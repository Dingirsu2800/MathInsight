import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionPanel from './QuestionPanel';

afterEach(() => cleanup());

const baseQuestion = {
  questionId: 'question-1',
  questionNo: 1,
  questionContent: 'Nhập kết quả.',
};

describe('QuestionPanel short answer controls', () => {
  it('renders question short answer as a bounded single-line input', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{ ...baseQuestion, questionType: 'SHORT_ANSWER' }}
        answer={{}}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    expect(screen.getByText('Trả lời ngắn')).toBeInTheDocument();
    const input = screen.getByPlaceholderText('Nhập đáp án ngắn...');
    expect(input.tagName).toBe('INPUT');
    expect(input).toHaveAttribute('maxlength', '100');
    fireEvent.change(input, { target: { value: 'π' } });
    expect(onAnswer).toHaveBeenCalledWith('question-1', { shortAnswerText: 'π' });
  });

  it('limits composite text parts to 255 characters', () => {
    render(
      <QuestionPanel
        question={{
          ...baseQuestion,
          questionType: 'COMPOSITE',
          parts: [{ partId: 'part-text', content: 'Tìm tập nghiệm.', answerType: 'TEXT' }],
        }}
        answer={{ parts: [] }}
        onAnswer={vi.fn()}
        totalQuestions={1}
      />
    );

    expect(screen.getByPlaceholderText('Nhập đáp án ngắn...'))
      .toHaveAttribute('maxlength', '255');
  });

  it('keeps a decimal comma as raw state in a numeric part', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{
          ...baseQuestion,
          questionType: 'COMPOSITE',
          parts: [{ partId: 'part-number', content: 'Tính giá trị.', answerType: 'NUMERIC' }],
        }}
        answer={{ parts: [] }}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    const input = screen.getByPlaceholderText('Nhập kết quả...');
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveAttribute('inputmode', 'decimal');
    fireEvent.change(input, { target: { value: '-1,5' } });
    expect(onAnswer).toHaveBeenCalledWith('question-1', {
      parts: [{ partId: 'part-number', numericAnswer: '-1,5' }],
    });
  });
});
