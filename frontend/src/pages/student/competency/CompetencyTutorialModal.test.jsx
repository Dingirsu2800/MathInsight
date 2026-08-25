import { cleanup, render, screen, fireEvent } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import CompetencyTutorialModal from './CompetencyTutorialModal';

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('CompetencyTutorialModal', () => {
  it('does not render when isOpen is false', () => {
    render(<CompetencyTutorialModal isOpen={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders step 1 by default when isOpen is true', () => {
    render(<CompetencyTutorialModal isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Nguyên lý Điểm Năng Lực (Official Point)')).toBeInTheDocument();
    expect(screen.getByText('Bước 1 / 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Tiếp theo/i })).toBeInTheDocument();
  });

  it('navigates through steps when clicking Next and Back', () => {
    render(<CompetencyTutorialModal isOpen={true} onClose={vi.fn()} />);

    // Click Next to Step 2
    fireEvent.click(screen.getByRole('button', { name: /Tiếp theo/i }));
    expect(screen.getByText('Tỷ lệ trọng số (55% Đề thi + 45% Luyện tập)')).toBeInTheDocument();
    expect(screen.getByText('Bước 2 / 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Quay lại/i })).toBeInTheDocument();

    // Click Next to Step 3
    fireEvent.click(screen.getByRole('button', { name: /Tiếp theo/i }));
    expect(screen.getByText('Ví dụ hành trình thực tế chi tiết')).toBeInTheDocument();
    expect(screen.getByText('Bước 3 / 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Đã hiểu/i })).toBeInTheDocument();

    // Click Back to Step 2
    fireEvent.click(screen.getByRole('button', { name: /Quay lại/i }));
    expect(screen.getByText('Tỷ lệ trọng số (55% Đề thi + 45% Luyện tập)')).toBeInTheDocument();
  });

  it('calls onClose when clicking "Đã hiểu" on the last step', () => {
    const handleClose = vi.fn();
    render(<CompetencyTutorialModal isOpen={true} onClose={handleClose} />);

    // Go to step 3
    fireEvent.click(screen.getByRole('button', { name: /Tiếp theo/i }));
    fireEvent.click(screen.getByRole('button', { name: /Tiếp theo/i }));

    // Click "Đã hiểu"
    fireEvent.click(screen.getByRole('button', { name: /Đã hiểu/i }));
    expect(handleClose).toHaveBeenCalledTimes(1);
  });
});
