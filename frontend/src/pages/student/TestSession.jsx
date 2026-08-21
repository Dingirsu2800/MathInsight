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

  // Track whether failed submission was 'manual' or 'timeout' for exact retry routing
  const [failedSubmitMode, setFailedSubmitMode] = useState(null);

  const answersRef = useRef(answers);
  const sessionRef = useRef(session);
  const dirtyRef = useRef(false);
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
      if (localDraft && typeof localDraft === 'object') {
        finalAnswers = { ...persistedAnswers };
        let hasUnsavedChanges = false;
        Object.entries(localDraft).forEach(([qId, localAns]) => {
          if (localAns && (localAns.answerId || localAns.shortAnswerText?.trim() || localAns.selectedOptions?.length || localAns.parts?.length)) {
            finalAnswers[qId] = { ...(finalAnswers[qId] || {}), ...localAns };
            hasUnsavedChanges = true;
          }
        });
        if (hasUnsavedChanges) {
          dirtyRef.current = true;
        }
      }

      setSession(view);
      setAnswers(finalAnswers);
      answersRef.current = finalAnswers;

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
      }
    }
    previousQuestionIdRef.current = currentQuestionId;
    questionStartTimeRef.current = now;
  }, [currentQuestionId]);

  const handleTimeoutSubmit = useCallback(async () => {
    // Only timed Exam sessions can be timeout submitted (backend contract requirement)
    const isExamTimedMode = sessionRef.current?.testFormat === 'Exam' && hasTimeLimit === true;
    if (!sessionId || !isExamTimedMode || submitInFlightRef.current) return;

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
      setFailedSubmitMode('timeout');
      setError(getTestGenErrorMessage(requestError, 'Không thể tự động nộp bài hết giờ. Vui lòng thử lại.'));
      submitInFlightRef.current = false;
      setSubmitting(false);
    }
  }, [hasTimeLimit, loadSession, navigate, sessionId]);

  const performAutoSave = useCallback(async () => {
    if (!sessionId || sessionRef.current?.status !== 'InProgress' || !dirtyRef.current) return;
    const payload = toAutoSavePayload(answersRef.current);
    dirtyRef.current = false;

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
      } catch (requestError) {
        if (requestError.response?.data?.code === 'TESTING_SESSION_EXPIRED') {
          await handleTimeoutSubmit();
          return;
        }
        dirtyRef.current = true;
        throw requestError;
      }
    });
    autoSaveQueueRef.current = request;
    return request;
  }, [handleTimeoutSubmit, sessionId]);

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
    const interval = setInterval(() => performAutoSave().catch(() => undefined), AUTO_SAVE_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [performAutoSave, session?.status]);

  const scheduleAutoSave = useCallback(() => {
    if (autoSaveTimerRef.current) clearTimeout(autoSaveTimerRef.current);
    autoSaveTimerRef.current = setTimeout(() => {
      autoSaveTimerRef.current = null;
      performAutoSave().catch(() => undefined);
    }, AUTO_SAVE_DEBOUNCE_MS);
  }, [performAutoSave]);

  const handleAnswer = useCallback((questionId, update) => {
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
    try {
      if (dirtyRef.current) await performAutoSave();
      await submitSession(sessionId);
      clearLocalDraft(sessionId);
      navigate(`/student/test-result/${sessionId}`);
    } catch (err) {
      setFailedSubmitMode('manual');
      setError(getTestGenErrorMessage(err, 'Nộp bài thất bại. Vui lòng thử lại.'));
      submitInFlightRef.current = false;
      setSubmitting(false);
      setShowSubmitModal(false);
    }
  }, [navigate, performAutoSave, sessionId]);

  const handleRetrySubmit = useCallback(async () => {
    setError(null);
    submitInFlightRef.current = false;
    if (failedSubmitMode === 'timeout') {
      await handleTimeoutSubmit();
    } else {
      await handleConfirmSubmit();
    }
  }, [failedSubmitMode, handleTimeoutSubmit, handleConfirmSubmit]);

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
                <button
                  onClick={handleRetrySubmit}
                  disabled={submitting}
                  className="px-6 py-2 bg-primary text-white rounded-lg text-sm font-bold disabled:opacity-50 min-h-[44px]"
                >
                  {submitting ? 'Đang gửi...' : 'Thử lại'}
                </button>
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

        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-on-surface">{session.testName}</h2>
            <p className="text-sm text-on-surface-variant">{questions.length} câu hỏi · {durationText}</p>
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
