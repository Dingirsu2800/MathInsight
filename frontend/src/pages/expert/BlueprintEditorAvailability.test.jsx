import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
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
