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
  it('renders numeric-only single-line short answer input accepting valid editing sequences', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{ ...baseQuestion, questionType: 'SHORT_ANSWER' }}
        answer={{ shortAnswerText: '' }}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    expect(screen.getByText('Trả lời ngắn')).toBeInTheDocument();
    const input = screen.getByPlaceholderText('Nhập đáp án ngắn...');
    expect(input.tagName).toBe('INPUT');
    expect(input).toHaveAttribute('inputmode', 'decimal');
    expect(input).toHaveAttribute('maxlength', '100');

    // Valid typing steps
    fireEvent.change(input, { target: { value: '-' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '-' });

    fireEvent.change(input, { target: { value: '-3' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '-3' });

    fireEvent.change(input, { target: { value: '1.5' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '1.5' });

    fireEvent.change(input, { target: { value: '1,5' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '1,5' });
  });

  it('rejects non-numeric characters, multiple separators, exponent letters, fractions, and symbols in short answer', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{ ...baseQuestion, questionType: 'SHORT_ANSWER' }}
        answer={{ shortAnswerText: '1' }}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    const input = screen.getByPlaceholderText('Nhập đáp án ngắn...');

    // Non-numeric inputs should be rejected and NOT trigger onAnswer
    fireEvent.change(input, { target: { value: 'π' } });
    expect(onAnswer).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'abc' } });
    expect(onAnswer).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '1/2' } });
    expect(onAnswer).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '1e3' } });
    expect(onAnswer).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '1.5.2' } });
    expect(onAnswer).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: '1,5,2' } });
    expect(onAnswer).not.toHaveBeenCalled();
  });

  it('restricts composite text parts to numeric short answers', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{
          ...baseQuestion,
          questionType: 'COMPOSITE',
          parts: [{ partId: 'part-text', content: 'Tìm nghiệm.', answerType: 'TEXT' }],
        }}
        answer={{ parts: [] }}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    const input = screen.getByPlaceholderText('Nhập đáp án ngắn...');
    expect(input).toHaveAttribute('inputmode', 'decimal');

    fireEvent.change(input, { target: { value: '-2,5' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', {
      parts: [{ partId: 'part-text', textAnswer: '-2,5' }],
    });

    fireEvent.change(input, { target: { value: 'vô nghiệm' } });
    expect(onAnswer).toHaveBeenCalledTimes(1);
  });

  it('keeps decimal comma as raw state in numeric parts', () => {
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
    expect(input).toHaveAttribute('inputmode', 'decimal');
    fireEvent.change(input, { target: { value: '-1,5' } });
    expect(onAnswer).toHaveBeenCalledWith('question-1', {
      parts: [{ partId: 'part-number', numericAnswer: '-1,5' }],
    });
  });
});
