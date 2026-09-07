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
  it('renders short answer input with symbol buttons accepting valid text and math symbols', () => {
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
    expect(screen.getByRole('button', { name: 'Chèn ký hiệu pi (π)' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Chèn căn bậc hai (√)' })).toBeInTheDocument();

    // Valid typing steps including text and math symbols
    fireEvent.change(input, { target: { value: '2π' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '2π' });

    fireEvent.change(input, { target: { value: '√(2)' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '√(2)' });

    fireEvent.change(input, { target: { value: 'Hà Nội' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: 'Hà Nội' });

    fireEvent.change(input, { target: { value: '1,5' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', { shortAnswerText: '1,5' });
  });

  it('inserts π symbol into short answer when π button is clicked', () => {
    const onAnswer = vi.fn();
    render(
      <QuestionPanel
        question={{ ...baseQuestion, questionType: 'SHORT_ANSWER' }}
        answer={{ shortAnswerText: '2' }}
        onAnswer={onAnswer}
        totalQuestions={1}
      />
    );

    const piBtn = screen.getByRole('button', { name: 'Chèn ký hiệu pi (π)' });
    fireEvent.click(piBtn);

    expect(onAnswer).toHaveBeenCalledWith('question-1', { shortAnswerText: '2π' });
  });

  it('allows text and symbols in composite text parts', () => {
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

    fireEvent.change(input, { target: { value: '-2,5' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', {
      parts: [{ partId: 'part-text', textAnswer: '-2,5' }],
    });

    fireEvent.change(input, { target: { value: 'vô nghiệm' } });
    expect(onAnswer).toHaveBeenLastCalledWith('question-1', {
      parts: [{ partId: 'part-text', textAnswer: 'vô nghiệm' }],
    });
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
    fireEvent.change(input, { target: { value: '-1,5' } });
    expect(onAnswer).toHaveBeenCalledWith('question-1', {
      parts: [{ partId: 'part-number', numericAnswer: '-1,5' }],
    });
  });
});
