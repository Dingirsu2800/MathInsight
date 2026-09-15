import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import BlueprintDetailPage from './BlueprintDetailPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ blueprintId: 'bp-mixed-123' }),
    useLocation: () => ({ pathname: '/expert/blueprints/bp-mixed-123', state: null }),
  };
});

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    getBlueprintDetail: vi.fn(),
    getBlueprintGeneratedTests: vi.fn(),
    submitBlueprintForReview: vi.fn(),
    reviewBlueprint: vi.fn(),
    cloneBlueprint: vi.fn(),
    deleteBlueprint: vi.fn(),
    archiveSharedBlueprintExam: vi.fn(),
  },
}));

vi.mock('../../services/authStorage', () => ({
  getAccountId: () => 'acc-expert-1',
}));

vi.mock('./ExpertLayout', () => ({
  default: ({ children }) => <div data-testid="expert-layout">{children}</div>,
}));

vi.mock('../../components/expert/GenerateSharedTestDialog', () => ({
  default: () => null,
}));

vi.mock('../../components/expert/FixedExamComposerDialog', () => ({
  default: () => null,
}));

beforeEach(() => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  testGeneratorApi.getBlueprintGeneratedTests.mockResolvedValue({
    data: { items: [], totalCount: 0, totalPages: 1 },
  });
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('BlueprintDetailPage Mixed and Homogeneous section display', () => {
  it('renders Mixed section badge, suppresses section scoring rule, and displays row types and rules', async () => {
    testGeneratorApi.getBlueprintDetail.mockResolvedValue({
      data: {
        blueprintId: 'bp-mixed-123',
        blueprintName: 'Cấu trúc đề hỗn hợp 2026',
        grade: 12,
        totalQuestions: 2,
        totalScore: 10,
        durationMinutes: 45,
        expertId: 'acc-expert-1',
        expertName: 'Chuyên gia Toán',
        status: 'Draft',
        sections: [
          {
            blueprintSectionId: 'sec-mixed-1',
            sectionName: 'Phần thi tích hợp',
            sectionCode: 'SEC01',
            questionType: 'Mixed',
            scoringRule: null,
            totalQuestions: 2,
            scoreBudget: 10,
            instructionText: 'Làm bài cẩn thận',
            details: [
              {
                blueprintDetailId: 'det-1',
                tagId: 'tag-1',
                tagName: 'Hàm số liên tục',
                difficultyId: 'diff-1',
                difficultyName: 'Nhận biết',
                difficultyLevel: 1,
                questionType: 'SingleChoice',
                scoringRule: 'AllOrNothing',
                quantity: 1,
              },
              {
                blueprintDetailId: 'det-2',
                tagId: 'tag-2',
                tagName: 'Hình không gian',
                difficultyId: 'diff-2',
                difficultyName: 'Thông hiểu',
                difficultyLevel: 2,
                questionType: 'Composite',
                scoringRule: 'WeightedParts',
                quantity: 1,
              },
            ],
          },
        ],
      },
    });

    render(
      <BrowserRouter>
        <BlueprintDetailPage />
      </BrowserRouter>
    );

    // Title
    expect(await screen.findByText('Cấu trúc đề hỗn hợp 2026')).toBeInTheDocument();

    // Mixed badge
    expect(screen.getByText('Hỗn hợp')).toBeInTheDocument();

    // Section scoring rule is null and section is not Composite, so "Quy tắc:" is NOT displayed at section level
    expect(screen.queryByText(/Quy tắc:/i)).not.toBeInTheDocument();

    // Mixed table headers
    expect(screen.getByText('Loại câu hỏi')).toBeInTheDocument();
    expect(screen.getByText('Quy tắc chấm')).toBeInTheDocument();

    // Row 1: SingleChoice -> "Trắc nghiệm một đáp án", "Tất cả hoặc không"
    expect(screen.getByText('Trắc nghiệm một đáp án')).toBeInTheDocument();
    expect(screen.getByText('Tất cả hoặc không')).toBeInTheDocument();

    // Row 2: Composite -> "Câu hỏi nhiều mệnh đề", "Theo trọng số phần"
    expect(screen.getByText('Câu hỏi nhiều mệnh đề')).toBeInTheDocument();
    expect(screen.getByText('Theo trọng số phần')).toBeInTheDocument();
  });

  it('renders Homogeneous Composite section with section-level scoring rule and 3-column table', async () => {
    testGeneratorApi.getBlueprintDetail.mockResolvedValue({
      data: {
        blueprintId: 'bp-comp-123',
        blueprintName: 'Cấu trúc đề nhiều mệnh đề',
        grade: 12,
        totalQuestions: 1,
        totalScore: 4,
        durationMinutes: 30,
        expertId: 'acc-expert-1',
        status: 'Draft',
        sections: [
          {
            blueprintSectionId: 'sec-comp-1',
            sectionName: 'Phần Đúng/Sai nhiều ý',
            questionType: 'Composite',
            scoringRule: 'TieredTrueFalse',
            partCountPerQuestion: 4,
            totalQuestions: 1,
            scoreBudget: 4,
            details: [
              {
                blueprintDetailId: 'det-comp-1',
                tagName: 'Tích phân',
                difficultyName: 'Vận dụng',
                difficultyLevel: 3,
                quantity: 1,
              },
            ],
          },
        ],
      },
    });

    render(
      <BrowserRouter>
        <BlueprintDetailPage />
      </BrowserRouter>
    );

    expect(await screen.findByText('Cấu trúc đề nhiều mệnh đề')).toBeInTheDocument();

    // Section rule is displayed
    expect(screen.getByText(/Quy tắc:/i)).toBeInTheDocument();
    expect(screen.getByText('Đúng / Sai phân bậc')).toBeInTheDocument();

    // 3-column table: no "Loại câu hỏi" or "Quy tắc chấm" headers
    expect(screen.queryByText('Loại câu hỏi')).not.toBeInTheDocument();
    expect(screen.queryByText('Quy tắc chấm')).not.toBeInTheDocument();
  });
});
