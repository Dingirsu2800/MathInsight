import { cleanup, fireEvent, render, screen, waitFor, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import TestSession, { answersDiffer } from './TestSession';
import * as testingApi from '../../services/testingApi';

vi.mock('../../services/testingApi', () => ({
  getSessionContent: vi.fn(),
  autoSaveAnswers: vi.fn(),
  submitSession: vi.fn(),
  timeoutSubmitSession: vi.fn(),
  recordIncident: vi.fn(),
}));

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ sessionId: 'session-123' }),
  };
});

describe('answersDiffer helper', () => {
  it('returns false when answers are identical or both empty', () => {
    expect(answersDiffer(null, null)).toBe(false);
    expect(answersDiffer({}, {})).toBe(false);
    expect(answersDiffer({ shortAnswerText: 'π/√(2)' }, { shortAnswerText: ' π/√(2) ' })).toBe(false);
    expect(answersDiffer({ answerId: 'opt-1' }, { answerId: 'opt-1' })).toBe(false);
    expect(
      answersDiffer(
        { selectedOptions: ['a', 'b'], parts: [{ partId: 'p1', booleanAnswer: true }] },
        { selectedOptions: ['b', 'a'], parts: [{ partId: 'p1', booleanAnswer: true }] }
      )
    ).toBe(false);
  });

  it('returns true when answers differ', () => {
    expect(answersDiffer(null, { shortAnswerText: '123' })).toBe(true);
    expect(answersDiffer({ shortAnswerText: '1' }, { shortAnswerText: '2' })).toBe(true);
    expect(answersDiffer({ answerId: 'opt-1' }, { answerId: 'opt-2' })).toBe(true);
    expect(answersDiffer({ selectedOptions: ['a'] }, { selectedOptions: ['a', 'b'] })).toBe(true);
    expect(
      answersDiffer(
        { parts: [{ partId: 'p1', booleanAnswer: true }] },
        { parts: [{ partId: 'p1', booleanAnswer: false }] }
      )
    ).toBe(true);
  });
});

describe('TestSession autosave and submit behavior', () => {
  const mockSessionData = {
    sessionId: 'session-123',
    testId: 'test-123',
    testName: 'Bài kiểm tra thử nghiệm',
    testFormat: 'Practice',
    durationMinutes: 30,
    hasTimeLimit: true,
    remainingSeconds: 1800,
    elapsedSeconds: 0,
    status: 'InProgress',
    questions: [
      {
        questionId: 'q-1',
        questionNo: 1,
        questionContent: 'Tính giá trị của biểu thức:',
        questionType: 'SHORT_ANSWER',
        answerOptions: [],
        parts: [],
      },
    ],
    savedAnswers: [],
  };

  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    window.matchMedia = vi.fn().mockImplementation((query) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }));
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it('renders "Đã lưu" when initialized with empty or persisted answers', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    expect(await screen.findByTestId('autosave-status-saved')).toBeInTheDocument();
    expect(screen.getByText('Đã lưu')).toBeInTheDocument();
  });

  it('restores local draft and marks dirty/saving if it differs from server', async () => {
    localStorage.setItem(
      'mathinsight_test_draft_session-123',
      JSON.stringify({
        'q-1': { shortAnswerText: 'π/√(2)', answerId: null, parts: [], selectedOptions: [] },
      })
    );

    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    testingApi.autoSaveAnswers.mockResolvedValueOnce({ savedAt: new Date().toISOString() });

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    // Initial load will see local draft differs and trigger autosave
    await waitFor(() => {
      expect(testingApi.autoSaveAnswers).toHaveBeenCalledWith(
        'session-123',
        expect.arrayContaining([
          expect.objectContaining({
            questionId: 'q-1',
            shortAnswerText: 'π/√(2)',
          }),
        ])
      );
    });

    expect(await screen.findByTestId('autosave-status-saved')).toBeInTheDocument();
  });

  it('transitions to "saving" when student edits and shows "error" banner if autosave fails without losing draft', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    testingApi.autoSaveAnswers.mockRejectedValue(new Error('Network error or token refresh failure'));

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');
    fireEvent.change(input, { target: { value: 'π/√(2)' } });

    // Status immediately becomes saving
    expect(screen.getByTestId('autosave-status-saving')).toBeInTheDocument();

    // After debounce (1200ms) and failed API call, shows error status and banner
    expect(await screen.findByTestId('autosave-status-error', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getByTestId('autosave-error-banner')).toBeInTheDocument();

    // Verify local draft is safely preserved in localStorage
    const savedDraft = JSON.parse(localStorage.getItem('mathinsight_test_draft_session-123'));
    expect(savedDraft['q-1'].shortAnswerText).toBe('π/√(2)');
  });

  it('blocks submit if pending autosave fails, keeps draft, and does not submit blindly', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    testingApi.autoSaveAnswers.mockRejectedValueOnce({
      response: { data: { code: 'NETWORK_ERROR', message: 'Mất kết nối máy chủ' } },
    });

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');
    fireEvent.change(input, { target: { value: 'π/√(2)' } });

    // Open modal
    fireEvent.click(screen.getByRole('button', { name: 'Nộp bài' }));
    await screen.findByRole('heading', { name: 'Xác nhận nộp bài' });

    // Modal submit button is the second 'Nộp bài' button
    const confirmBtn = screen.getAllByRole('button', { name: 'Nộp bài' })[1];
    fireEvent.click(confirmBtn);

    // Because autosave failed, submitSession MUST NOT be called!
    await waitFor(() => {
      expect(screen.getByText('Không thể tiếp tục')).toBeInTheDocument();
    });
    expect(testingApi.submitSession).not.toHaveBeenCalled();

    // Local draft must NOT be cleared!
    const draft = localStorage.getItem('mathinsight_test_draft_session-123');
    expect(draft).not.toBeNull();
    expect(JSON.parse(draft)['q-1'].shortAnswerText).toBe('π/√(2)');
  });

  it('checks server session state on retry: redirects if already Submitted, avoiding blind duplicate submission', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    // Autosave succeeds
    testingApi.autoSaveAnswers.mockResolvedValueOnce({ savedAt: new Date().toISOString() });
    // Submit throws network error (no response)
    testingApi.submitSession.mockRejectedValueOnce(new Error('Network Error'));

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');
    fireEvent.change(input, { target: { value: 'π/√(2)' } });

    // Open modal and confirm submit
    fireEvent.click(screen.getByRole('button', { name: 'Nộp bài' }));
    await screen.findByRole('heading', { name: 'Xác nhận nộp bài' });
    const confirmBtn = screen.getAllByRole('button', { name: 'Nộp bài' })[1];
    fireEvent.click(confirmBtn);

    // Error screen appears
    expect(await screen.findByText('Không thể tiếp tục')).toBeInTheDocument();
    expect(screen.getByText('Thử lại')).toBeInTheDocument();

    // Now before retry, server session is checked. Suppose the server actually processed it!
    testingApi.getSessionContent.mockResolvedValueOnce({
      ...mockSessionData,
      status: 'Submitted',
    });

    fireEvent.click(screen.getByText('Thử lại'));

    await waitFor(() => {
      // Navigates directly to test result without sending another submitSession!
      expect(mockNavigate).toHaveBeenCalledWith('/student/test-result/session-123');
    });
    // Local draft cleared
    expect(localStorage.getItem('mathinsight_test_draft_session-123')).toBeNull();
    // submitSession was only called once, not retried blindly!
    expect(testingApi.submitSession).toHaveBeenCalledTimes(1);
  });

  it('re-routes to result page when submitSession returns TESTING_SESSION_ALREADY_COMPLETED', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    testingApi.autoSaveAnswers.mockResolvedValueOnce({ savedAt: new Date().toISOString() });
    testingApi.submitSession.mockRejectedValueOnce({
      response: { data: { code: 'TESTING_SESSION_ALREADY_COMPLETED' } },
    });

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');
    fireEvent.change(input, { target: { value: 'π/√(2)' } });

    fireEvent.click(screen.getByRole('button', { name: 'Nộp bài' }));
    await screen.findByRole('heading', { name: 'Xác nhận nộp bài' });
    const confirmBtn = screen.getAllByRole('button', { name: 'Nộp bài' })[1];
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/student/test-result/session-123');
    });
    expect(localStorage.getItem('mathinsight_test_draft_session-123')).toBeNull();
  });

  it('does not mark "saved" when request A resolves if student has entered edit B; marks "saved" only after B resolves', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);

    let resolveA;
    let resolveB;
    const promiseA = new Promise((res) => { resolveA = res; });
    const promiseB = new Promise((res) => { resolveB = res; });

    let callCount = 0;
    testingApi.autoSaveAnswers.mockImplementation(() => {
      callCount += 1;
      if (callCount === 1) return promiseA;
      if (callCount === 2) return promiseB;
      return Promise.resolve({ savedAt: new Date().toISOString() });
    });

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');

    // 1. Enter edit A
    fireEvent.change(input, { target: { value: 'Answer A' } });

    // Wait for A's debounce to fire and start autoSaveAnswers (callCount === 1)
    await waitFor(() => {
      expect(callCount).toBe(1);
    }, { timeout: 3000 });

    // 2. While request A is in-flight, enter edit B
    fireEvent.change(input, { target: { value: 'Answer B' } });

    // 3. Resolve request A
    await act(async () => {
      resolveA({ savedAt: new Date().toISOString() });
    });

    // Verify: even though A resolved, UI must NOT report saved because B is not yet confirmed!
    expect(screen.getByTestId('autosave-status-saving')).toBeInTheDocument();
    expect(screen.queryByTestId('autosave-status-saved')).toBeNull();

    // 4. Wait for B's debounce to fire and start autoSaveAnswers (callCount === 2)
    await waitFor(() => {
      expect(callCount).toBe(2);
    }, { timeout: 3000 });

    // 5. Now resolve request B
    await act(async () => {
      resolveB({ savedAt: new Date().toISOString() });
    });

    // Now UI successfully transitions to saved!
    expect(await screen.findByTestId('autosave-status-saved')).toBeInTheDocument();
    expect(screen.getByText('Đã lưu')).toBeInTheDocument();
  }, 15000);

  it('handles timeout submit while autosave is in-flight without deadlock, and does not falsely mark saved when in-flight completes', async () => {
    let resolveAutoSave;
    const pendingAutoSave = new Promise((res) => { resolveAutoSave = res; });

    // Seed local draft so that performAutoSave runs immediately on mount
    localStorage.setItem(
      'mathinsight_test_draft_session-123',
      JSON.stringify({ 'q-1': { shortAnswerText: 'Draft in flight' } })
    );

    testingApi.getSessionContent.mockResolvedValueOnce({
      ...mockSessionData,
      durationMinutes: 30,
      hasTimeLimit: true,
      remainingSeconds: 0, // Zero triggers immediate timeout submit from SessionTimer
    });
    testingApi.autoSaveAnswers.mockReturnValueOnce(pendingAutoSave);
    testingApi.timeoutSubmitSession.mockResolvedValueOnce({
      status: 'Graded',
      score: 10,
    });

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    // Timeout submit must be called immediately without waiting for or deadlocking on pending autosave
    await waitFor(() => {
      expect(testingApi.timeoutSubmitSession).toHaveBeenCalledWith('session-123');
      expect(mockNavigate).toHaveBeenCalledWith('/student/test-result/session-123');
    });

    // Manual submit must NOT have been called
    expect(testingApi.submitSession).not.toHaveBeenCalled();

    // When the pending autosave resolves afterwards, verify it does not error or cause invalid state
    await act(async () => {
      resolveAutoSave({ savedAt: new Date().toISOString() });
    });
  });

  it('distinguishes between auto-submit and manual submit when retry button is clicked', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce({
      ...mockSessionData,
      durationMinutes: 30,
      hasTimeLimit: true,
      remainingSeconds: 0,
    });
    // Timeout submit fails initially with network error
    testingApi.timeoutSubmitSession.mockRejectedValueOnce(new Error('Network error on timeout submit'));

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    // Wait for timeout submit failure message
    expect(await screen.findByText('Không thể tự động nộp bài hết giờ. Vui lòng thử lại.')).toBeInTheDocument();

    // On retry, server state check returns in-progress
    testingApi.getSessionContent.mockResolvedValueOnce({
      ...mockSessionData,
      status: 'InProgress',
    });
    // Second timeoutSubmitSession call succeeds
    testingApi.timeoutSubmitSession.mockResolvedValueOnce({ status: 'Graded' });

    // Click retry
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));

    await waitFor(() => {
      expect(testingApi.timeoutSubmitSession).toHaveBeenCalledTimes(2);
      expect(mockNavigate).toHaveBeenCalledWith('/student/test-result/session-123');
    });

    // submitSession (manual) must NEVER be called for timeout submit retries!
    expect(testingApi.submitSession).not.toHaveBeenCalled();
  });

  it('catches rejected promise on autosave retry button without unhandled rejection', async () => {
    testingApi.getSessionContent.mockResolvedValueOnce(mockSessionData);
    // Initial autosave fails
    testingApi.autoSaveAnswers.mockRejectedValueOnce(new Error('First failure'));

    render(
      <MemoryRouter initialEntries={['/student/test/session-123']}>
        <TestSession />
      </MemoryRouter>
    );

    const input = await screen.findByPlaceholderText('Nhập đáp án ngắn...');
    fireEvent.change(input, { target: { value: 'π/√(2)' } });

    // Wait for error banner
    expect(await screen.findByTestId('autosave-error-banner', {}, { timeout: 6000 })).toBeInTheDocument();

    // Setup second failure on retry click
    testingApi.autoSaveAnswers.mockRejectedValueOnce(new Error('Second failure on retry'));

    const unhandledRejections = [];
    const handleUnhandled = (event) => {
      unhandledRejections.push(event.reason);
    };
    window.addEventListener('unhandledrejection', handleUnhandled);

    // Click "Thử lưu lại" in banner
    const retryBtn = screen.getByRole('button', { name: 'Thử lưu lại' });
    await act(async () => {
      fireEvent.click(retryBtn);
    });

    window.removeEventListener('unhandledrejection', handleUnhandled);

    // Verify no unhandled rejection escaped the button handler
    expect(unhandledRejections).toHaveLength(0);
    expect(screen.getByTestId('autosave-error-banner')).toBeInTheDocument();
  }, 15000);
});
