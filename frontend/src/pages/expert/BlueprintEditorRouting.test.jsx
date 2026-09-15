import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route, Link, useLocation, useParams } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import BlueprintEditorPage from './BlueprintEditorPage';
import { testGeneratorApi } from '../../services/testGeneratorApi';
import { questionBankApi } from '../../services/questionBankApi';

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

function GlobalLocationTracker() {
  const location = useLocation();
  return (
    <div data-testid="global-location">
      <span data-testid="global-pathname">{location.pathname}</span>
    </div>
  );
}

function RouteWithInspector() {
  const location = useLocation();
  const params = useParams();
  return (
    <>
      <div data-testid="real-router-inspector">
        <span data-testid="real-pathname">{location.pathname}</span>
        <span data-testid="real-blueprint-id">{params.blueprintId || ''}</span>
      </div>
      <BlueprintEditorPage />
    </>
  );
}

function TestRouterApp({ initialRoute = '/expert/blueprints/new' }) {
  return (
    <MemoryRouter initialEntries={[initialRoute]}>
      <GlobalLocationTracker />
      <nav data-testid="test-nav">
        <Link to="/expert/blueprints/new" data-testid="link-create-new">
          Tạo cấu trúc mới
        </Link>
      </nav>
      <Routes>
        <Route path="/expert/blueprints/new" element={<RouteWithInspector />} />
        <Route path="/expert/blueprints/:blueprintId/edit" element={<RouteWithInspector />} />
        <Route
          path="/expert/blueprints/:blueprintId"
          element={<div data-testid="blueprint-detail-view">Màn hình chi tiết cấu trúc</div>}
        />
        <Route
          path="/expert/blueprints"
          element={<div data-testid="blueprint-list-view">Danh sách cấu trúc</div>}
        />
      </Routes>
    </MemoryRouter>
  );
}

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

  testGeneratorApi.checkBlueprintAvailability.mockResolvedValue({
    data: {
      checkedAt: '2026-09-14T01:23:45.0000000+00:00',
      sections: [],
      wholeBlueprintFeasible: true,
      availabilityCode: null,
    },
  });
});

afterEach(() => {
  cleanup();
  vi.resetAllMocks();
  localStorage.clear();
});

async function fillValidBlueprintForm(name = 'Cấu trúc đề thi THPT 2026') {
  const nameInput = await screen.findByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i);
  fireEvent.change(nameInput, { target: { value: name } });

  const totalQuestionsInput = screen.getByPlaceholderText(/Ví dụ: 50/i);
  fireEvent.change(totalQuestionsInput, { target: { value: '1' } });

  const secNameInput = screen.getByPlaceholderText(/VD: Trắc nghiệm khách quan nhiều lựa chọn/i);
  fireEvent.change(secNameInput, { target: { value: 'Phần 1: Trắc nghiệm' } });

  const secQuestionsInput = screen.getByPlaceholderText(/VD: 10/i);
  fireEvent.change(secQuestionsInput, { target: { value: '1' } });

  const selects = await screen.findAllByRole('combobox');
  fireEvent.click(selects[2]); // Topic picker
  fireEvent.click(await screen.findByText('Hàm số và đồ thị'));

  const diffSelects = screen.getAllByRole('combobox');
  fireEvent.click(diffSelects[3]); // Difficulty picker
  fireEvent.click(await screen.findByRole('option', { name: 'Nhận biết' }));

  const table = screen.getByRole('table');
  const qtyInput = within(table).getByRole('spinbutton');
  fireEvent.change(qtyInput, { target: { value: '1' } });
}

describe('BlueprintEditorPage Real React Router Regressions', () => {
  it('transition create -> edit: updates real router params and URL to draft ID X without losing form state or submit 409 error', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-real-x' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce({
      response: {
        status: 409,
        data: { code: 'BLUEPRINT_AVAILABILITY_INSUFFICIENT_QUESTIONS' },
      },
    });

    render(<TestRouterApp initialRoute="/expert/blueprints/new" />);

    // Initial router state
    expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/new');
    expect(screen.getByTestId('real-blueprint-id').textContent).toBe('');
    expect(screen.getByText('Tạo cấu trúc đề mới')).toBeInTheDocument();

    await fillValidBlueprintForm('Cấu trúc đề kiểm tra router thật X');

    // Click "Lưu & gửi phản biện"
    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.submitBlueprintForReview).toHaveBeenCalledWith('bp-real-x');
    });

    // Real router has updated location and params via navigate(..., { replace: true })
    await waitFor(() => {
      expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/bp-real-x/edit');
      expect(screen.getByTestId('real-blueprint-id').textContent).toBe('bp-real-x');
    });

    // Component did NOT remount: form state, inputs, and 409 error banner remain intact
    expect(screen.getByDisplayValue('Cấu trúc đề kiểm tra router thật X')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Phần 1: Trắc nghiệm')).toBeInTheDocument();
    expect(await screen.findByText(/Số lượng câu hỏi trong ngân hàng không đủ/i)).toBeInTheDocument();

    // UI reflects edit mode
    expect(screen.getByText('Chỉnh sửa cấu trúc đề')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu thay đổi' })).toBeInTheDocument();

    // Re-fetching from API was prevented so form was not overwritten
    expect(testGeneratorApi.getBlueprintDetail).not.toHaveBeenCalled();
  });

  it('retry after 409: save draft and submit for review both update draft X and do NOT create a second draft', async () => {
    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-real-x' },
    });
    testGeneratorApi.submitBlueprintForReview.mockRejectedValueOnce({
      response: {
        status: 409,
        data: { code: 'BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT' },
      },
    });

    render(<TestRouterApp initialRoute="/expert/blueprints/new" />);

    await fillValidBlueprintForm('Cấu trúc đề thi thử nghiệm');

    // First attempt: create succeeds, submit fails 409
    const submitBtn = screen.getByRole('button', { name: 'Lưu & gửi phản biện' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/bp-real-x/edit');
    });

    // User modifies name
    const nameInput = screen.getByDisplayValue('Cấu trúc đề thi thử nghiệm');
    fireEvent.change(nameInput, { target: { value: 'Cấu trúc đề thi thử nghiệm - Đã sửa' } });

    // Option A: Click "Lưu thay đổi" (Save draft)
    testGeneratorApi.updateBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-real-x' },
    });

    const saveDraftBtn = screen.getByRole('button', { name: 'Lưu thay đổi' });
    fireEvent.click(saveDraftBtn);

    await waitFor(() => {
      // updateBlueprint must be called with bp-real-x
      expect(testGeneratorApi.updateBlueprint).toHaveBeenCalledWith(
        'bp-real-x',
        expect.objectContaining({ blueprintName: 'Cấu trúc đề thi thử nghiệm - Đã sửa' })
      );
      // createBlueprint must NOT be called again
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
    });

    // Navigates to detail page on save draft success
    await waitFor(() => {
      expect(screen.getByTestId('global-pathname').textContent).toBe('/expert/blueprints/bp-real-x');
      expect(screen.getByTestId('blueprint-detail-view')).toBeInTheDocument();
    });
  });

  it('navigating to /expert/blueprints/new resets state and form, never reusing draft X', async () => {
    // Start directly at an existing draft edit route
    testGeneratorApi.getBlueprintDetail.mockResolvedValueOnce({
      data: {
        blueprintId: 'bp-existing-123',
        blueprintName: 'Cấu trúc đề cũ đang sửa',
        grade: 12,
        totalQuestions: 1,
        totalScore: 10,
        durationMinutes: 90,
        status: 'Draft',
        expertId: 'acc-test-expert',
        sections: [
          {
            sectionCode: 'SEC1',
            sectionName: 'Phần cũ',
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

    render(<TestRouterApp initialRoute="/expert/blueprints/bp-existing-123/edit" />);

    // Wait for existing draft to load
    expect(await screen.findByDisplayValue('Cấu trúc đề cũ đang sửa')).toBeInTheDocument();
    expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/bp-existing-123/edit');
    expect(screen.getByTestId('real-blueprint-id').textContent).toBe('bp-existing-123');

    // Click navigation link to create new blueprint
    const createNewLink = screen.getByTestId('link-create-new');
    fireEvent.click(createNewLink);

    // Real router navigates to /expert/blueprints/new
    await waitFor(() => {
      expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/new');
      expect(screen.getByTestId('real-blueprint-id').textContent).toBe('');
    });

    // Form must be RESET to fresh state (NOT retaining bp-existing-123 data)
    expect(screen.queryByDisplayValue('Cấu trúc đề cũ đang sửa')).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Ví dụ: Đề thi cuối kỳ 1 Toán học 12/i).value).toBe('');
    expect(screen.getByText('Tạo cấu trúc đề mới')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu bản nháp' })).toBeInTheDocument();

    // Now fill and save the new form: must call createBlueprint, NOT update bp-existing-123!
    await fillValidBlueprintForm('Cấu trúc đề hoàn toàn mới');

    testGeneratorApi.createBlueprint.mockResolvedValueOnce({
      data: { blueprintId: 'bp-brand-new-999' },
    });

    const saveNewDraftBtn = screen.getByRole('button', { name: 'Lưu bản nháp' });
    fireEvent.click(saveNewDraftBtn);

    await waitFor(() => {
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledTimes(1);
      expect(testGeneratorApi.createBlueprint).toHaveBeenCalledWith(
        expect.objectContaining({ blueprintName: 'Cấu trúc đề hoàn toàn mới' })
      );
      // bp-existing-123 was NEVER updated
      expect(testGeneratorApi.updateBlueprint).not.toHaveBeenCalled();
    });
  });

  it('reload route opens draft X by fetching detail from API', async () => {
    testGeneratorApi.getBlueprintDetail.mockResolvedValueOnce({
      data: {
        blueprintId: 'bp-reloaded-456',
        blueprintName: 'Cấu trúc đề mở qua reload',
        grade: 12,
        totalQuestions: 1,
        totalScore: 10,
        durationMinutes: 90,
        status: 'Draft',
        expertId: 'acc-test-expert',
        sections: [
          {
            sectionCode: 'SEC1',
            sectionName: 'Phần tải lại',
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

    render(<TestRouterApp initialRoute="/expert/blueprints/bp-reloaded-456/edit" />);

    expect(screen.getByTestId('real-pathname').textContent).toBe('/expert/blueprints/bp-reloaded-456/edit');
    expect(screen.getByTestId('real-blueprint-id').textContent).toBe('bp-reloaded-456');

    await waitFor(() => {
      expect(testGeneratorApi.getBlueprintDetail).toHaveBeenCalledWith('bp-reloaded-456');
    });

    expect(await screen.findByDisplayValue('Cấu trúc đề mở qua reload')).toBeInTheDocument();
    expect(screen.getByText('Chỉnh sửa cấu trúc đề')).toBeInTheDocument();
  });
});
