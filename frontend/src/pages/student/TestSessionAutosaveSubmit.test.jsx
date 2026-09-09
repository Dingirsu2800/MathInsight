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
    vi.clearAllMocks();
    localStorage.clear();
    mockNavigate.mockReset();
    window.matchMedia = window.matchMedia || vi.fn().mockImplementation((query) => ({
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
});
