import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import StudentLayout from '../../components/layout/StudentLayout';
import QuestionPanel from './test-session/QuestionPanel';
import QuestionNav from './test-session/QuestionNav';
import SessionTimer from './test-session/SessionTimer';
import SubmitConfirmModal from './test-session/SubmitConfirmModal';
import {
  startSession,
  autoSaveAnswers,
  recordIncident,
  submitSession,
} from '../../services/testingApi';

const AUTO_SAVE_INTERVAL_MS = 5 * 60 * 1000; // 5 minutes (BR-11)

/**
 * Full test-taking page.
 * Flow: testId from URL → startSession → display questions → auto-save → submit → redirect
 */
export default function TestSession() {
  const { testId } = useParams();
  const navigate = useNavigate();

  // Session state
  const [session, setSession] = useState(null);     // StartSessionResponse
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Answers: { [questionId]: { answerId?, shortAnswerText?, selectedOptions?, parts? } }
  const [answers, setAnswers] = useState({});
  const [currentQuestionId, setCurrentQuestionId] = useState(null);
  const [remainingSeconds, setRemainingSeconds] = useState(0);

  // Proctoring
  const [incidentCount, setIncidentCount] = useState(0);
  const [forceSubmitted, setForceSubmitted] = useState(false);

  // Submit flow
  const [showSubmitModal, setShowSubmitModal] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Refs for auto-save
  const answersRef = useRef(answers);
  answersRef.current = answers;
  const sessionRef = useRef(session);
  sessionRef.current = session;
  const dirtyRef = useRef(false);

  // ─── Start Session ──────────────────────────────────────────────────
  useEffect(() => {
    if (!testId) {
      setError('Không có mã bài kiểm tra.');
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    startSession(testId)
      .then((data) => {
        if (cancelled) return;
        setSession(data);
        setRemainingSeconds(data.durationMinutes * 60);
        if (data.questions?.length > 0) {
          setCurrentQuestionId(data.questions[0].questionId);
        }
      })
      .catch((err) => {
        if (cancelled) return;
        const code = err.response?.data?.code;
        if (code === 'TESTING_SESSION_ALREADY_IN_PROGRESS') {
          setError('Bạn đã có một phiên làm bài đang diễn ra cho bài kiểm tra này.');
        } else {
          setError('Không thể bắt đầu phiên làm bài. Vui lòng thử lại.');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [testId]);

  // Derived flag — Exam mode enables proctoring (BR-10), Practice does not
  const isExam = session?.testFormat === 'Exam';

  // ─── Tab Switch Detection (BR-10) — Exam only ─────────────────────
  useEffect(() => {
    if (!session || forceSubmitted || !isExam) return;

    const handleVisibility = () => {
      if (!document.hidden || !sessionRef.current) return;

      recordIncident(sessionRef.current.sessionId, 'TAB_SWITCH')
        .then((res) => {
          setIncidentCount(res.totalIncidents);
          if (res.forceSubmitted) {
            setForceSubmitted(true);
            navigate(`/student/test-result/${sessionRef.current.sessionId}`);
          }
        })
        .catch(() => { /* ignore network errors for incidents */ });
    };

    document.addEventListener('visibilitychange', handleVisibility);
    return () => document.removeEventListener('visibilitychange', handleVisibility);
  }, [session, forceSubmitted, isExam, navigate]);

  // ─── Auto-Save (BR-11): every 5 minutes + on answer change ────────
  const performAutoSave = useCallback(async () => {
    if (!sessionRef.current || !dirtyRef.current) return;
    dirtyRef.current = false;

    const answersMap = answersRef.current;
    const dtos = Object.entries(answersMap).map(([questionId, ans]) => ({
      questionId,
      answerId: ans.answerId || null,
      shortAnswerText: ans.shortAnswerText || null,
      timeSpent: 0,
      selectedOptions: ans.selectedOptions?.map((id) => ({ optionId: id })) || null,
      parts: ans.parts?.map((p) => ({
        partId: p.partId,
        booleanAnswer: p.booleanAnswer ?? null,
        textAnswer: p.textAnswer || null,
        numericAnswer: p.numericAnswer ?? null,
      })) || null,
    }));

    if (dtos.length === 0) return;

    try {
      const res = await autoSaveAnswers(sessionRef.current.sessionId, dtos);
      if (res.remainingSeconds != null) {
        setRemainingSeconds(res.remainingSeconds);
      }
    } catch {
      // Silently fail — will retry next interval
    }
  }, []);

  // Periodic auto-save
  useEffect(() => {
    if (!session) return;
    const interval = setInterval(performAutoSave, AUTO_SAVE_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [session, performAutoSave]);

  // ─── Answer Handler ────────────────────────────────────────────────
  const handleAnswer = useCallback((questionId, update) => {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: { ...(prev[questionId] || {}), ...update },
    }));
    dirtyRef.current = true;
  }, []);

  // ─── Navigation ────────────────────────────────────────────────────
  const questions = session?.questions || [];
  const currentIndex = questions.findIndex((q) => q.questionId === currentQuestionId);

  const goNext = () => {
    if (currentIndex < questions.length - 1) {
      setCurrentQuestionId(questions[currentIndex + 1].questionId);
    }
  };

  const goPrev = () => {
    if (currentIndex > 0) {
      setCurrentQuestionId(questions[currentIndex - 1].questionId);
    }
  };

  // ─── Answered IDs for nav ──────────────────────────────────────────
  const answeredIds = useMemo(() => {
    const set = new Set();
    for (const [qId, ans] of Object.entries(answers)) {
      if (
        ans.answerId ||
        ans.shortAnswerText?.trim() ||
        (ans.selectedOptions && ans.selectedOptions.length > 0) ||
        (ans.parts && ans.parts.some(
          (p) => p.booleanAnswer != null || p.textAnswer?.trim() || p.numericAnswer != null
        ))
      ) {
        set.add(qId);
      }
    }
    return set;
  }, [answers]);

  const unansweredCount = questions.length - answeredIds.size;

  // ─── Timer Expiry (BR-13) ─────────────────────────────────────────
  const handleTimeUp = useCallback(async () => {
    if (!sessionRef.current || submitting) return;
    // Auto-save remaining answers then submit
    await performAutoSave();
    try {
      await submitSession(sessionRef.current.sessionId);
    } catch { /* ignore */ }
    navigate(`/student/test-result/${sessionRef.current.sessionId}`);
  }, [navigate, performAutoSave, submitting]);

  // ─── Submit Flow ───────────────────────────────────────────────────
  const handleSubmitClick = () => setShowSubmitModal(true);

  const handleConfirmSubmit = async () => {
    if (!session) return;
    setSubmitting(true);

    // Save latest answers first
    await performAutoSave();

    try {
      await submitSession(session.sessionId);
      navigate(`/student/test-result/${session.sessionId}`);
    } catch {
      setSubmitting(false);
      setShowSubmitModal(false);
      setError('Nộp bài thất bại. Vui lòng thử lại.');
    }
  };

  // ─── Render: Loading ───────────────────────────────────────────────
  if (loading) {
    return (
      <StudentLayout>
        <div className="flex items-center justify-center py-24">
          <div className="flex flex-col items-center gap-4">
            <div className="w-10 h-10 border-4 border-primary/20 border-t-primary rounded-full animate-spin" />
            <p className="text-on-surface-variant text-sm animate-pulse">Đang tải bài kiểm tra...</p>
          </div>
        </div>
      </StudentLayout>
    );
  }

  // ─── Render: Error ─────────────────────────────────────────────────
  if (error || !session) {
    return (
      <StudentLayout>
        <div className="flex items-center justify-center py-24">
          <div className="bg-pure-surface border border-whisper-border rounded-xl p-8 max-w-md text-center shadow-sm">
            <span className="material-symbols-outlined text-4xl text-deep-rose mb-3">error</span>
            <h3 className="text-lg font-bold text-on-surface mb-2">Không thể bắt đầu</h3>
            <p className="text-sm text-on-surface-variant mb-4">{error || 'Lỗi không xác định.'}</p>
            <button
              onClick={() => navigate('/student/dashboard')}
              className="px-6 py-2 bg-primary text-white rounded-lg text-sm font-bold hover:bg-primary/90 transition-colors"
            >
              Quay về trang chủ
            </button>
          </div>
        </div>
      </StudentLayout>
    );
  }

  // ─── Render: Test Session ──────────────────────────────────────────
  const currentQuestion = questions[currentIndex] || questions[0];

  return (
    <StudentLayout>
      <div className="space-y-6">
        {/* Top bar: test info + timer + incidents + submit */}
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div>
              <h2 className="text-xl font-bold text-on-surface">
                {isExam ? 'Kiểm tra' : 'Luyện tập'}
              </h2>
              <p className="text-sm text-on-surface-variant">
                {questions.length} câu hỏi · {session.durationMinutes} phút
              </p>
            </div>
            {/* Format badge */}
            <span className={`px-3 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider ${
              isExam
                ? 'bg-tertiary-fixed text-tertiary'
                : 'bg-primary-fixed text-primary'
            }`}>
              {isExam ? 'Exam' : 'Practice'}
            </span>
          </div>

          <div className="flex items-center gap-4">
            {/* Incident counter — Exam only (BR-10) */}
            {isExam && incidentCount > 0 && (
              <div className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold ${
                incidentCount >= 4 ? 'bg-red-100 text-red-700' : 'bg-amber-100 text-amber-700'
              }`}>
                <span className="material-symbols-outlined text-sm">warning</span>
                {incidentCount}/5 vi phạm
              </div>
            )}

            {/* Exam proctoring indicator */}
            {isExam && (
              <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold bg-red-50 text-red-600 border border-red-200">
                <span className="material-symbols-outlined text-sm">shield</span>
                Giám sát bật
              </div>
            )}

            {/* Timer */}
            <SessionTimer
              remainingSeconds={remainingSeconds}
              onTimeUp={handleTimeUp}
            />

            {/* Submit button */}
            <button
              onClick={handleSubmitClick}
              className="px-6 py-2.5 bg-primary text-white rounded-xl text-sm font-bold hover:bg-primary/90 transition-colors flex items-center gap-2 shadow-sm"
            >
              <span className="material-symbols-outlined text-base">send</span>
              Nộp bài
            </button>
          </div>
        </div>

        {/* Main content: question + nav sidebar */}
        <div className="grid grid-cols-12 gap-6">
          {/* Question panel */}
          <div className="col-span-12 lg:col-span-8 xl:col-span-9">
            <QuestionPanel
              question={currentQuestion}
              answer={answers[currentQuestion?.questionId]}
              onAnswer={handleAnswer}
              totalQuestions={questions.length}
            />

            {/* Prev/Next navigation */}
            <div className="flex items-center justify-between mt-4">
              <button
                onClick={goPrev}
                disabled={currentIndex <= 0}
                className="px-5 py-2.5 rounded-xl border border-whisper-border text-sm font-bold text-on-surface-variant hover:bg-surface-container transition-colors disabled:opacity-30 disabled:cursor-not-allowed flex items-center gap-2"
              >
                <span className="material-symbols-outlined text-base">arrow_back</span>
                Câu trước
              </button>
              <button
                onClick={goNext}
                disabled={currentIndex >= questions.length - 1}
                className="px-5 py-2.5 rounded-xl border border-whisper-border text-sm font-bold text-on-surface-variant hover:bg-surface-container transition-colors disabled:opacity-30 disabled:cursor-not-allowed flex items-center gap-2"
              >
                Câu tiếp
                <span className="material-symbols-outlined text-base">arrow_forward</span>
              </button>
            </div>
          </div>

          {/* Sidebar: question nav */}
          <div className="col-span-12 lg:col-span-4 xl:col-span-3">
            <div className="sticky top-6 space-y-4">
              <QuestionNav
                questions={questions}
                answeredIds={answeredIds}
                currentQuestionId={currentQuestionId}
                onSelect={setCurrentQuestionId}
              />
            </div>
          </div>
        </div>
      </div>

      {/* Submit confirmation modal */}
      <SubmitConfirmModal
        isOpen={showSubmitModal}
        unansweredCount={unansweredCount}
        totalQuestions={questions.length}
        onConfirm={handleConfirmSubmit}
        onCancel={() => setShowSubmitModal(false)}
        submitting={submitting}
      />
    </StudentLayout>
  );
}
