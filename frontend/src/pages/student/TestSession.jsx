import React, { useState, useEffect, useRef, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import StudentLayout from "./StudentLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import LatexPreview from "../../components/expert/LatexPreview";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { cn } from "../../utils/cn";

function getPartCategory(partType) {
  if (!partType) return "TrueFalse";
  const norm = partType.toLowerCase();
  if (norm.includes("short") || norm.includes("text")) return "Text";
  if (norm.includes("num") || norm.includes("number")) return "Numeric";
  return "TrueFalse";
}

function formatRemainingTime(totalSeconds) {
  if (totalSeconds <= 0) return "00:00";
  const m = Math.floor(totalSeconds / 60);
  const s = totalSeconds % 60;
  if (m >= 60) {
    const h = Math.floor(m / 60);
    const remM = m % 60;
    return `${h.toString().padStart(2, "0")}:${remM.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  }
  return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
}

export default function TestSession({ sessionId: propSessionId }) {
  const params = useParams();
  const activeSessionId = propSessionId || params.sessionId;
  const navigate = useNavigate();

  // Data states
  const [sessionData, setSessionData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState("");

  // Student answer state: { [questionId]: { selectedAnswerId, shortAnswerText, selectedOptions: [], parts: { [partId]: { booleanAnswer, textAnswer, numericAnswer } } } }
  const [userAnswers, setUserAnswers] = useState({});
  const userAnswersRef = useRef(userAnswers);
  userAnswersRef.current = userAnswers;

  // Proctoring & Timer states
  const [tabSwitches, setTabSwitches] = useState(0);
  const [remainingSeconds, setRemainingSeconds] = useState(null);
  const [lastSavedTime, setLastSavedTime] = useState(null);

  // Submit modal & request states
  const [isSubmitOpen, setIsSubmitOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");

  const submitInFlightRef = useRef(false);
  const incidentInFlightRef = useRef(false);
  const autoSaveDebounceRef = useRef(null);
  const autoSaveQueueRef = useRef(Promise.resolve());

  // Helper to convert userAnswers state into backend AutoSaveAnswerDto array
  const buildAutoSavePayload = useCallback((answersMap, questionsList) => {
    const questionLookup = new Map(questionsList.map((question) => [question.questionId, question]));

    return Object.entries(answersMap).map(([questionId, uAns]) => {
      const q = questionLookup.get(questionId);
      if (!q) return null;

      let answerId = null;
      let shortAnswerText = null;
      let selectedOptions = null;
      let parts = null;

      if (q.questionType === "SingleChoice" || q.questionType === "TrueFalse") {
        answerId = uAns.selectedAnswerId || null;
      } else if (q.questionType === "MultipleChoice") {
        selectedOptions = (uAns.selectedOptions || []).map((id) => ({ answerId: id }));
      } else if (q.questionType === "ShortAnswer") {
        shortAnswerText = uAns.shortAnswerText?.trim() || null;
      } else if (q.questionType === "Composite" && q.parts) {
        const partsAns = uAns.parts || {};
        parts = Object.entries(partsAns).map(([partId, pAns]) => {
          let numericVal = null;
          if (pAns.numericAnswer !== undefined && pAns.numericAnswer !== null && pAns.numericAnswer !== "") {
            const parsed = parseFloat(pAns.numericAnswer);
            if (!isNaN(parsed)) numericVal = parsed;
          }

          return {
            partId,
            booleanAnswer: pAns.booleanAnswer !== undefined ? pAns.booleanAnswer : null,
            textAnswer: pAns.textAnswer?.trim() || null,
            numericAnswer: numericVal
          };
        });
      }

      return {
        questionId: q.questionId,
        answerId,
        shortAnswerText,
        timeSpent: uAns.timeSpent || 0,
        selectedOptions,
        parts
      };
    }).filter(Boolean);
  }, []);

  // Execute AutoSave to backend
  const executeAutoSave = useCallback(async (answersMap) => {
    if (!activeSessionId || sessionData?.status !== "InProgress") return;

    const payload = buildAutoSavePayload(answersMap, sessionData?.questions || []);
    const request = autoSaveQueueRef.current.catch(() => undefined).then(async () => {
      const res = await testGeneratorApi.autoSaveSession(activeSessionId, payload);
      if (res.data?.remainingSeconds !== undefined) {
        setRemainingSeconds(res.data.remainingSeconds);
      }
      setLastSavedTime(new Date());
    });

    autoSaveQueueRef.current = request;
    return request;
  }, [activeSessionId, sessionData, buildAutoSavePayload]);

  const clearScheduledAutoSave = useCallback(() => {
    if (autoSaveDebounceRef.current) {
      clearTimeout(autoSaveDebounceRef.current);
      autoSaveDebounceRef.current = null;
    }
  }, []);

  // Schedule debounced AutoSave when userAnswers change
  const scheduleAutoSave = useCallback((newAnswers) => {
    clearScheduledAutoSave();
    autoSaveDebounceRef.current = setTimeout(() => {
      autoSaveDebounceRef.current = null;
      executeAutoSave(newAnswers).catch((err) => {
        console.error("Lỗi tự động lưu đáp án:", err);
      });
    }, 1200);
  }, [clearScheduledAutoSave, executeAutoSave]);

  // 1. Fetch Session Content
  const fetchSession = async () => {
    if (!activeSessionId) {
      setPageError("Mã phiên làm bài không hợp lệ.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setPageError("");
    try {
      const res = await testGeneratorApi.getSessionContent(activeSessionId);
      const data = res.data;
      setSessionData(data);

      // An empty auto-save is a safe heartbeat that returns authoritative remaining time.
      if (data.status === "InProgress" && data.durationMinutes) {
        try {
          const timerRes = await testGeneratorApi.autoSaveSession(activeSessionId, []);
          setRemainingSeconds(timerRes.data?.remainingSeconds ?? data.durationMinutes * 60);
        } catch {
          setRemainingSeconds(data.durationMinutes * 60);
        }
      }
    } catch (err) {
      setPageError(getTestGenErrorMessage(err, "Không thể tải phiên làm bài thi. Vui lòng kiểm tra lại."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSession();
  }, [activeSessionId]);

  useEffect(() => clearScheduledAutoSave, [clearScheduledAutoSave]);

  // 2. Countdown Timer
  useEffect(() => {
    if (sessionData?.status !== "InProgress" || remainingSeconds === null) return;

    if (remainingSeconds <= 0) {
      // Time is up -> Auto submit
      handleTimeoutAutoSubmit();
      return;
    }

    const timerId = setInterval(() => {
      setRemainingSeconds((prev) => (prev !== null && prev > 0 ? prev - 1 : 0));
    }, 1000);

    return () => clearInterval(timerId);
  }, [remainingSeconds, sessionData?.status]);

  // Handle Timeout Auto-Submit
  const handleTimeoutAutoSubmit = async () => {
    if (submitInFlightRef.current) return;
    submitInFlightRef.current = true;
    setSubmitting(true);

    try {
      clearScheduledAutoSave();
      try {
        await executeAutoSave(userAnswersRef.current);
      } catch (err) {
        console.error("Timeout auto-save error:", err);
      }

      await testGeneratorApi.submitSession(activeSessionId);
      alert("Thời gian làm bài đã hết! Hệ thống đã tự động nộp bài thi của bạn.");
      navigate(`/student/test-result/${activeSessionId}`);
    } catch (err) {
      console.error("Timeout submit error:", err);
      alert("Không thể tự động nộp bài do lỗi kết nối. Vui lòng thử nộp bài lại.");
      submitInFlightRef.current = false;
      setSubmitting(false);
    }
  };

  // 3. Proctoring Listeners (TAB_SWITCH & FOCUS_LOSS)
  useEffect(() => {
    if (!activeSessionId || loading || pageError || sessionData?.status !== "InProgress") return;

    const recordIncidentCall = async (typeLabel) => {
      if (incidentInFlightRef.current) return;
      incidentInFlightRef.current = true;

      try {
        clearScheduledAutoSave();
        try {
          await executeAutoSave(userAnswersRef.current);
        } catch (err) {
          console.error("Lỗi lưu đáp án trước khi ghi nhận vi phạm:", err);
        }

        const res = await testGeneratorApi.recordIncident(activeSessionId, typeLabel);
        const data = res.data || {};
        const totalCount = data.totalIncidents || (tabSwitches + 1);
        setTabSwitches(totalCount);

        if (data.forceSubmitted) {
          alert("CẢNH BÁO: Phiên làm bài của bạn đã tự động nộp do vi phạm an toàn thi cử (rời màn hình quá 5 lần).");
          navigate(`/student/test-result/${activeSessionId}`);
          return;
        }

        alert(`CẢNH BÁO AN TOÀN THI CỬ: Bạn vừa rời màn hình làm bài. Đây là lần vi phạm thứ ${totalCount}/5.`);
      } catch (err) {
        console.error("Lỗi ghi nhận vi phạm thi cử:", err);
      } finally {
        incidentInFlightRef.current = false;
      }
    };

    const handleVisibilityChange = () => {
      if (document.hidden) {
        recordIncidentCall("TAB_SWITCH");
      }
    };

    const handleWindowBlur = () => {
      recordIncidentCall("FOCUS_LOSS");
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);
    window.addEventListener("blur", handleWindowBlur);

    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      window.removeEventListener("blur", handleWindowBlur);
    };
  }, [activeSessionId, loading, pageError, sessionData?.status, tabSwitches, navigate, executeAutoSave, clearScheduledAutoSave]);

  // Answer Handlers
  const handleSingleChoiceSelect = (questionId, answerId) => {
    setUserAnswers((prev) => {
      const next = {
        ...prev,
        [questionId]: {
          ...prev[questionId],
          selectedAnswerId: answerId
        }
      };
      scheduleAutoSave(next);
      return next;
    });
  };

  const handleMultipleChoiceToggle = (questionId, answerId) => {
    setUserAnswers((prev) => {
      const existingOptions = prev[questionId]?.selectedOptions || [];
      const hasOption = existingOptions.includes(answerId);
      const newOptions = hasOption
        ? existingOptions.filter((id) => id !== answerId)
        : [...existingOptions, answerId];

      const next = {
        ...prev,
        [questionId]: {
          ...prev[questionId],
          selectedOptions: newOptions
        }
      };
      scheduleAutoSave(next);
      return next;
    });
  };

  const handleShortAnswerChange = (questionId, text) => {
    setUserAnswers((prev) => {
      const next = {
        ...prev,
        [questionId]: {
          ...prev[questionId],
          shortAnswerText: text
        }
      };
      scheduleAutoSave(next);
      return next;
    });
  };

  const handleCompositePartChange = (questionId, partId, field, value) => {
    setUserAnswers((prev) => {
      const existingParts = prev[questionId]?.parts || {};
      const next = {
        ...prev,
        [questionId]: {
          ...prev[questionId],
          parts: {
            ...existingParts,
            [partId]: {
              ...existingParts[partId],
              [field]: value
            }
          }
        }
      };
      scheduleAutoSave(next);
      return next;
    });
  };

  // Submit Session Execution
  const handleSubmitSession = async () => {
    if (!activeSessionId) return;

    if (submitInFlightRef.current) return;
    submitInFlightRef.current = true;
    setSubmitting(true);
    setSubmitError("");

    try {
      // 1. AutoSave remaining answers immediately before submitting
      clearScheduledAutoSave();
      await executeAutoSave(userAnswersRef.current);

      // 2. Submit session to backend (NO payload)
      await testGeneratorApi.submitSession(activeSessionId);
      setIsSubmitOpen(false);
      navigate(`/student/test-result/${activeSessionId}`);
    } catch (err) {
      setSubmitError(getTestGenErrorMessage(err, "Không thể nộp bài thi. Vui lòng kiểm tra lại kết nối và thử lại."));
      submitInFlightRef.current = false;
      setSubmitting(false);
    }
  };

  // Invalid / Missing Session State
  if (!activeSessionId || pageError) {
    return (
      <StudentLayout>
        <div className="p-gutter flex flex-col items-center justify-center min-h-[400px] text-center max-w-md mx-auto gap-4 select-text">
          <span className="material-symbols-outlined text-[48px] text-error">assignment_late</span>
          <h2 className="text-lg font-bold text-on-surface">Không thể tải bài thi</h2>
          <p className="text-xs text-on-surface-variant">{pageError || "Mã phiên làm bài không tồn tại hoặc đã hết hạn."}</p>
          <Button variant="primary" onClick={() => navigate("/student/test")}>
            Về danh sách đề thi
          </Button>
        </div>
      </StudentLayout>
    );
  }

  // Loading State
  if (loading) {
    return (
      <StudentLayout>
        <div className="p-gutter flex flex-col items-center justify-center min-h-[400px]">
          <div className="w-10 h-10 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p className="mt-4 text-sm text-on-surface-variant font-semibold">Đang chuẩn bị đề thi...</p>
        </div>
      </StudentLayout>
    );
  }

  const isReadOnly = sessionData?.status !== "InProgress";
  const questions = sessionData?.questions || [];
  const answeredCount = Object.keys(userAnswers).filter((qId) => {
    const ans = userAnswers[qId];
    if (!ans) return false;
    return (
      ans.selectedAnswerId ||
      ans.shortAnswerText?.trim() ||
      (ans.selectedOptions && ans.selectedOptions.length > 0) ||
      (ans.parts && Object.keys(ans.parts).length > 0)
    );
  }).length;

  return (
    <StudentLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">
        {/* Header Bar */}
        <DashboardPageHeader
          title={sessionData?.testName || "Phiên làm bài thi"}
          subtitle={`Tổng điểm: ${sessionData?.maxScore || 10} điểm | Trạng thái: ${sessionData?.status || "InProgress"}`}
        >
          <div className="flex flex-wrap items-center gap-3">
            {/* Timer Countdown Badge */}
            {!isReadOnly && remainingSeconds !== null && (
              <div className={cn(
                "px-3.5 py-1.5 rounded-lg border font-mono font-extrabold text-xs flex items-center gap-1.5 shadow-sm",
                remainingSeconds <= 300
                  ? "bg-error/15 border-error/30 text-error animate-pulse"
                  : "bg-primary/10 border-primary/20 text-primary"
              )}>
                <span className="material-symbols-outlined text-[18px]">timer</span>
                <span>Còn lại: {formatRemainingTime(remainingSeconds)}</span>
              </div>
            )}

            {/* Proctoring Warning Badge */}
            <div className={cn(
              "px-3 py-1.5 rounded-lg border text-xs font-bold flex items-center gap-1.5",
              tabSwitches > 0 ? "bg-error/10 border-error/20 text-error" : "bg-surface-container-low border-whisper-border text-on-surface-variant"
            )}>
              <span className="material-symbols-outlined text-[18px]">visibility</span>
              <span>Rời tab: {tabSwitches}/5</span>
            </div>

            {/* Answer Progress & Saved Time Badge */}
            <div className="px-3 py-1.5 rounded-lg border border-whisper-border bg-surface-container-low text-xs font-bold text-on-surface font-mono flex items-center gap-2">
              <span>Đã làm: {answeredCount} / {questions.length} câu</span>
              {lastSavedTime && (
                <span className="text-[10px] text-emerald-success font-normal">
                  (Đã lưu {lastSavedTime.toLocaleTimeString("vi-VN")})
                </span>
              )}
            </div>

            {/* Submit Button */}
            {!isReadOnly ? (
              <Button
                variant="primary"
                disabled={submitting}
                onClick={() => setIsSubmitOpen(true)}
                className="font-bold min-h-[40px]"
              >
                <span className="material-symbols-outlined text-[18px] mr-1.5">send</span>
                Nộp bài thi
              </Button>
            ) : (
              <span className="px-3.5 py-1.5 rounded-lg bg-surface-container-high border border-whisper-border text-on-surface-variant font-bold text-xs">
                Bài thi đã nộp ({sessionData?.status})
              </span>
            )}
          </div>
        </DashboardPageHeader>

        {/* Status Notice Banner if Not InProgress */}
        {isReadOnly && (
          <div className="p-4 bg-amber-warning/10 border border-amber-warning/20 rounded-xl text-on-surface flex items-center gap-3 select-text text-xs">
            <span className="material-symbols-outlined text-amber-warning text-[22px] shrink-0">info</span>
            <div>
              <strong className="block text-sm font-bold text-amber-warning mb-0.5">Phiên làm bài đã kết thúc</strong>
              <p className="text-on-surface-variant">
                Phiên làm bài này có trạng thái <strong className="text-on-surface">"{sessionData?.status}"</strong> và không còn nhận các thao tác chỉnh sửa đáp án hoặc nộp lại.
              </p>
            </div>
          </div>
        )}

        {/* Question Cards List */}
        <div className="flex flex-col gap-6 select-text">
          {questions.map((q, idx) => {
            const qNum = q.questionNo || idx + 1;
            const qAns = userAnswers[q.questionId] || {};

            return (
              <div
                key={q.questionId || idx}
                id={`q-${qNum}`}
                className="bg-pure-surface border border-whisper-border rounded-xl p-5 md:p-6 shadow-sm flex flex-col gap-4 scroll-mt-20"
              >
                {/* Question Header */}
                <div className="flex items-center justify-between border-b border-whisper-border pb-3">
                  <div className="flex items-center gap-2">
                    <span className="bg-primary text-on-primary text-xs font-black px-3 py-1 rounded-lg font-mono">
                      Câu {qNum}
                    </span>
                    <span className="bg-surface-container-low border border-whisper-border text-on-surface-variant text-[11px] font-bold px-2.5 py-0.5 rounded">
                      {q.questionType}
                    </span>
                  </div>
                  <span className="text-xs text-on-surface-variant font-mono">
                    Điểm: <strong className="text-primary">{q.maxPoints} đ</strong>
                  </span>
                </div>

                {/* Question Content */}
                <div className="space-y-3">
                  <LatexPreview content={q.questionContent} />

                  {q.pictureUrl && (
                    <div className="my-3 max-w-lg rounded-xl overflow-hidden border border-whisper-border bg-surface-container-low p-2">
                      <img
                        src={q.pictureUrl}
                        alt={`Hình ảnh minh họa cho câu ${qNum}`}
                        className="max-w-full h-auto object-contain mx-auto rounded-lg max-h-80"
                        loading="lazy"
                      />
                    </div>
                  )}
                </div>

                {/* Option / Answer Inputs depending on QuestionType */}
                {/* 1. Single Choice & Standalone TrueFalse */}
                {(q.questionType === "SingleChoice" || q.questionType === "TrueFalse") && q.answerOptions && (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mt-2">
                    {q.answerOptions.map((opt, optIdx) => {
                      const isSelected = qAns.selectedAnswerId === opt.answerId;
                      const optionLabel = String.fromCharCode(65 + optIdx);
                      return (
                        <button
                          key={opt.answerId || optIdx}
                          type="button"
                          disabled={isReadOnly}
                          onClick={() => handleSingleChoiceSelect(q.questionId, opt.answerId)}
                          className={cn(
                            "p-3.5 rounded-xl border text-left flex items-start gap-3 transition-all cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed",
                            isSelected
                              ? "bg-primary/10 border-primary ring-1 ring-primary shadow-sm"
                              : "bg-surface-container-lowest border-whisper-border hover:bg-surface-container-low"
                          )}
                        >
                          <span
                            className={cn(
                              "w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0 font-mono mt-0.5",
                              isSelected
                                ? "bg-primary text-on-primary"
                                : "bg-surface-container-high text-on-surface-variant border border-whisper-border"
                            )}
                          >
                            {optionLabel}
                          </span>
                          <div className="flex-1 min-w-0">
                            <LatexPreview content={opt.answerContent} />
                          </div>
                        </button>
                      );
                    })}
                  </div>
                )}

                {/* 2. Multiple Choice */}
                {q.questionType === "MultipleChoice" && q.answerOptions && (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mt-2">
                    {q.answerOptions.map((opt, optIdx) => {
                      const selectedOpts = qAns.selectedOptions || [];
                      const isSelected = selectedOpts.includes(opt.answerId);
                      const optionLabel = String.fromCharCode(65 + optIdx);
                      return (
                        <button
                          key={opt.answerId || optIdx}
                          type="button"
                          disabled={isReadOnly}
                          onClick={() => handleMultipleChoiceToggle(q.questionId, opt.answerId)}
                          className={cn(
                            "p-3.5 rounded-xl border text-left flex items-start gap-3 transition-all cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed",
                            isSelected
                              ? "bg-primary/10 border-primary ring-1 ring-primary shadow-sm"
                              : "bg-surface-container-lowest border-whisper-border hover:bg-surface-container-low"
                          )}
                        >
                          <div
                            className={cn(
                              "w-5 h-5 rounded border flex items-center justify-center shrink-0 mt-0.5",
                              isSelected ? "bg-primary border-primary text-on-primary" : "border-outline-variant bg-pure-surface"
                            )}
                          >
                            {isSelected && <span className="material-symbols-outlined text-[14px]">check</span>}
                          </div>
                          <span className="font-bold text-xs font-mono text-on-surface mt-0.5">{optionLabel}.</span>
                          <div className="flex-1 min-w-0">
                            <LatexPreview content={opt.answerContent} />
                          </div>
                        </button>
                      );
                    })}
                  </div>
                )}

                {/* 3. Short Answer Input */}
                {q.questionType === "ShortAnswer" && (
                  <div className="mt-2 space-y-1.5">
                    <label htmlFor={`short-ans-${q.questionId}`} className="text-xs font-bold text-on-surface-variant">
                      Nhập đáp án của bạn:
                    </label>
                    <input
                      id={`short-ans-${q.questionId}`}
                      type="text"
                      disabled={isReadOnly}
                      value={qAns.shortAnswerText || ""}
                      onChange={(e) => handleShortAnswerChange(q.questionId, e.target.value)}
                      placeholder="Nhập kết quả tự luận ngắn..."
                      className="w-full h-11 p-3 bg-surface-container-low border border-whisper-border rounded-xl text-xs font-mono text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
                    />
                  </div>
                )}

                {/* 4. Composite Parts (TrueFalse, Text, Numeric) */}
                {q.questionType === "Composite" && q.parts && (
                  <div className="space-y-3 mt-2">
                    {q.parts.map((part, pIdx) => {
                      const partLabelStr = part.partLabel || `${String.fromCharCode(97 + pIdx)})`;
                      const partAns = qAns.parts?.[part.partId] || {};
                      const cat = getPartCategory(part.partType);

                      return (
                        <div
                          key={part.partId || pIdx}
                          className="p-3.5 bg-surface-container-lowest border border-whisper-border rounded-xl space-y-3"
                        >
                          <div className="flex items-start gap-2">
                            <span className="font-bold text-primary text-xs font-mono shrink-0 mt-0.5">
                              {partLabelStr}
                            </span>
                            <div className="flex-1 min-w-0">
                              <LatexPreview content={part.partContent} />
                            </div>
                          </div>

                          {/* Composite Part Inputs according to partType */}
                          {cat === "TrueFalse" && (
                            <div className="flex items-center gap-3 pt-1">
                              <span className="text-xs text-on-surface-variant font-medium">Chọn mệnh đề:</span>
                              <div className="flex gap-2">
                                <button
                                  type="button"
                                  disabled={isReadOnly}
                                  onClick={() => handleCompositePartChange(q.questionId, part.partId, "booleanAnswer", true)}
                                  className={cn(
                                    "px-4 py-1.5 rounded-lg border text-xs font-bold transition-all cursor-pointer disabled:opacity-60",
                                    partAns.booleanAnswer === true
                                      ? "bg-emerald-success text-white border-emerald-success shadow-sm"
                                      : "bg-surface-container-low border-whisper-border text-on-surface-variant hover:bg-surface-container"
                                  )}
                                >
                                  ĐÚNG
                                </button>
                                <button
                                  type="button"
                                  disabled={isReadOnly}
                                  onClick={() => handleCompositePartChange(q.questionId, part.partId, "booleanAnswer", false)}
                                  className={cn(
                                    "px-4 py-1.5 rounded-lg border text-xs font-bold transition-all cursor-pointer disabled:opacity-60",
                                    partAns.booleanAnswer === false
                                      ? "bg-error text-white border-error shadow-sm"
                                      : "bg-surface-container-low border-whisper-border text-on-surface-variant hover:bg-surface-container"
                                  )}
                                >
                                  SAI
                                </button>
                              </div>
                            </div>
                          )}

                          {cat === "Text" && (
                            <div className="pt-1 space-y-1">
                              <label htmlFor={`part-text-${part.partId}`} className="text-xs font-bold text-on-surface-variant">
                                Nhập câu trả lời:
                              </label>
                              <input
                                id={`part-text-${part.partId}`}
                                type="text"
                                disabled={isReadOnly}
                                value={partAns.textAnswer || ""}
                                onChange={(e) => handleCompositePartChange(q.questionId, part.partId, "textAnswer", e.target.value)}
                                placeholder="Nhập nội dung trả lời..."
                                className="w-full h-10 p-2.5 bg-surface-container-low border border-whisper-border rounded-xl text-xs font-mono text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
                              />
                            </div>
                          )}

                          {cat === "Numeric" && (
                            <div className="pt-1 space-y-1">
                              <label htmlFor={`part-num-${part.partId}`} className="text-xs font-bold text-on-surface-variant">
                                Nhập giá trị số:
                              </label>
                              <input
                                id={`part-num-${part.partId}`}
                                type="number"
                                step="any"
                                disabled={isReadOnly}
                                value={partAns.numericAnswer !== undefined && partAns.numericAnswer !== null ? partAns.numericAnswer : ""}
                                onChange={(e) => handleCompositePartChange(q.questionId, part.partId, "numericAnswer", e.target.value)}
                                placeholder="Nhập kết quả số..."
                                className="w-full h-10 p-2.5 bg-surface-container-low border border-whisper-border rounded-xl text-xs font-mono text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
                              />
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Submit Confirmation Dialog */}
      <Dialog isOpen={isSubmitOpen} onClose={() => !submitting && setIsSubmitOpen(false)} isCloseDisabled={submitting}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">task_alt</span>
            Xác nhận nộp bài thi
          </DialogTitle>
          <DialogDescription>
            Bạn đã hoàn thành <strong className="text-primary font-mono">{answeredCount} / {questions.length}</strong> câu hỏi.
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed select-text">
            Bạn có chắc chắn muốn nộp bài thi? Hệ thống sẽ tự động lưu lại toàn bộ các câu trả lời mới nhất của bạn và chấm điểm. Sau khi nộp, bạn không thể chỉnh sửa đáp án.
          </p>

          {submitError && (
            <div role="alert" className="mt-3 p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2 select-text">
              <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
              <span>{submitError}</span>
            </div>
          )}
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={() => setIsSubmitOpen(false)}
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="primary"
            disabled={submitting}
            onClick={handleSubmitSession}
            className="font-bold min-w-[120px]"
          >
            {submitting ? "Đang nộp..." : "Xác nhận nộp bài"}
          </Button>
        </DialogFooter>
      </Dialog>
    </StudentLayout>
  );
}
