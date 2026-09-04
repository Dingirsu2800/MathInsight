import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import HistoryFilters from './HistoryFilters';

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('HistoryFilters', () => {
  it('renders filter controls and submits with selected values', () => {
    const handleFilter = vi.fn();
    render(<HistoryFilters onFilter={handleFilter} />);

    const fromInput = screen.getByLabelText('Từ ngày');
    const toInput = screen.getByLabelText('Đến ngày');
    const formatSelect = screen.getByRole('combobox');
    const applyBtn = screen.getByRole('button', { name: /áp dụng lọc/i });

    fireEvent.change(formatSelect, { target: { value: 'Exam' } });
    fireEvent.change(fromInput, { target: { value: '2026-09-01' } });
    fireEvent.change(toInput, { target: { value: '2026-09-03' } });
    fireEvent.click(applyBtn);

    expect(handleFilter).toHaveBeenCalledWith({
      testFormat: 'Exam',
      fromDate: '2026-09-01',
      toDate: '2026-09-03',
    });
  });

  it('validates and shows error when fromDate > toDate', () => {
    const handleFilter = vi.fn();
    render(<HistoryFilters onFilter={handleFilter} />);

    const fromInput = screen.getByLabelText('Từ ngày');
    const toInput = screen.getByLabelText('Đến ngày');
    const applyBtn = screen.getByRole('button', { name: /áp dụng lọc/i });

    fireEvent.change(fromInput, { target: { value: '2026-09-05' } });
    fireEvent.change(toInput, { target: { value: '2026-09-03' } });
    fireEvent.click(applyBtn);

    expect(
      screen.getByText('Ngày bắt đầu không được lớn hơn ngày kết thúc.')
    ).toBeVisible();
    expect(handleFilter).not.toHaveBeenCalled();
  });

  it('resets filters when clicking "Đặt lại"', () => {
    const handleFilter = vi.fn();
    render(<HistoryFilters onFilter={handleFilter} />);

    const fromInput = screen.getByLabelText('Từ ngày');
    fireEvent.change(fromInput, { target: { value: '2026-09-01' } });

    const resetBtn = screen.getByRole('button', { name: /đặt lại/i });
    fireEvent.click(resetBtn);

    expect(handleFilter).toHaveBeenCalledWith({
      testFormat: '',
      fromDate: '',
      toDate: '',
    });
    expect(fromInput.value).toBe('');
  });
});
