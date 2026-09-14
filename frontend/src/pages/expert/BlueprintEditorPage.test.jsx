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
      const banner = screen.getByRole('alert');
      expect(banner).toHaveTextContent('Dữ liệu không hợp lệ');
      expect(banner).toHaveTextContent('Tổng số câu của các phần (0) phải bằng tổng số câu của cấu trúc đề (20)');
    });

    expect(testGeneratorApi.createBlueprint).not.toHaveBeenCalled();
  });

  it('renders "Hỗn hợp" in question type options and shows row-level type/rule selectors when selected', async () => {
    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Section 1 question type select
    const typeSelects = screen.getAllByRole('combobox');
    fireEvent.click(typeSelects[1]); // section questionType

    const mixedOption = await screen.findByText('Hỗn hợp');
    expect(mixedOption).toBeInTheDocument();
    fireEvent.click(mixedOption);

    // Section scoring rule is hidden (not rendered)
    expect(screen.queryByText(/Đúng\/Sai phân bậc \(giảm một nửa điểm còn lại\)/i)).not.toBeInTheDocument();

    // Table now has columns "Loại câu hỏi" and "Quy tắc chấm"
    expect(screen.getByRole('columnheader', { name: /Loại câu hỏi/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /Quy tắc chấm/i })).toBeInTheDocument();

    // The detail row has default "Tất cả hoặc không" displayed
    expect(screen.getByText('Tất cả hoặc không')).toBeInTheDocument();
  });

  it('prompts confirmation when switching Mixed with incompatible rows to homogeneous and cancels/confirms properly', async () => {
    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Switch section to Mixed
    const typeSelects = screen.getAllByRole('combobox');
    fireEvent.click(typeSelects[1]);
    fireEvent.click(await screen.findByText('Hỗn hợp'));

    // Add a second detail row
    const addRowBtn = screen.getByRole('button', { name: /Thêm dòng phân bổ/i });
    fireEvent.click(addRowBtn);

    // Change second row's questionType to ShortAnswer
    const rowTypeSelects = screen.getAllByRole('combobox').filter(cb => cb.textContent.includes('Trắc nghiệm một lựa chọn'));
    // Last row type select is for row 2
    fireEvent.click(rowTypeSelects[rowTypeSelects.length - 1]);
    fireEvent.click(await screen.findByText('Tự luận ngắn'));

    // Now try switching section back to SingleChoice
    const secTypeSelect = screen.getAllByRole('combobox')[1];
    fireEvent.click(secTypeSelect);
    const options = await screen.findAllByRole('option');
    const singleChoiceOption = options.find(opt => opt.textContent.includes('Trắc nghiệm một lựa chọn'));
    fireEvent.click(singleChoiceOption);

    // Confirmation dialog appears!
    expect(await screen.findByText('Xác nhận đổi loại phần thi')).toBeInTheDocument();
    expect(screen.getByText(/cấu hình phân bổ riêng từng dòng trước đó sẽ bị thay thế/i)).toBeInTheDocument();

    // Cancel: dialog closes and section stays Mixed
    const cancelBtn = screen.getByRole('button', { name: /Giữ lại Hỗn hợp/i });
    fireEvent.click(cancelBtn);

    await waitFor(() => {
      expect(screen.queryByText('Xác nhận đổi loại phần thi')).not.toBeInTheDocument();
    });
    expect(screen.getByRole('columnheader', { name: /Loại câu hỏi/i })).toBeInTheDocument();

    // Switch again and confirm
    fireEvent.click(secTypeSelect);
    const options2 = await screen.findAllByRole('option');
    const singleChoiceOption2 = options2.find(opt => opt.textContent.includes('Trắc nghiệm một lựa chọn'));
    fireEvent.click(singleChoiceOption2);

    const confirmBtn = await screen.findByRole('button', { name: /Xác nhận chuyển đổi/i });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(screen.queryByText('Xác nhận đổi loại phần thi')).not.toBeInTheDocument();
    });

    // Row-level type/rule headers are gone (homogeneous mode)
    expect(screen.queryByRole('columnheader', { name: /Loại câu hỏi/i })).not.toBeInTheDocument();
  });

  it('submits a valid Mixed blueprint with section scoringRule null and row-level policies', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValue({
      data: { blueprintId: 'bp-mixed-created', blueprintName: 'Đề thi Hỗn hợp' },
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Header info
    const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
    fireEvent.change(nameInput, { target: { value: 'Đề thi Hỗn hợp' } });

    const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
    fireEvent.change(totalQuestionsInput, { target: { value: '1' } });

    // Section 1: set name and switch to Mixed
    const secNameInput = screen.getByPlaceholderText(/VD: Trắc nghiệm khách quan nhiều lựa chọn/i);
    fireEvent.change(secNameInput, { target: { value: 'Phần 1 Hỗn hợp' } });

    const typeSelects = screen.getAllByRole('combobox');
    fireEvent.click(typeSelects[1]);
    fireEvent.click(await screen.findByText('Hỗn hợp'));


    const secQuestionsInput = screen.getByPlaceholderText(/VD: 10/i);
    fireEvent.change(secQuestionsInput, { target: { value: '1' } });

    // Select topic in topic picker
    const topicSelect = screen.getAllByRole('combobox')[2];
    fireEvent.click(topicSelect);
    const topicOption = await screen.findByText(/Hàm số và đồ thị/i);
    fireEvent.click(topicOption);

    // Select difficulty
    const diffSelect = screen.getAllByRole('combobox')[3];
    fireEvent.click(diffSelect);
    const diffOption = await screen.findByText(/Nhận biết/i);
    fireEvent.click(diffOption);




    // Save
    const saveBtn = screen.getByRole('button', { name: /Lưu bản nháp/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
    });

    const payload = testGeneratorApi.createBlueprint.mock.calls[0][0];
    expect(payload.sections[0].questionType).toBe('Mixed');
    expect(payload.sections[0].scoringRule).toBeNull();
    expect(payload.sections[0].partCountPerQuestion).toBeNull();
    expect(payload.sections[0].details[0]).toMatchObject({
      tagId: 'topic-1',
      difficultyId: 'diff-1',
      quantity: 1,
      questionType: 'SingleChoice',
      scoringRule: 'AllOrNothing',
    });
  });
});

