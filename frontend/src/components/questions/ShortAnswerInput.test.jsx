import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import React, { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ShortAnswerInput from './ShortAnswerInput';

afterEach(() => {
  cleanup();
});

function StatefulWrapper(props) {
  const [val, setVal] = useState(props.initialValue ?? '');
  return <ShortAnswerInput value={val} onChange={setVal} {...props} />;
}

describe('ShortAnswerInput component', () => {
  it('renders input, buttons, and helper text', () => {
    render(<ShortAnswerInput value="" onChange={vi.fn()} />);

    expect(screen.getByPlaceholderText('Nhập đáp án ngắn...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Chèn ký hiệu pi (π)' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Chèn căn bậc hai (√)' })).toBeInTheDocument();
    expect(screen.getByText(/Ví dụ: 2π hoặc √\(2\)/)).toBeInTheDocument();
    expect(screen.getByText('0/100')).toBeInTheDocument();
  });

  it('inserts π symbol when pi button is clicked', () => {
    const onChange = vi.fn();
    render(<ShortAnswerInput value="2" onChange={onChange} />);

    const piBtn = screen.getByRole('button', { name: 'Chèn ký hiệu pi (π)' });
    fireEvent.click(piBtn);

    expect(onChange).toHaveBeenCalledWith('2π');
  });

  it('inserts √() when sqrt button is clicked without selection', () => {
    const onChange = vi.fn();
    render(<ShortAnswerInput value="" onChange={onChange} />);

    const sqrtBtn = screen.getByRole('button', { name: 'Chèn căn bậc hai (√)' });
    fireEvent.click(sqrtBtn);

    expect(onChange).toHaveBeenCalledWith('√()');
  });

  it('shows math preview when math symbols are present', () => {
    render(<ShortAnswerInput value="2π + √(2)" onChange={vi.fn()} />);

    expect(screen.getByText(/Xem trước công thức:/i)).toBeInTheDocument();
    expect(screen.getByTestId('short-answer-math-preview')).toBeInTheDocument();
  });

  it('does not show math preview for plain text', () => {
    render(<ShortAnswerInput value="Hà Nội" onChange={vi.fn()} />);

    expect(screen.queryByText(/Xem trước công thức:/i)).not.toBeInTheDocument();
  });

  it('displays error message when error prop is provided', () => {
    render(<ShortAnswerInput value="abc" onChange={vi.fn()} error="Giá trị không hợp lệ" />);

    expect(screen.getByText('Giá trị không hợp lệ')).toBeInTheDocument();
  });
});
