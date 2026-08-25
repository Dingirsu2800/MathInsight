import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import QuestionOcrDraftReviewDialog, { getDetectedMarkLabel } from './QuestionOcrDraftReviewDialog';

vi.mock('./LatexPreview', () => ({
  default: ({ content }) => <div data-testid="latex-preview">{content}</div>,
}));

vi.mock('../ui/custom-select', () => ({
  CustomSelect: ({ value, onValueChange, items }) => (
    <select data-testid="custom-select" value={value} onChange={(e) => onValueChange(e.target.value)}>
      {items?.map((it) => (
        <option key={it.value} value={it.value}>{it.label}</option>
      ))}
    </select>
  ),
}));

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('QuestionOcrDraftReviewDialog - OCR Mark Untrusted Observations & Simplified Review', () => {
  const baseOcrResult = {
    pageConfidence: 0.95,
    extractedImages: [],
    warnings: [],
  };

  it('correctly maps detectedMark constants to neutral Vietnamese labels', () => {
    expect(getDetectedMarkLabel('Circled')).toBe('Phát hiện có khoanh');
    expect(getDetectedMarkLabel('Ticked')).toBe('Phát hiện dấu tích');
    expect(getDetectedMarkLabel('Crossed')).toBe('Phát hiện gạch chéo');
    expect(getDetectedMarkLabel('Highlighted')).toBe('Phát hiện được tô nổi bật');
    expect(getDetectedMarkLabel('Unknown')).toBe('Có ký hiệu cần kiểm tra');
    expect(getDetectedMarkLabel('None')).toBeNull();
    expect(getDetectedMarkLabel('')).toBeNull();
    expect(getDetectedMarkLabel(null)).toBeNull();
  });

  it('renders "Phát hiện có khoanh" badge and helper notice, and NEVER renders "Gợi ý đúng"', () => {
    const reviewDraft = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Cho hình chóp $S.ABC$...',
      solutionContent: '',
      answers: [
        { content: 'A. 1/3', detectedMark: 'Circled', suggestedIsCorrect: true },
        { content: 'B. 2/3', detectedMark: 'None', suggestedIsCorrect: false },
        { content: 'C. 1', detectedMark: 'Ticked', suggestedIsCorrect: null },
        { content: 'D. 2', detectedMark: 'Crossed', suggestedIsCorrect: null },
      ],
    };

    render(
      <QuestionOcrDraftReviewDialog
        isOpen={true}
        onClose={vi.fn()}
        ocrResult={baseOcrResult}
        reviewDraft={reviewDraft}
        setReviewDraft={vi.fn()}
        attachSourceImage={false}
        setAttachSourceImage={vi.fn()}
        selectedExtractedImageId={null}
        setSelectedExtractedImageId={vi.fn()}
        manualCropSelection={null}
        setManualCropSelection={vi.fn()}
        ocrImageUploading={false}
        ocrImageUploadError=""
        onApplyDraft={vi.fn()}
        ocrPreviewUrl=""
        isOcrBusy={false}
      />
    );

    // Assert observation badges
    expect(screen.getByText('Phát hiện có khoanh')).toBeInTheDocument();
    expect(screen.getByText('Phát hiện dấu tích')).toBeInTheDocument();
    expect(screen.getByText('Phát hiện gạch chéo')).toBeInTheDocument();

    // Assert that "Gợi ý đúng" is NEVER rendered
    expect(screen.queryByText('Gợi ý đúng')).not.toBeInTheDocument();

    // Assert helper observation disclaimer copy
    expect(
      screen.getByText('Ký hiệu chỉ là dữ liệu quan sát từ ảnh, không phải đáp án đã xác nhận.')
    ).toBeInTheDocument();

    // Raw Mistral Markdown diagnostics section must NOT be rendered
    expect(screen.queryByText(/Mã Markdown gốc từ Mistral OCR/i)).not.toBeInTheDocument();
  });

  it('hides empty solution by default and reveals solution editor on "Thêm lời giải" click', () => {
    const reviewDraft = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Đề bài',
      solutionContent: '',
      answers: [],
    };

    const setReviewDraft = vi.fn();

    render(
      <QuestionOcrDraftReviewDialog
        isOpen={true}
        onClose={vi.fn()}
        ocrResult={baseOcrResult}
        reviewDraft={reviewDraft}
        setReviewDraft={setReviewDraft}
        attachSourceImage={false}
        setAttachSourceImage={vi.fn()}
        selectedExtractedImageId={null}
        setSelectedExtractedImageId={vi.fn()}
        manualCropSelection={null}
        setManualCropSelection={vi.fn()}
        ocrImageUploading={false}
        ocrImageUploadError=""
        onApplyDraft={vi.fn()}
        ocrPreviewUrl=""
        isOcrBusy={false}
      />
    );

    // Empty solution textarea should NOT be visible by default
    expect(screen.queryByPlaceholderText(/Nhập hướng dẫn giải/i)).not.toBeInTheDocument();

    // "Thêm lời giải" button should be visible
    const addSolutionBtn = screen.getByRole('button', { name: /Thêm lời giải/i });
    expect(addSolutionBtn).toBeInTheDocument();

    // Click "Thêm lời giải"
    fireEvent.click(addSolutionBtn);

    // Solution textarea should now be visible
    expect(screen.getByPlaceholderText(/Nhập hướng dẫn giải/i)).toBeInTheDocument();
  });

  it('displays solution editor immediately when OCR returned explicit solution', () => {
    const reviewDraft = {
      suggestedQuestionType: 'SINGLE_CHOICE',
      questionContent: 'Đề bài',
      solutionContent: 'Lời giải chi tiết từ OCR',
      answers: [],
    };

    render(
      <QuestionOcrDraftReviewDialog
        isOpen={true}
        onClose={vi.fn()}
        ocrResult={baseOcrResult}
        reviewDraft={reviewDraft}
        setReviewDraft={vi.fn()}
        attachSourceImage={false}
        setAttachSourceImage={vi.fn()}
        selectedExtractedImageId={null}
        setSelectedExtractedImageId={vi.fn()}
        manualCropSelection={null}
        setManualCropSelection={vi.fn()}
        ocrImageUploading={false}
        ocrImageUploadError=""
        onApplyDraft={vi.fn()}
        ocrPreviewUrl=""
        isOcrBusy={false}
      />
    );

    expect(screen.getByPlaceholderText(/Nhập hướng dẫn giải/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue('Lời giải chi tiết từ OCR')).toBeInTheDocument();
  });
});
