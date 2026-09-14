import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import BlueprintEditorPage from './BlueprintEditorPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';
import { questionBankApi } from '../../services/questionBankApi';

let mockParams = {};
let mockLocation = { pathname: '/expert/blueprints/new', state: null };
const mockNavigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => mockParams,
    useLocation: () => mockLocation,
  };
});

vi.mock('../../services/testGeneratorApi', () => ({
  testGeneratorApi: {
    createBlueprint: vi.fn(),
    updateBlueprint: vi.fn(),
    getBlueprintDetail: vi.fn(),
    checkBlueprintAvailability: vi.fn(),
    submitBlueprintForReview: vi.fn(),
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

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

beforeEach(() => {
  window.HTMLElement.prototype.scrollIntoView = vi.fn();
  window.scrollTo = vi.fn();
  localStorage.setItem('account_id', 'acc-test-expert');
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
      { tagId: 'topic-2', name: 'Hình học không gian', depth: 1 },
    ],
  });
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
  localStorage.clear();
  mockParams = {};
  mockLocation = { pathname: '/expert/blueprints/new', state: null };
});

describe('BlueprintEditorPage Live Availability (Task 2)', () => {
  it('displays "—" when allocation row is incomplete and indicates missing criteria in summary', async () => {
    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Initial empty row has no topic or difficulty
    expect(await screen.findByText('—')).toBeInTheDocument();
    expect(screen.getByText(/Chưa đủ thông tin:/i)).toBeInTheDocument();
  });

  it('displays available count and "Thiếu N" when capacity is insufficient', async () => {
    testGeneratorApi.checkBlueprintAvailability.mockImplementation(async (payload) => {
      const rowId = payload.sections[0].rows[0].clientRowId;
      return {
        data: {
          checkedAt: '2026-09-14T01:23:45.0000000+00:00',
          sections: [
            {
              clientSectionId: payload.sections[0].clientSectionId,
              rows: [
                { clientRowId: rowId, availableCount: 2, requiredCount: 5, shortage: 3 },
              ],
            },
          ],
          wholeBlueprintFeasible: false,
          availabilityCode: 'BLUEPRINT_AVAILABILITY_INSUFFICIENT_QUESTIONS',
        },
      };
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Comboboxes: [0] = Grade, [1] = Section type, [2] = Topic, [3] = Difficulty
    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]); // Topic picker
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]); // Difficulty picker
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    // Wait for 400ms debounce
    await sleep(450);

    await waitFor(() => {
      expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalled();
    });

    // Available count and shortage badge
    expect(await screen.findByText('2 câu')).toBeInTheDocument();
    expect(screen.getByText('Thiếu 3')).toBeInTheDocument();

    // Summary panel indicates shortage
    expect(screen.getByText(/Ngân hàng chưa đủ câu hỏi/i)).toBeInTheDocument();
  });

  it('displays true zero count from BE without treating it as a network error', async () => {
    testGeneratorApi.checkBlueprintAvailability.mockImplementation(async (payload) => {
      const rowId = payload.sections[0].rows[0].clientRowId;
      return {
        data: {
          checkedAt: '2026-09-14T01:23:45.0000000+00:00',
          sections: [
            {
              clientSectionId: payload.sections[0].clientSectionId,
              rows: [
                { clientRowId: rowId, availableCount: 0, requiredCount: 2, shortage: 2 },
              ],
            },
          ],
          wholeBlueprintFeasible: false,
          availabilityCode: 'BLUEPRINT_AVAILABILITY_INSUFFICIENT_QUESTIONS',
        },
      };
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    await sleep(450);

    await waitFor(() => {
      expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalled();
    });

    const table = screen.getByRole('table');
    // True zero count is displayed properly in table
    expect(await within(table).findByText('0 câu')).toBeInTheDocument();
    expect(within(table).getByText('Thiếu 2')).toBeInTheDocument();
    expect(within(table).queryByText('Lỗi kiểm tra')).not.toBeInTheDocument();
  });

  it('displays "Lỗi kiểm tra" and "Thử lại" on network error, never displays 0, and retries on click', async () => {
    testGeneratorApi.checkBlueprintAvailability.mockRejectedValueOnce(new Error('Network connection timeout'));

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    await sleep(450);

    await waitFor(() => {
      expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalledTimes(1);
    });

    const table = screen.getByRole('table');
    // Verify error state
    expect(await within(table).findByText('Lỗi kiểm tra')).toBeInTheDocument();
    expect(within(table).getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
    // Must NOT display 0 as if it had zero count
    expect(within(table).queryByText('0 câu')).not.toBeInTheDocument();

    // Now test retry
    testGeneratorApi.checkBlueprintAvailability.mockImplementationOnce(async (payload) => {
      const rowId = payload.sections[0].rows[0].clientRowId;
      return {
        data: {
          sections: [
            {
              clientSectionId: payload.sections[0].clientSectionId,
              rows: [{ clientRowId: rowId, availableCount: 7, requiredCount: 1, shortage: 0 }],
            },
          ],
          wholeBlueprintFeasible: true,
        },
      };
    });

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));

    await waitFor(() => {
      expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalledTimes(2);
    });
    expect(await screen.findByText('7 câu')).toBeInTheDocument();
  });

  it('discards stale response A when response B resolves first or after (A/B race condition)', async () => {
    let resolveA;
    const promiseA = new Promise((resolve) => {
      resolveA = resolve;
    });

    // Request A is delayed
    testGeneratorApi.checkBlueprintAvailability.mockImplementationOnce(async () => promiseA);

    // Request B resolves immediately with count 12
    testGeneratorApi.checkBlueprintAvailability.mockImplementationOnce(async (payload) => {
      const rowId = payload.sections[0].rows[0].clientRowId;
      return {
        data: {
          sections: [
            {
              clientSectionId: payload.sections[0].clientSectionId,
              rows: [{ clientRowId: rowId, availableCount: 12, requiredCount: 1, shortage: 0 }],
            },
          ],
          wholeBlueprintFeasible: true,
        },
      };
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    // Trigger Request A
    await sleep(450);
    expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalledTimes(1);

    // Now user changes difficulty to Thông hiểu, triggering Request B
    const updatedDiffSelects = screen.getAllByRole('combobox');
    fireEvent.click(updatedDiffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Thông hiểu' }));

    // Trigger Request B
    await sleep(450);
    expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalledTimes(2);

    // Wait for Request B to display 12 câu
    expect(await screen.findByText('12 câu')).toBeInTheDocument();

    // Now delayed Request A resolves with old count 3
    resolveA({
      data: {
        sections: [
          {
            clientSectionId: 'old-sec',
            rows: [{ clientRowId: 'mock-row-1', availableCount: 3, requiredCount: 1, shortage: 0 }],
          },
        ],
        wholeBlueprintFeasible: true,
      },
    });

    await sleep(100);

    // Request B's 12 câu must NOT be overwritten by Request A!
    expect(screen.getByText('12 câu')).toBeInTheDocument();
    expect(screen.queryByText('3 câu')).not.toBeInTheDocument();
  });

  it('handles overlap conflict where individual rows have 0 shortage but overall blueprint fails', async () => {
    testGeneratorApi.checkBlueprintAvailability.mockImplementation(async (payload) => {
      const rowId = payload.sections[0].rows[0].clientRowId;
      return {
        data: {
          checkedAt: '2026-09-14T01:23:45.0000000+00:00',
          sections: [
            {
              clientSectionId: payload.sections[0].clientSectionId,
              rows: [
                { clientRowId: rowId, availableCount: 5, requiredCount: 5, shortage: 0 },
              ],
            },
          ],
          wholeBlueprintFeasible: false,
          availabilityCode: 'BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT',
        },
      };
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    await sleep(450);

    await waitFor(() => {
      expect(testGeneratorApi.checkBlueprintAvailability).toHaveBeenCalled();
    });

    // Row shows 5 câu without shortage badge
    expect(await screen.findByText('5 câu')).toBeInTheDocument();
    expect(screen.queryByText(/Thiếu/i)).not.toBeInTheDocument();

    // Summary displays overlap conflict alert!
    expect(screen.getByText('Xung đột trùng lặp câu hỏi')).toBeInTheDocument();
    expect(screen.getByText(/Các dòng phân bổ đủ câu hỏi riêng lẻ nhưng bị trùng lặp tập câu hỏi/i)).toBeInTheDocument();
  });

  it('allows saving draft even when capacity is insufficient or preview failed', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValue({
      data: { blueprintId: 'bp-new-saved', blueprintName: 'Đề thi nháp' },
    });
    testGeneratorApi.checkBlueprintAvailability.mockRejectedValue(new Error('Preview failure'));

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Fill valid form
    const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
    fireEvent.change(nameInput, { target: { value: 'Đề kiểm tra lưu nháp thành công' } });

    const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
    fireEvent.change(totalQuestionsInput, { target: { value: '1' } });

    const secNameInput = screen.getByPlaceholderText(/VD: Trắc nghiệm khách quan nhiều lựa chọn/i);
    fireEvent.change(secNameInput, { target: { value: 'Phần 1' } });

    const secTotalInput = screen.getByPlaceholderText(/VD: 10/i);
    fireEvent.change(secTotalInput, { target: { value: '1' } });

    const selects = screen.getAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    // Click "Lưu bản nháp"
    const saveDraftBtn = screen.getByRole('button', { name: 'Lưu bản nháp' });
    fireEvent.click(saveDraftBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalled();
    });
    expect(mockNavigate).toHaveBeenCalledWith(
      '/expert/blueprints/bp-new-saved',
      expect.objectContaining({
        state: expect.objectContaining({ newlyCreatedBlueprintId: 'bp-new-saved' }),
      })
    );
  });

  it('handles submit 409, displays localized error, and preserves form edits', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValue({
      data: { blueprintId: 'bp-sub-1', blueprintName: 'Đề thi gửi duyệt' },
    });

    const error409 = {
      response: {
        status: 409,
        data: {
          code: 'BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT',
          message: 'The rows are individually available, but the same questions cannot satisfy the whole blueprint.',
        },
      },
    };
    testGeneratorApi.submitBlueprintForReview.mockRejectedValue(error409);

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Fill form
    const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
    fireEvent.change(nameInput, { target: { value: 'Cấu trúc đề kiểm tra submit 409' } });

    const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
    fireEvent.change(totalQuestionsInput, { target: { value: '1' } });

    const secNameInput = screen.getByPlaceholderText(/VD: Trắc nghiệm khách quan nhiều lựa chọn/i);
    fireEvent.change(secNameInput, { target: { value: 'Phần 1' } });

    const secTotalInput = screen.getByPlaceholderText(/VD: 10/i);
    fireEvent.change(secTotalInput, { target: { value: '1' } });

    const selects = screen.getAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    // Click "Lưu & gửi phản biện"
    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalled();
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-sub-1');
    });

    // Form data must be PRESERVED (no navigate away)
    expect(mockNavigate).not.toHaveBeenCalled();
    expect(screen.getByDisplayValue('Cấu trúc đề kiểm tra submit 409')).toBeInTheDocument();

    // Error banner displays localized 409 message
    expect(await screen.findByText(/Các phần thi bị xung đột trùng lặp câu hỏi/i)).toBeInTheDocument();
  });

  it('handles row removal cleanly when request resolves, preserving remaining row data', async () => {
    testGeneratorApi.checkBlueprintAvailability.mockImplementation(async (payload) => {
      return {
        data: {
          sections: payload.sections.map((sec) => ({
            clientSectionId: sec.clientSectionId,
            rows: sec.rows.map((r) => ({
              clientRowId: r.clientRowId,
              availableCount: 9,
              requiredCount: r.quantity,
              shortage: 0,
            })),
          })),
          wholeBlueprintFeasible: true,
        },
      };
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    // Row 1
    const selects = await screen.findAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

    // Add Row 2
    fireEvent.click(screen.getByRole('button', { name: /Thêm dòng phân bổ/i }));

    const updatedSelects = screen.getAllByRole('combobox');
    fireEvent.click(updatedSelects[4]);
    fireEvent.click(await screen.findByText('Hình học không gian'));

    const updatedDiffs = screen.getAllByRole('combobox');
    fireEvent.click(updatedDiffs[5]);
    fireEvent.click(await screen.findByRole('option', { name: 'Thông hiểu' }));

    await sleep(450);

    const table = screen.getByRole('table');
    expect(await within(table).findAllByText('9 câu')).toHaveLength(2);

    // Now delete Row 1
    const deleteButtons = screen.getAllByRole('button', { name: /Xóa dòng phân bổ này/i });
    fireEvent.click(deleteButtons[0]);

    await sleep(450);

    const remainingRows = within(table).getAllByRole('row');
    // Header row + 1 data row = 2 rows
    expect(remainingRows).toHaveLength(2);
    expect(within(table).getByText('9 câu')).toBeInTheDocument();
  });
});

describe('FE Task 2 Regressions: handleSaveAndSubmit draft identity preservation and retry', () => {
  const fillValidBlueprintForm = async (title = 'Cấu trúc đề kiểm tra hồi quy X') => {
    const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
    fireEvent.change(nameInput, { target: { value: title } });

    const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
    fireEvent.change(totalQuestionsInput, { target: { value: '1' } });

    const secNameInput = screen.getByPlaceholderText(/VD: Trắc nghiệm khách quan nhiều lựa chọn/i);
    fireEvent.change(secNameInput, { target: { value: 'Phần 1' } });

    const secTotalInput = screen.getByPlaceholderText(/VD: 10/i);
    fireEvent.change(secTotalInput, { target: { value: '1' } });

    const selects = screen.getAllByRole('combobox');
    fireEvent.click(selects[2]);
    fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

    const diffSelects = screen.getAllByRole('combobox');
    fireEvent.click(diffSelects[3]);
    fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));
  };

  it('regression 1 & 2: create returns ID X, submit 409 -> user edits form and resubmits -> create called once, update and submit use X', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X', blueprintName: 'Cấu trúc đề kiểm tra hồi quy X' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce({
      response: {
        status: 409,
        data: {
          code: 'BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT',
          message: 'The rows are individually available, but the same questions cannot satisfy the whole blueprint.',
        },
      },
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    await fillValidBlueprintForm('Cấu trúc đề kiểm tra hồi quy X');

    // First submit -> create succeeds with bp-X, submit fails with 409
    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-X');
    });

    // 409 error banner shown, form data and client row IDs preserved
    expect(await screen.findByText(/Các phần thi bị xung đột trùng lặp câu hỏi/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue('Cấu trúc đề kiểm tra hồi quy X')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/expert/blueprints/bp-X/edit');

    // User edits form
    const nameInput = screen.getByDisplayValue('Cấu trúc đề kiểm tra hồi quy X');
    fireEvent.change(nameInput, { target: { value: 'Cấu trúc đề kiểm tra hồi quy X - Đã cập nhật' } });

    // Configure update and second submit to succeed
    testGeneratorApi.updateBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X', blueprintName: 'Cấu trúc đề kiểm tra hồi quy X - Đã cập nhật' },
    });
    testGeneratorApi.submitBlueprintForReview.mockResolvedValueOnce({
      data: { success: true },
    });

    // Click submit again
    fireEvent.click(submitBtn);

    await waitFor(() => {
      // create must NOT be called again!
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      // update must be called with bp-X
      expect(testGeneratorApi.updateBlueprint).toHaveBeenCalledWith(
        'bp-X',
        expect.objectContaining({ blueprintName: 'Cấu trúc đề kiểm tra hồi quy X - Đã cập nhật' })
      );
      // submit must be called with bp-X
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-X');
    });

    // Successful submit navigates to detail page
    expect(mockNavigate).toHaveBeenCalledWith(
      '/expert/blueprints/bp-X',
      expect.objectContaining({
        state: expect.objectContaining({ feedback: expect.objectContaining({ type: 'success' }) }),
      })
    );
  });

  it('regression 3: after submit 409, clicking save draft updates draft X rather than creating a new draft', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce({
      response: {
        status: 409,
        data: { code: 'BLUEPRINT_AVAILABILITY_INSUFFICIENT_QUESTIONS' },
      },
    });

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    await fillValidBlueprintForm('Cấu trúc đề kiểm tra hồi quy X');

    // Submit fails with 409
    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-X');
    });

    // User decides to save draft instead
    testGeneratorApi.updateBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X' },
    });

    const saveDraftBtn = screen.getByRole('button', { name: /Lưu bản nháp|Lưu thay đổi/i });
    fireEvent.click(saveDraftBtn);

    await waitFor(() => {
      // updateBlueprint must be called with bp-X
      expect(testGeneratorApi.updateBlueprint).toHaveBeenCalledWith('bp-X', expect.any(Object));
      // createBlueprint must NOT be called a second time
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
    });

    expect(mockNavigate).toHaveBeenCalledWith(
      '/expert/blueprints/bp-X',
      expect.objectContaining({
        state: expect.objectContaining({ feedback: expect.objectContaining({ type: 'success' }) }),
      })
    );
  });

  it('regression 4: reload route after successful create synchronizes route and loads draft X', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce({
      response: {
        status: 409,
        data: { code: 'BLUEPRINT_AVAILABILITY_INSUFFICIENT_QUESTIONS' },
      },
    });

    const { unmount } = render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    await fillValidBlueprintForm('Cấu trúc đề kiểm tra hồi quy X');

    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
    });

    // Route was synchronized to edit route
    expect(window.location.pathname).toBe('/expert/blueprints/bp-X/edit');

    // Simulate reloading the route: unmount and remount at /expert/blueprints/bp-X/edit
    unmount();

    testGeneratorApi.getBlueprintDetail.mockResolvedValueOnce({
      data: {
        blueprintId: 'bp-X',
        blueprintName: 'Cấu trúc đề đã lưu X từ máy chủ',
        grade: 12,
        totalQuestions: 1,
        totalScore: 10,
        durationMinutes: 90,
        status: 'Draft',
        expertId: 'acc-test-expert',
        sections: [
          {
            sectionCode: 'SEC1',
            sectionName: 'Phần 1',
            questionType: 'SingleChoice',
            scoringRule: 'AllOrNothing',
            totalQuestions: 1,
            scoreBudget: 10,
            details: [
              {
                tagId: 'topic-1',
                difficultyId: 'diff-1',
                quantity: 1,
              },
            ],
          },
        ],
      },
    });

    mockParams = { blueprintId: 'bp-X' };
    mockLocation = { pathname: '/expert/blueprints/bp-X/edit', state: null };

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(testGeneratorApi.getBlueprintDetail).toHaveBeenCalledWith('bp-X');
    });

    expect(await screen.findByDisplayValue('Cấu trúc đề đã lưu X từ máy chủ')).toBeInTheDocument();
  });

  it('regression 5: submit network error after create success does not lose draft identity and does not auto-retry', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce(
      new Error('Network Error: Failed to connect to server')
    );

    render(
      <BrowserRouter>
        <BlueprintEditorPage />
      </BrowserRouter>
    );

    await fillValidBlueprintForm('Cấu trúc đề kiểm tra hồi quy X');

    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-X');
    });

    // Verify no automatic mutation retry occurred
    await sleep(100);
    expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledTimes(1);
    expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);

    // Form data and route preserved
    expect(screen.getByDisplayValue('Cấu trúc đề kiểm tra hồi quy X')).toBeInTheDocument();
    expect(window.location.pathname).toBe('/expert/blueprints/bp-X/edit');

    // Next user action uses updateBlueprint on bp-X, never createBlueprint
    testGeneratorApi.updateBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-X' },
    });

    const saveDraftBtn = screen.getByRole('button', { name: /Lưu bản nháp|Lưu thay đổi/i });
    fireEvent.click(saveDraftBtn);

    await waitFor(() => {
      expect(testGeneratorApi.updateBlueprint).toHaveBeenCalledWith('bp-X', expect.any(Object));
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
    });
  });
});
