import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ReportReasonChips, { formatReportReason } from './ReportReasonChips';

afterEach(() => cleanup());

describe('ReportReasonChips component', () => {
  it('renders all 6 approved Vietnamese quick-reason chips', () => {
    render(<ReportReasonChips selectedReasons={[]} onChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Nội dung sai hoặc thiếu' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Đáp án chưa chính xác' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lời giải chưa phù hợp' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Công thức hoặc hình ảnh bị lỗi' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sai chủ đề hoặc độ khó' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Khác / bổ sung chi tiết' })).toBeInTheDocument();
  });

  it('selects a reason without submitting', () => {
    const onChange = vi.fn();
    render(<ReportReasonChips selectedReasons={[]} onChange={onChange} />);

    const chip = screen.getByRole('button', { name: 'Đáp án chưa chính xác' });
    expect(chip).toHaveAttribute('type', 'button');
    fireEvent.click(chip);

    expect(onChange).toHaveBeenCalledWith(['Đáp án chưa chính xác']);
  });

  it('toggles choices and formats all selected reasons on separate lines', () => {
    const onChange = vi.fn();
    render(
      <ReportReasonChips selectedReasons={['Nội dung sai hoặc thiếu']} onChange={onChange} />
    );

    const chip1 = screen.getByRole('button', { name: 'Nội dung sai hoặc thiếu' });
    expect(chip1).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(chip1);
    expect(onChange).toHaveBeenCalledWith([]);

    const chip2 = screen.getByRole('button', { name: 'Công thức hoặc hình ảnh bị lỗi' });
    fireEvent.click(chip2);
    expect(onChange).toHaveBeenCalledWith(['Nội dung sai hoặc thiếu', 'Công thức hoặc hình ảnh bị lỗi']);
    expect(formatReportReason(['Nội dung sai hoặc thiếu', 'Công thức hoặc hình ảnh bị lỗi'], ''))
      .toBe('• Nội dung sai hoặc thiếu\n• Công thức hoặc hình ảnh bị lỗi');
  });

  it('shows custom input without clearing selected reasons', () => {
    const onChange = vi.fn();
    render(
      <ReportReasonChips selectedReasons={['Đáp án chưa chính xác']} onChange={onChange} />
    );

    expect(screen.queryByLabelText('Mô tả chi tiết')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Khác / bổ sung chi tiết' }));
    expect(onChange).toHaveBeenCalledWith(['Đáp án chưa chính xác', 'Khác']);
    expect(formatReportReason(['Đáp án chưa chính xác', 'Khác'], 'Đáp án B sai'))
      .toBe('• Đáp án chưa chính xác\nChi tiết: Đáp án B sai');
  });
});
