import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { startSession } from "../../services/testingApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";

export default function StartTestDialog({ isOpen, onClose, test }) {
  const navigate = useNavigate();

  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [resumeSessionId, setResumeSessionId] = useState(null);

  const submittingRef = useRef(false);

  useEffect(() => {
    if (isOpen) {
      setSubmitting(false);
      setErrorMessage("");
      setResumeSessionId(null);
      submittingRef.current = false;
    }
  }, [isOpen, test]);

  if (!test) return null;

  const handleStartSession = async () => {
    // If we already detected an in-progress session, resume it
    if (resumeSessionId) {
      onClose();
      navigate(`/student/test/${resumeSessionId}`);
      return;
    }

    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setErrorMessage("");

    try {
      const data = await startSession(test.testId);
      const sessionId = data?.sessionId || data?.id;

      if (sessionId) {
        onClose();
        navigate(`/student/test/${sessionId}`);
      } else {
        throw new Error("Không nhận được SessionID từ máy chủ.");
      }
    } catch (err) {
      submittingRef.current = false;
      setSubmitting(false);

      const errCode = err.response?.data?.code;

      if (errCode === "TESTING_SESSION_ALREADY_IN_PROGRESS") {
        const existingSessionId = err.response?.data?.existingSessionId;
        if (existingSessionId && typeof existingSessionId === "string") {
          setResumeSessionId(existingSessionId);
        }
        setErrorMessage("Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này.");
        return;
      }

      if (errCode === "TESTING_TEST_ACCESS_DENIED") {
        setErrorMessage("Bạn không thể bắt đầu đề này.");
        return;
      }

      setErrorMessage(getTestGenErrorMessage(err, "Không thể bắt đầu làm bài thi. Vui lòng thử lại."));
    }
  };

  const handleSafeClose = () => {
    if (!submitting) {
      onClose();
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={handleSafeClose} isCloseDisabled={submitting}>
      <div className="flex flex-col h-full select-none">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">quiz</span>
            {resumeSessionId ? "Tiếp tục bài làm" : "Xác nhận làm bài thi"}
          </DialogTitle>
          <DialogDescription>
            {test.testName}
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 select-text">
            {/* Test info grid */}
            <div className="grid grid-cols-2 gap-3 bg-surface-container-low p-4 rounded-xl border border-whisper-border">
              <div>
                <span className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Khối lớp</span>
                <span className="block text-sm font-bold text-on-surface mt-0.5">Khối {test.grade}</span>
              </div>
              <div>
                <span className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Thời gian</span>
                <span className="block text-sm font-bold text-on-surface mt-0.5">{test.durationMinutes === 0 ? "Không giới hạn" : `${test.durationMinutes} phút`}</span>
              </div>
              <div>
                <span className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Số câu hỏi</span>
                <span className="block text-sm font-bold text-on-surface font-mono mt-0.5">{test.totalQuestions} câu</span>
              </div>
              <div>
                <span className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Tổng điểm</span>
                <span className="block text-sm font-bold text-primary font-mono mt-0.5">{test.maxScore} điểm</span>
              </div>
            </div>

            {/* Resume Session Banner */}
            {resumeSessionId && (
              <div className="p-3.5 bg-amber-warning/10 border border-amber-warning/20 rounded-xl text-on-surface flex items-start gap-2.5 text-xs">
                <span className="material-symbols-outlined text-amber-warning text-[20px] shrink-0 mt-0.5">pending_actions</span>
                <div>
                  <strong className="block font-bold text-amber-warning mb-0.5">Phiên làm bài chưa hoàn tất</strong>
                  <p className="text-on-surface-variant leading-relaxed">
                    Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này. Hãy chọn "Tiếp tục bài đang làm" để tiếp tục tiến trình của bạn.
                  </p>
                </div>
              </div>
            )}

            {/* General Error Banner */}
            {errorMessage && !resumeSessionId && (
              <div role="alert" className="p-3.5 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-start gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
                <p className="flex-1 leading-relaxed">{errorMessage}</p>
              </div>
            )}
          </div>
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={handleSafeClose}
            className="min-h-[44px]"
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="primary"
            disabled={submitting}
            onClick={handleStartSession}
            className="min-h-[44px] min-w-[140px] font-bold"
          >
            {submitting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang bắt đầu...</span>
              </div>
            ) : resumeSessionId ? (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                <span>Tiếp tục bài đang làm</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                <span>Bắt đầu làm bài</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
