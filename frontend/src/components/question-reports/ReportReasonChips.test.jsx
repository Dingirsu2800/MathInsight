import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import React, { createRef } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ReportReasonChips from './ReportReasonChips';

afterEach(() => cleanup());

describe('ReportReasonChips component', () => {
  it('renders all 6 approved Vietnamese quick-reason chips', () => {
    render(<ReportReasonChips value="" onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Nội dung sai hoặc thiếu' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Đáp án chưa chính xác' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lời giải chưa phù hợp' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Công thức hoặc hình ảnh bị lỗi' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sai chủ đề hoặc độ khó' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Khác' })).toBeInTheDocument();
  });

  it('populates empty reason value when a chip is clicked without submitting', () => {
    const onChange = vi.fn();
    render(<ReportReasonChips value="" onChange={onChange} />);

    const chip = screen.getByRole('button', { name: 'Đáp án chưa chính xác' });
    expect(chip).toHaveAttribute('type', 'button');
    fireEvent.click(chip);

    expect(onChange).toHaveBeenCalledWith('Đáp án chưa chính xác');
  });

  it('appends with semicolon without duplicating already-selected reason', () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <ReportReasonChips value="Nội dung sai hoặc thiếu" onChange={onChange} />
    );

    // Clicking already existing reason does not duplicate
    const chip1 = screen.getByRole('button', { name: 'Nội dung sai hoặc thiếu' });
    fireEvent.click(chip1);
    expect(onChange).not.toHaveBeenCalled();

    // Clicking another reason appends with semicolon
    const chip2 = screen.getByRole('button', { name: 'Công thức hoặc hình ảnh bị lỗi' });
    fireEvent.click(chip2);
    expect(onChange).toHaveBeenCalledWith('Nội dung sai hoặc thiếu; Công thức hoặc hình ảnh bị lỗi');
  });

  it('clicking "Khác" focuses the textarea without appending text', () => {
    const onChange = vi.fn();
    const onFocusTextarea = vi.fn();
    render(
      <div>
        <ReportReasonChips
          value="Ghi chú ban đầu"
          onChange={onChange}
          onFocusTextarea={onFocusTextarea}
        />
        <textarea data-testid="custom-textarea" />
      </div>
    );

    const otherChip = screen.getByRole('button', { name: 'Khác' });
    expect(otherChip).toHaveAttribute('type', 'button');
    fireEvent.click(otherChip);

    expect(onChange).not.toHaveBeenCalled();
    expect(onFocusTextarea).toHaveBeenCalledTimes(1);
  });
});
