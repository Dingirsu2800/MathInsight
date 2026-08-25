import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import BlueprintEditorPage from './BlueprintEditorPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';
import { questionBankApi } from '../../services/questionBankApi';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({}),
    useLocation: () => ({ pathname: '/expert/blueprints/new', state: null }),
  };
});

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    createBlueprint: vi.fn(),
    updateBlueprint: vi.fn(),
    getBlueprintDetail: vi.fn(),
  },
}));

vi.mock('../../services/questionBankApi', () => ({
  questionBankApi: {
    getDifficulties: vi.fn(),
    getTopicTags: vi.fn(),
  },
}));

vi.mock('./ExpertLayout', () => ({
  default: ({ children }) => <div data-testid="expert-layout">{children}</div>,
}));

vi.mock('../../components/layout/DashboardLayout', () => ({
  default: ({ children }) => <div data-testid="dashboard-layout">{children}</div>,
}));

beforeEach(() => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  questionBankApi.getDifficulties.mockResolvedValue({
    data: [
      { difficultyId: 'diff-1', difficultyName: 'Nhận biết' },
      { difficultyId: 'diff-2', difficultyName: 'Thông hiểu' },
    ],
  });
  questionBankApi.getTopicTags.mockResolvedValue({
    data: [
      { tagId: 'topic-root-1', name: 'Đại số 12', depth: 0 },
      { tagId: 'topic-1', name: 'Hàm số và đồ thị', depth: 1 },
    ],
  });
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});

describe('BlueprintEditorPage Vietnamese terminology and validation', () => {
  it('renders the approved Vietnamese labels and composite helper copy', async () => {
    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    expect(await screen.findByText(/Số câu của đề/i)).toBeInTheDocument();
    expect(screen.getByText(/Tổng điểm của phần/i)).toBeInTheDocument();

    // Open question type select to see "Câu hỏi gồm nhiều mệnh đề"
    const typeSelects = screen.getAllByRole('combobox');
    fireEvent.click(typeSelects[1]); // second select is questionType

    expect(await screen.findByText(/Câu hỏi gồm nhiều mệnh đề/i)).toBeInTheDocument();
  });

  it('validates matching question counts and total scores between header and sections', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValue({
      data: { blueprintId: 'bp-new-1', blueprintName: 'Đề mẫu' },
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Fill header
    const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
    fireEvent.change(nameInput, { target: { value: 'Cấu trúc đề ôn thi chuẩn 2026' } });

    const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
    fireEvent.change(totalQuestionsInput, { target: { value: '20' } });

    // Section 1 totalQuestions is empty by default (0 sum), sum (0) != 20
    const saveBtn = screen.getByRole('button', { name: /Lưu bản nháp/i });
    fireEvent.click(saveBtn);

    // Expect validation failure message in feedback banner
    await waitFor(() => {
      const banner = screen.getByText(/Dữ liệu không hợp lệ/i);
      expect(banner).toHaveTextContent('Tổng số câu của các phần (0) phải bằng tổng số câu của cấu trúc đề (20)');
    });

    expect(testGeneratorApi.createBlueprint).not.toHaveBeenCalled();
  });
});
