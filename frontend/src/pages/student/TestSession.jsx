import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ExamLayout from '../../components/layout/ExamLayout';
import QuestionPanel from './test-session/QuestionPanel';
import QuestionNav from './test-session/QuestionNav';
import SessionTimer from './test-session/SessionTimer';
import SubmitConfirmModal from './test-session/SubmitConfirmModal';
import TabSwitchWarningModal from './test-session/TabSwitchWarningModal';
import {
  autoSaveAnswers,
  getSessionContent,
  recordIncident,
  submitSession,
  timeoutSubmitSession,
} from '../../services/testingApi';
import { getTestGenErrorMessage } from '../../utils/testGenerationErrorLocalizer';
import {
  toAutoSavePayload,
  toFiniteNumericAnswer,
} from './test-session/answerPayload';

const AUTO_SAVE_INTERVAL_MS = 5 * 60 * 1000;
const AUTO_SAVE_DEBOUNCE_MS = 1200;

function adaptQuestion(question) {
  return {
    ...question,
    options: (question.answerOptions || []).map((option) => ({
      optionId: option.answerId,
      content: option.answerContent,
    })),
    parts: (question.parts || []).map((part) => {
      const normalizedType = (part.partType || '').toLowerCase();
      const answerType = normalizedType.includes('num') || normalizedType.includes('number')
        ? 'NUMERIC'
        : normalizedType.includes('short') || normalizedType.includes('text')
          ? 'TEXT'
          : 'BOOLEAN';
      return { ...part, content: part.partContent, answerType };
    }),
  };
}

function hydrateAnswers(savedAnswers = []) {
  return Object.fromEntries(savedAnswers.map((answer) => [answer.questionId, {
    answerId: answer.answerId || null,
    shortAnswerText: answer.shortAnswerText || '',
    timeSpent: answer.timeSpent || 0,
    selectedOptions: (answer.selectedOptions || []).map((option) => option.answerId),
    parts: (answer.parts || []).map((part) => ({ ...part })),
  }]));
}

function getDraftStorageKey(id) {
  return `mathinsight_test_draft_${id}`;
}

export function answersDiffer(ans1, ans2) {
  if (!ans1 && !ans2) return false;
  if (!ans1 || !ans2) return true;
  if ((ans1.answerId || null) !== (ans2.answerId || null)) return true;
  if ((ans1.shortAnswerText || '').trim() !== (ans2.shortAnswerText || '').trim()) return true;

  const opts1 = (ans1.selectedOptions || []).slice().sort();
  const opts2 = (ans2.selectedOptions || []).slice().sort();
  if (opts1.length !== opts2.length) return true;
  for (let i = 0; i < opts1.length; i += 1) {
    if (opts1[i] !== opts2[i]) return true;
  }

  const parts1 = ans1.parts || [];
  const parts2 = ans2.parts || [];
  if (parts1.length !== parts2.length) return true;
  if (JSON.stringify(parts1) !== JSON.stringify(parts2)) return true;

  return false;
}

function getLocalDraft(sessionId) {
  try {
    const raw = localStorage.getItem(getDraftStorageKey(sessionId));
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function saveLocalDraft(sessionId, answers) {
  try {
    localStorage.setItem(getDraftStorageKey(sessionId), JSON.stringify(answers));
  } catch {
    // Ignore quota or private mode errors
  }
}

function clearLocalDraft(sessionId) {
  try {
    localStorage.removeItem(getDraftStorageKey(sessionId));
  } catch {
    // Ignore
  }
}

export default function TestSession() {
  const { sessionId } = useParams();
  const navigate = useNavigate();
  const [session, setSession] = useState(null);
  const [answers, setAnswers] = useState({});
  const [currentQuestionId, setCurrentQuestionId] = useState(null);
  const [flaggedIds, setFlaggedIds] = useState(new Set());

  // Time Policy states: hasTimeLimit, remainingSeconds, elapsedSeconds
  const [hasTimeLimit, setHasTimeLimit] = useState(true);
  const [remainingSeconds, setRemainingSeconds] = useState(null);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);

  const [incidentCount, setIncidentCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [showSubmitModal, setShowSubmitModal] = useState(false);
  const [showTabWarning, setShowTabWarning] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [isOffline, setIsOffline] = useState(!navigator.onLine);
  const [showRestoredBanner, setShowRestoredBanner] = useState(false);

  // Autosave status indicator states: 'saved' | 'saving' | 'error'
  const [autoSaveStatus, setAutoSaveStatus] = useState('saved');
  const [autoSaveError, setAutoSaveError] = useState(null);

  // Track whether failed submission was 'manual' or 'timeout' for exact retry routing
  const [failedSubmitMode, setFailedSubmitMode] = useState(null);

  const answersRef = useRef(answers);
  const sessionRef = useRef(session);
  const dirtyRef = useRef(false);
  const currentRevisionRef = useRef(0);
  const lastSavedRevisionRef = useRef(0);
  const submitInFlightRef = useRef(false);
  const autoSaveTimerRef = useRef(null);
  const autoSaveQueueRef = useRef(Promise.resolve());
  const questionStartTimeRef = useRef(Date.now());
  const previousQuestionIdRef = useRef(null);
  answersRef.current = answers;
  sessionRef.current = session;

  const loadSession = useCallback(async () => {
    if (!sessionId) {
      setError('Mã phiên làm bài không hợp lệ.');
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);
    setFailedSubmitMode(null);
    try {
      const data = await getSessionContent(sessionId);
      const questions = (data.questions || []).map(adaptQuestion);
      const view = { ...data, questions };
      const persistedAnswers = hydrateAnswers(data.savedAnswers);

      const localDraft = getLocalDraft(sessionId);
      let finalAnswers = persistedAnswers;
      let hasUnsavedChanges = false;
      if (localDraft && typeof localDraft === 'object') {
        finalAnswers = { ...persistedAnswers };
        Object.entries(localDraft).forEach(([qId, localAns]) => {
          if (localAns && (localAns.answerId || localAns.shortAnswerText?.trim() || localAns.selectedOptions?.length || localAns.parts?.length)) {
            if (answersDiffer(persistedAnswers[qId], localAns)) {
              finalAnswers[qId] = { ...(finalAnswers[qId] || {}), ...localAns };
              hasUnsavedChanges = true;
            }
          }
        });
      }

      setSession(view);
      setAnswers(finalAnswers);
      answersRef.current = finalAnswers;

      if (hasUnsavedChanges) {
        dirtyRef.current = true;
        currentRevisionRef.current = 1;
        lastSavedRevisionRef.current = 0;
        setAutoSaveStatus('saving');
      } else {
        dirtyRef.current = false;
        currentRevisionRef.current = 0;
        lastSavedRevisionRef.current = 0;
        setAutoSaveStatus('saved');
        setAutoSaveError(null);
      }

      // Update time policy states from backend session content
      const isLimit = data.hasTimeLimit ?? (data.durationMinutes > 0);
      setHasTimeLimit(isLimit);
      setRemainingSeconds(isLimit ? (data.remainingSeconds ?? data.durationMinutes * 60) : null);
      setElapsedSeconds(data.elapsedSeconds ?? 0);

      setCurrentQuestionId((current) => current || questions[0]?.questionId || null);
    } catch (err) {
      setError(getTestGenErrorMessage(err, 'Không thể tải phiên làm bài. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    loadSession();
  }, [loadSession]);

  useEffect(() => () => {
    if (autoSaveTimerRef.current) clearTimeout(autoSaveTimerRef.current);
  }, []);

  const handleTimeoutSubmit = useCallback(async () => {
    // Only timed sessions can be timeout submitted (backend contract requirement)
    const isTimedSession = hasTimeLimit === true || (sessionRef.current?.durationMinutes > 0 && hasTimeLimit !== false);
    if (!sessionId || !isTimedSession || submitInFlightRef.current) return;

    if (autoSaveTimerRef.current) {
      clearTimeout(autoSaveTimerRef.current);
      autoSaveTimerRef.current = null;
    }

    submitInFlightRef.current = true;
    setSubmitting(true);
    try {
      await timeoutSubmitSession(sessionId);
      clearLocalDraft(sessionId);
      navigate(`/student/test-result/${sessionId}`);
    } catch (requestError) {
      const code = requestError.response?.data?.code;
      if (code === 'TESTING_SESSION_NOT_EXPIRED') {
        submitInFlightRef.current = false;
        setSubmitting(false);
        await loadSession();
        return;
      }
      // Session already completed (Submitted/Graded by another path) → go to result
      if (code === 'TESTING_SESSION_ALREADY_COMPLETED') {
        clearLocalDraft(sessionId);
        navigate(`/student/test-result/${sessionId}`);
        return;
      }
      if (code === 'TESTING_SESSION_NOT_IN_PROGRESS' || !requestError.response) {
        try {
          const serverState = await getSessionContent(sessionId);
          if (serverState.status === 'Submitted' || serverState.status === 'Graded' || serverState.submissionType != null) {
            clearLocalDraft(sessionId);
            navigate(`/student/test-result/${sessionId}`);
            return;
          }
        } catch {
          // Ignore
        }
      }
      setFailedSubmitMode('timeout');
      setError(getTestGenErrorMessage(requestError, 'Không thể tự động nộp bài hết giờ. Vui lòng thử lại.'));
      submitInFlightRef.current = false;
      setSubmitting(false);
    }
  }, [hasTimeLimit, loadSession, navigate, sessionId]);

  const performAutoSave = useCallback(async () => {
    if (!sessionId || sessionRef.current?.status !== 'InProgress' || !dirtyRef.current) return;
    const targetRevision = currentRevisionRef.current;
    const payload = toAutoSavePayload(answersRef.current);
    dirtyRef.current = false;
    setAutoSaveStatus('saving');

    const request = autoSaveQueueRef.current.catch(() => undefined).then(async () => {
      try {
        const result = await autoSaveAnswers(sessionId, payload);
        if (result.hasTimeLimit !== undefined) {
          setHasTimeLimit(result.hasTimeLimit);
          if (result.hasTimeLimit === false) {
            setRemainingSeconds(null);
          }
        }
        if (result.remainingSeconds !== undefined && result.hasTimeLimit !== false) {
          setRemainingSeconds(result.remainingSeconds);
        }
        if (result.elapsedSeconds != null) {
          setElapsedSeconds(result.elapsedSeconds);
        }
        saveLocalDraft(sessionId, answersRef.current);

        lastSavedRevisionRef.current = Math.max(lastSavedRevisionRef.current, targetRevision);

        // Only report 'saved' if the server has confirmed all revisions up to the latest currentRevision
        // AND there are no newer unsaved dirty changes, pending debounce timers, or submit in flight.
        if (
          !submitInFlightRef.current &&
          sessionRef.current?.status === 'InProgress' &&
          lastSavedRevisionRef.current >= currentRevisionRef.current &&
          !dirtyRef.current &&
          !autoSaveTimerRef.current
        ) {
          setAutoSaveStatus('saved');
          setAutoSaveError(null);
        }
      } catch (requestError) {
        if (requestError.response?.data?.code === 'TESTING_SESSION_EXPIRED') {
          await handleTimeoutSubmit();
          return;
        }
        dirtyRef.current = true;
        if (!submitInFlightRef.current && lastSavedRevisionRef.current < targetRevision) {
          setAutoSaveStatus('error');
          setAutoSaveError(getTestGenErrorMessage(requestError, 'Tự động lưu bài thất bại. Vui lòng kiểm tra kết nối.'));
        }
        throw requestError;
      }
    });
    autoSaveQueueRef.current = request;
    return request;
  }, [handleTimeoutSubmit, sessionId]);

  const handleManualRetryAutoSave = useCallback(() => {
    dirtyRef.current = true;
    performAutoSave().catch(() => {
      // Caught here to avoid unhandled promise rejection in browser event handler;
      // performAutoSave handles setting autoSaveStatus='error' and autoSaveError internally.
    });
  }, [performAutoSave]);

  // If session is loaded and has unsaved draft changes, sync to server
  useEffect(() => {
    if (session?.status === 'InProgress' && dirtyRef.current) {
      performAutoSave().catch(() => undefined);
    }
  }, [performAutoSave, session?.sessionId, session?.status]);

  const scheduleAutoSave = useCallback(() => {
    if (autoSaveTimerRef.current) clearTimeout(autoSaveTimerRef.current);
    setAutoSaveStatus('saving');
    autoSaveTimerRef.current = setTimeout(() => {
      autoSaveTimerRef.current = null;
      performAutoSave().catch(() => undefined);
    }, AUTO_SAVE_DEBOUNCE_MS);
  }, [performAutoSave]);

  useEffect(() => {
    if (!currentQuestionId) return;
    const previousId = previousQuestionIdRef.current;
    const now = Date.now();
    if (previousId && previousId !== currentQuestionId) {
      const elapsed = Math.max(0, Math.floor((now - questionStartTimeRef.current) / 1000));
      if (elapsed > 0) {
        setAnswers((current) => ({
          ...current,
          [previousId]: {
            ...(current[previousId] || {}),
            timeSpent: (current[previousId]?.timeSpent || 0) + elapsed,
          },
        }));
        dirtyRef.current = true;
        scheduleAutoSave();
      }
    }
    previousQuestionIdRef.current = currentQuestionId;
    questionStartTimeRef.current = now;
  }, [currentQuestionId, scheduleAutoSave]);

  useEffect(() => {
    let timer;
    const handleOnline = () => {
      setIsOffline(false);
      setShowRestoredBanner(true);
      if (dirtyRef.current) {
        performAutoSave().catch(() => undefined);
      }
      timer = setTimeout(() => {
        setShowRestoredBanner(false);
      }, 4000);
    };

    const handleOffline = () => {
      setIsOffline(true);
      setShowRestoredBanner(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      if (timer) clearTimeout(timer);
    };
  }, [performAutoSave]);

  useEffect(() => {
    if (session?.status !== 'InProgress') return undefined;
    const interval = setInterval(() => {
      if (dirtyRef.current) {
        performAutoSave().catch(() => undefined);
      }
    }, AUTO_SAVE_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [performAutoSave, session?.status]);

  const handleAnswer = useCallback((questionId, update) => {
    currentRevisionRef.current += 1;
    setAnswers((current) => {
      const next = {
        ...current,
        [questionId]: { ...(current[questionId] || {}), ...update },
      };
      answersRef.current = next;
      saveLocalDraft(sessionId, next);
      return next;
    });
    dirtyRef.current = true;
    scheduleAutoSave();
  }, [scheduleAutoSave, sessionId]);

  const toggleFlag = useCallback((questionId) => {
    setFlaggedIds((prev) => {
      const next = new Set(prev);
      if (next.has(questionId)) next.delete(questionId);
      else next.add(questionId);
      return next;
    });
  }, []);

  // Strictly enable proctoring incident tracking ONLY for timed Exam sessions (Finding 6)
  const isExamMode = session?.testFormat === 'Exam' && hasTimeLimit === true;

  // Tab-switch WARNING for ALL session types (purely UI, no server call)
  useEffect(() => {
    if (session?.status !== 'InProgress') return undefined;
    const handleVisibilityWarning = () => {
      if (document.hidden) setShowTabWarning(true);
    };
    document.addEventListener('visibilitychange', handleVisibilityWarning);
    return () => document.removeEventListener('visibilitychange', handleVisibilityWarning);
  }, [session?.status]);

  useEffect(() => {
    if (!sessionId || !isExamMode || session?.status !== 'InProgress') return undefined;
    const handleVisibility = async () => {
      if (!document.hidden || submitInFlightRef.current) return;
      try {
        const result = await recordIncident(sessionId, 'TAB_SWITCH');
        setIncidentCount(result.totalIncidents);
        if (result.forceSubmitted) {
          clearLocalDraft(sessionId);
          navigate(`/student/test-result/${sessionId}`);
        }
      } catch {
        // Incident logging failure must not block the test UI.
      }
    };
    document.addEventListener('visibilitychange', handleVisibility);
    return () => document.removeEventListener('visibilitychange', handleVisibility);
  }, [isExamMode, navigate, session?.status, sessionId]);

  const questions = session?.questions || [];
  const currentIndex = questions.findIndex((question) => question.questionId === currentQuestionId);
  const currentQuestion = questions[currentIndex] || questions[0];
  const answeredIds = useMemo(() => new Set(Object.entries(answers)
    .filter(([, answer]) => answer.answerId
      || answer.shortAnswerText?.trim()
      || answer.selectedOptions?.length
      || answer.parts?.some((part) => part.booleanAnswer != null
        || part.textAnswer?.trim()
        || toFiniteNumericAnswer(part.numericAnswer) !== null))
    .map(([questionId]) => questionId)), [answers]);
  const unansweredCount = questions.length - answeredIds.size;

  const handleConfirmSubmit = useCallback(async () => {
    if (!sessionId || submitInFlightRef.current) return;
    submitInFlightRef.current = true;
    setSubmitting(true);
    setError(null);

    // 1. Cancel pending debounce timer
    if (autoSaveTimerRef.current) {
      clearTimeout(autoSaveTimerRef.current);
      autoSaveTimerRef.current = null;
    }

    try {
      // 2. Wait for any in-flight auto-save or perform auto-save if dirty
      if (dirtyRef.current) {
        await performAutoSave();
      } else {
        await autoSaveQueueRef.current;
      }

      // Check if save failed or left dirty
      if (dirtyRef.current) {
        await performAutoSave();
      }

      // Ensure all student answers have been confirmed saved by server before proceeding
      if (lastSavedRevisionRef.current < currentRevisionRef.current) {
        throw new Error('Chưa thể lưu đầy đủ câu trả lời mới nhất lên máy chủ.');
      }

      // 3. Submit session
      await submitSession(sessionId);
      clearLocalDraft(sessionId);
      navigate(`/student/test-result/${sessionId}`);
    } catch (err) {
      const code = err.response?.data?.code;
      if (code === 'TESTING_SESSION_ALREADY_COMPLETED') {
        clearLocalDraft(sessionId);
        navigate(`/student/test-result/${sessionId}`);
        return;
      }

      // If submit failed due to conflict or network error, check server state
      if (code === 'TESTING_SESSION_NOT_IN_PROGRESS' || !err.response) {
        try {
          const serverState = await getSessionContent(sessionId);
          if (serverState.status === 'Submitted' || serverState.status === 'Graded' || serverState.submissionType != null) {
            clearLocalDraft(sessionId);
            navigate(`/student/test-result/${sessionId}`);
            return;
          }
        } catch {
          // Keep draft and report error
        }
      }

      setFailedSubmitMode('manual');
      setError(getTestGenErrorMessage(err, 'Nộp bài thất bại. Vui lòng thử lại.'));
      submitInFlightRef.current = false;
      setSubmitting(false);
      setShowSubmitModal(false);
    }
  }, [navigate, performAutoSave, sessionId]);

  const handleRetrySubmit = useCallback(async () => {
    setError(null);
    setSubmitting(true);
    submitInFlightRef.current = false;

    // Check server state before blindly retrying submit
    try {
      const serverState = await getSessionContent(sessionId);
      if (serverState.status === 'Submitted' || serverState.status === 'Graded' || serverState.submissionType != null) {
        clearLocalDraft(sessionId);
        navigate(`/student/test-result/${sessionId}`);
        return;
      }
    } catch (checkErr) {
      setError(getTestGenErrorMessage(checkErr, 'Không thể kết nối máy chủ để kiểm tra trạng thái bài thi. Vui lòng kiểm tra mạng và thử lại.'));
      setSubmitting(false);
      return;
    }

    if (failedSubmitMode === 'timeout') {
      await handleTimeoutSubmit();
    } else {
      await handleConfirmSubmit();
    }
  }, [failedSubmitMode, handleConfirmSubmit, handleTimeoutSubmit, navigate, sessionId]);

  if (loading) {
    return <ExamLayout><div className="flex items-center justify-center py-24"><div className="w-10 h-10 border-4 border-primary/20 border-t-primary rounded-full animate-spin" /></div></ExamLayout>;
  }

  if (error || !session) {
    return (
      <ExamLayout>
        <div className="flex items-center justify-center py-24">
          <div className="bg-pure-surface border border-whisper-border rounded-xl p-8 max-w-md text-center shadow-sm">
            <span className="material-symbols-outlined text-4xl text-deep-rose mb-3">error</span>
            <h3 className="text-lg font-bold text-on-surface mb-2">Không thể tiếp tục</h3>
            <p className="text-sm text-on-surface-variant mb-4">{error || 'Lỗi không xác định.'}</p>
            <div className="flex items-center justify-center gap-3">
              {session && (
                <>
                  <button
                    onClick={handleRetrySubmit}
                    disabled={submitting}
                    className="px-6 py-2 bg-primary text-white rounded-lg text-sm font-bold disabled:opacity-50 min-h-[44px]"
                  >
                    {submitting ? 'Đang gửi...' : 'Thử lại'}
                  </button>
                  <button
                    onClick={() => {
                      setError(null);
                      setSubmitting(false);
                      submitInFlightRef.current = false;
                    }}
                    disabled={submitting}
                    className="px-6 py-2 border border-whisper-border text-on-surface rounded-lg text-sm font-bold hover:bg-surface-container-low min-h-[44px]"
                  >
                    Quay lại bài làm
                  </button>
                </>
              )}
              <button
                onClick={() => navigate('/student/test')}
                className="px-6 py-2 border border-whisper-border text-on-surface rounded-lg text-sm font-bold hover:bg-surface-container-low min-h-[44px]"
              >
                Quay lại chọn đề
              </button>
            </div>
          </div>
        </div>
      </ExamLayout>
    );
  }

  const durationText = !hasTimeLimit || session.durationMinutes === 0
    ? 'Không giới hạn'
    : `${session.durationMinutes} phút`;

  return (
    <ExamLayout>
      <div className="max-w-screen-xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {isOffline && (
          <div className="bg-amber-500 text-white px-4 py-3 rounded-xl shadow-md flex items-center justify-between gap-3 text-sm font-semibold animate-pulse">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-xl">wifi_off</span>
              <span>Mất kết nối Internet! Bài làm của bạn đang được tự động lưu an toàn trên thiết bị này. Vui lòng không đóng hoặc tải lại trang (F5) cho đến khi có mạng trở lại.</span>
            </div>
            <span className="bg-amber-700/50 px-3 py-1 rounded-lg text-xs font-bold whitespace-nowrap">Ngoại tuyến</span>
          </div>
        )}

        {showRestoredBanner && !isOffline && (
          <div className="bg-emerald-600 text-white px-4 py-3 rounded-xl shadow-md flex items-center justify-between gap-3 text-sm font-semibold transition-all">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-xl">wifi</span>
              <span>Đã khôi phục kết nối Internet! Hệ thống đang tự động đồng bộ và lưu bài làm của bạn lên máy chủ.</span>
            </div>
            <span className="bg-emerald-800/40 px-3 py-1 rounded-lg text-xs font-bold whitespace-nowrap">Đã có mạng</span>
          </div>
        )}

        {autoSaveStatus === 'error' && !isOffline && (
          <div className="bg-rose-50 border border-rose-200 text-rose-800 px-4 py-3 rounded-xl shadow-sm flex items-center justify-between gap-3 text-sm font-semibold" data-testid="autosave-error-banner">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-xl text-rose-600">cloud_off</span>
              <span>
                {autoSaveError || 'Chưa thể tự động lưu câu trả lời lên máy chủ. Bài làm hiện được lưu an toàn trên trình duyệt của bạn.'}
              </span>
            </div>
            <button
              type="button"
              onClick={handleManualRetryAutoSave}
              className="bg-rose-600 hover:bg-rose-700 text-white px-3 py-1.5 rounded-lg text-xs font-bold whitespace-nowrap transition-colors"
            >
              Thử lưu lại
            </button>
          </div>
        )}

        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-on-surface">{session.testName}</h2>
            <div className="flex items-center gap-3 mt-1 text-sm text-on-surface-variant">
              <span>{questions.length} câu hỏi · {durationText}</span>
              <span className="text-whisper-border">|</span>
              {autoSaveStatus === 'saving' && (
                <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-primary" data-testid="autosave-status-saving">
                  <span className="material-symbols-outlined text-[15px] animate-spin">sync</span>
                  <span>Đang lưu...</span>
                </span>
              )}
              {autoSaveStatus === 'saved' && (
                <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-600" data-testid="autosave-status-saved">
                  <span className="material-symbols-outlined text-[15px]">cloud_done</span>
                  <span>Đã lưu</span>
                </span>
              )}
              {autoSaveStatus === 'error' && (
                <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-rose-600" data-testid="autosave-status-error" title={autoSaveError || 'Lưu bài thất bại'}>
                  <span className="material-symbols-outlined text-[15px]">cloud_off</span>
                  <span>Lưu bài thất bại</span>
                  <button
                    type="button"
                    onClick={handleManualRetryAutoSave}
                    className="ml-1 text-xs underline font-bold hover:text-rose-700"
                  >
                    Thử lại
                  </button>
                </span>
              )}
            </div>
          </div>
          <div className="flex items-center gap-4">
            {isExamMode && incidentCount > 0 && <span className="px-3 py-1.5 rounded-lg text-xs font-bold bg-amber-100 text-amber-700">{incidentCount}/5 vi phạm</span>}
            {isExamMode && <span className="px-3 py-1.5 rounded-lg text-xs font-bold bg-red-50 text-red-600 border border-red-200">Giám sát bật</span>}
            <SessionTimer
              hasTimeLimit={hasTimeLimit}
              remainingSeconds={remainingSeconds}
              elapsedSeconds={elapsedSeconds}
              onTimeUp={handleTimeoutSubmit}
            />
            <button onClick={() => setShowSubmitModal(true)} disabled={submitting} className="px-6 py-2.5 bg-primary text-white rounded-xl text-sm font-bold disabled:opacity-50 min-h-[44px]">Nộp bài</button>
          </div>
        </div>

        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-12 lg:col-span-8 xl:col-span-9">
            <QuestionPanel question={currentQuestion} answer={answers[currentQuestion?.questionId]} onAnswer={handleAnswer} totalQuestions={questions.length} />
            <div className="flex items-center justify-between mt-4">
              <button onClick={() => setCurrentQuestionId(questions[currentIndex - 1]?.questionId)} disabled={currentIndex <= 0} className="px-5 py-2.5 rounded-xl border border-whisper-border text-sm font-bold disabled:opacity-30 min-h-[44px]">Câu trước</button>
              <button
                onClick={() => toggleFlag(currentQuestion?.questionId)}
                title={flaggedIds.has(currentQuestion?.questionId) ? 'Bỏ đánh dấu phân vân' : 'Đánh dấu phân vân'}
                className={`flex items-center gap-1.5 px-4 py-2.5 rounded-xl border text-sm font-bold min-h-[44px] transition-all ${flaggedIds.has(currentQuestion?.questionId)
                    ? 'bg-amber-400/15 border-amber-400 text-amber-600 hover:bg-amber-400/25'
                    : 'border-whisper-border text-on-surface-variant hover:bg-amber-50 hover:border-amber-300 hover:text-amber-600'
                  }`}
              >
                <span className="material-symbols-outlined text-[18px]">
                  {flaggedIds.has(currentQuestion?.questionId) ? 'flag' : 'flag'}
                </span>
                {flaggedIds.has(currentQuestion?.questionId) ? 'Bỏ đánh dấu' : 'Đánh dấu'}
              </button>
              <button onClick={() => setCurrentQuestionId(questions[currentIndex + 1]?.questionId)} disabled={currentIndex >= questions.length - 1} className="px-5 py-2.5 rounded-xl border border-whisper-border text-sm font-bold disabled:opacity-30 min-h-[44px]">Câu tiếp</button>
            </div>
          </div>
          <div className="col-span-12 lg:col-span-4 xl:col-span-3">
            <div className="sticky top-6"><QuestionNav questions={questions} answeredIds={answeredIds} flaggedIds={flaggedIds} currentQuestionId={currentQuestionId} onSelect={setCurrentQuestionId} /></div>
          </div>
        </div>
      </div>

      <SubmitConfirmModal isOpen={showSubmitModal} unansweredCount={unansweredCount} totalQuestions={questions.length} onConfirm={handleConfirmSubmit} onCancel={() => setShowSubmitModal(false)} submitting={submitting} />

      <TabSwitchWarningModal
        isOpen={showTabWarning}
        isExamMode={isExamMode}
        incidentCount={incidentCount}
        maxIncidents={5}
        onClose={() => setShowTabWarning(false)}
      />
    </ExamLayout>
  );
}
