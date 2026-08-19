import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { startSession } from "../../services/testingApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { cn } from "../../utils/cn";

export default function AdaptiveBlueprintExamDialog({ isOpen, onClose }) {
  const navigate = useNavigate();

  // Blueprint Options State
  const [options, setOptions] = useState([]);
  const [loadingOptions, setLoadingOptions] = useState(false);
  const [optionsError, setOptionsError] = useState("");
  const [selectedBlueprintId, setSelectedBlueprintId] = useState("");

  // Action State (Generation & Session Start)
  const [generating, setGenerating] = useState(false);
  const [starting, setStarting] = useState(false);
  const [generatedTestId, setGeneratedTestId] = useState(null);
  const [actionError, setActionError] = useState("");
  const [resumeSessionId, setResumeSessionId] = useState(null);

  // In-flight and retained IDs refs
  const submittingRef = useRef(false);
  const generatedTestIdRef = useRef(null);

  const fetchOptions = async () => {
    setLoadingOptions(true);
    setOptionsError("");
    try {
      const res = await testGeneratorApi.getBlueprintExamOptions();
      const items = res.data || [];
      setOptions(items);
      if (items.length > 0) {
        setSelectedBlueprintId((prev) => {
          const exists = items.some((item) => item.blueprintId === prev);
          return exists ? prev : items[0].blueprintId;
        });
      } else {
        setSelectedBlueprintId("");
      }
    } catch (err) {
      setOptionsError(getTestGenErrorMessage(err, "Không thể tải danh sách cấu trúc đề thi. Vui lòng thử lại sau."));
    } finally {
      setLoadingOptions(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      setGenerating(false);
      setStarting(false);
      setActionError("");
      setResumeSessionId(null);
      submittingRef.current = false;
      fetchOptions();
    }
  }, [isOpen]);

  const selectedBlueprint = options.find((b) => b.blueprintId === selectedBlueprintId) || null;
  const isBusy = generating || starting;

  const handleCreateAndStart = async () => {
    if (submittingRef.current) return;

    // If an existing session is in progress, resume it
    if (resumeSessionId) {
      onClose();
      navigate(`/student/test/${resumeSessionId}`);
      return;
    }

    if (!selectedBlueprintId) return;

    submittingRef.current = true;
    setActionError("");

    let testIdToStart = generatedTestIdRef.current;

    // Step 1: Generate Test if not already generated
    if (!testIdToStart) {
      setGenerating(true);
      try {
        const res = await testGeneratorApi.generateBlueprintExam(selectedBlueprintId);
        testIdToStart = res.data?.testId;
        if (!testIdToStart) {
          throw new Error("Không nhận được mã đề thi từ máy chủ.");
        }
        generatedTestIdRef.current = testIdToStart;
        setGeneratedTestId(testIdToStart);
      } catch (err) {
        submittingRef.current = false;
        setGenerating(false);
        setActionError(getTestGenErrorMessage(err, "Không thể tạo bài thi theo năng lực. Vui lòng thử lại sau."));
        return;
      } finally {
        setGenerating(false);
      }
    }

    // Step 2: Start session with the retained testId
    setStarting(true);
    try {
      const sessionData = await startSession(testIdToStart);
      const sessionId = sessionData?.sessionId || sessionData?.id;

      if (sessionId) {
        generatedTestIdRef.current = null;
        setGeneratedTestId(null);
        submittingRef.current = false;
        setStarting(false);
        onClose();
        navigate(`/student/test/${sessionId}`);
      } else {
        throw new Error("Không nhận được mã phiên làm bài từ máy chủ.");
      }
    } catch (err) {
      submittingRef.current = false;
      setStarting(false);

      const errCode = err.response?.data?.code;
      if (errCode === "TESTING_SESSION_ALREADY_IN_PROGRESS") {
        const existingSessionId = err.response?.data?.existingSessionId;
        if (existingSessionId && typeof existingSessionId === "string") {
          setResumeSessionId(existingSessionId);
        }
        setActionError("Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này.");
        return;
      }

      setActionError(getTestGenErrorMessage(err, "Không thể bắt đầu phiên làm bài. Vui lòng thử bắt đầu lại."));
    }
  };

  const handleSafeClose = () => {
    if (!isBusy) {
      onClose();
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={handleSafeClose} isCloseDisabled={isBusy}>
      <div className="flex flex-col h-full select-none max-w-2xl mx-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">auto_awesome</span>
            Tạo đề theo năng lực
          </DialogTitle>
          <DialogDescription>
            Đề thi được cá nhân hóa dựa trên cấu trúc chuẩn và kết quả làm bài gần đây của em.
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 select-text">
            {/* Loading options */}
            {loadingOptions && (
              <div className="flex flex-col gap-3">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="p-4 bg-surface-container-low rounded-xl border border-whisper-border animate-pulse flex flex-col gap-2">
                    <div className="h-4 bg-surface-container-high rounded w-3/4"></div>
                    <div className="h-3 bg-surface-container rounded w-1/2"></div>
                  </div>
                ))}
              </div>
            )}

            {/* Error loading options */}
            {optionsError && !loadingOptions && (
              <div role="alert" className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                  <span>{optionsError}</span>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={fetchOptions}
                  className="h-8 text-xs font-bold shrink-0"
                >
                  Thử lại
                </Button>
              </div>
            )}

            {/* Empty options */}
            {!loadingOptions && !optionsError && options.length === 0 && (
              <div className="bg-surface-container-low border border-whisper-border rounded-xl p-8 text-center text-on-surface-variant flex flex-col items-center justify-center gap-2">
                <span className="material-symbols-outlined text-[40px] text-outline-variant">assignment_late</span>
                <p className="text-sm font-bold text-on-surface">Chưa có cấu trúc đề thi nào phù hợp với khối lớp của bạn.</p>
                <p className="text-xs">Vui lòng quay lại sau hoặc liên hệ giáo viên để biết thêm chi tiết.</p>
              </div>
            )}

            {/* Blueprint list selection */}
            {!loadingOptions && !optionsError && options.length > 0 && (
              <div className="flex flex-col gap-2.5">
                <label className="text-xs font-bold text-on-surface-variant uppercase tracking-wider">
                  Chọn cấu trúc đề thi
                </label>
                <div className="flex flex-col gap-2 max-h-60 overflow-y-auto pr-1">
                  {options.map((bp) => {
                    const isSelected = bp.blueprintId === selectedBlueprintId;
                    return (
                      <button
                        key={bp.blueprintId}
                        type="button"
                        onClick={() => {
                          if (!isBusy && !generatedTestId) {
                            setSelectedBlueprintId(bp.blueprintId);
                            setActionError("");
                          }
                        }}
                        disabled={isBusy || !!generatedTestId}
                        className={cn(
                          "w-full text-left p-3.5 rounded-xl border transition-all flex items-center justify-between gap-3 cursor-pointer select-none",
                          isSelected
                            ? "bg-primary/5 border-primary shadow-sm"
                            : "bg-surface-container-low border-whisper-border hover:border-primary/40",
                          (isBusy || !!generatedTestId) && "cursor-not-allowed opacity-80"
                        )}
                      >
                        <div className="flex items-start gap-3 min-w-0 flex-1">
                          <div className={cn(
                            "w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0 mt-0.5 transition-colors",
                            isSelected ? "border-primary bg-primary" : "border-outline-variant"
                          )}>
                            {isSelected && <div className="w-2 h-2 rounded-full bg-white"></div>}
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="text-xs font-bold text-on-surface truncate">
                                {bp.blueprintName}
                              </span>
                              <span className="bg-primary/10 text-primary border border-primary/20 text-[10px] font-extrabold px-1.5 py-0.2 rounded shrink-0">
                                Khối {bp.grade}
                              </span>
                            </div>
                            <div className="flex items-center gap-3 text-[11px] text-on-surface-variant mt-1">
                              <span>{bp.sectionCount} phần</span>
                              <span>·</span>
                              <span className="font-mono">{bp.totalQuestions} câu</span>
                              <span>·</span>
                              <span>{bp.durationMinutes === 0 ? "Không giới hạn" : `${bp.durationMinutes} phút`}</span>
                              <span>·</span>
                              <span className="font-mono font-bold text-primary">{bp.totalScore} điểm</span>
                            </div>
                          </div>
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Natural explanation and details banner */}
            {selectedBlueprint && (
              <div className="bg-surface-container-low border border-whisper-border p-4 rounded-xl flex flex-col gap-2.5">
                <div className="flex items-center gap-2 text-xs font-bold text-on-surface">
                  <span className="material-symbols-outlined text-primary text-[18px]">psychology</span>
                  <span>Quy định đề thi theo năng lực</span>
                </div>
                <div className="flex flex-col gap-1.5 text-xs text-on-surface-variant leading-relaxed">
                  <div className="flex items-start gap-2">
                    <span className="material-symbols-outlined text-primary text-[16px] shrink-0 mt-0.5">check_circle</span>
                    <span>Cấu trúc đề thi (số phần, số câu hỏi, dạng bài và thời gian làm bài) được giữ nguyên theo chuẩn.</span>
                  </div>
                  <div className="flex items-start gap-2">
                    <span className="material-symbols-outlined text-primary text-[16px] shrink-0 mt-0.5">tune</span>
                    <span>Độ khó của một số câu hỏi có thể được tự động điều chỉnh phù hợp với kết quả học tập gần đây của em.</span>
                  </div>
                </div>
              </div>
            )}

            {/* Resume Session Banner */}
            {resumeSessionId && (
              <div className="p-3.5 bg-amber-500/10 border border-amber-500/20 rounded-xl text-on-surface flex items-start gap-2.5 text-xs">
                <span className="material-symbols-outlined text-amber-700 text-[20px] shrink-0 mt-0.5">pending_actions</span>
                <div>
                  <strong className="block font-bold text-amber-700 mb-0.5">Phiên làm bài chưa hoàn tất</strong>
                  <p className="text-on-surface-variant leading-relaxed">
                    Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này. Hãy chọn "Tiếp tục bài đang làm" để tiếp tục tiến trình của bạn.
                  </p>
                </div>
              </div>
            )}

            {/* Action Error Banner */}
            {actionError && !resumeSessionId && (
              <div role="alert" className="p-3.5 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-start gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
                <p className="flex-1 leading-relaxed">{actionError}</p>
              </div>
            )}
          </div>
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={isBusy}
            onClick={handleSafeClose}
            className="min-h-[44px]"
          >
            Hủy
          </Button>

          <Button
            type="button"
            variant="primary"
            disabled={loadingOptions || options.length === 0 || !selectedBlueprintId || isBusy}
            onClick={handleCreateAndStart}
            className="min-h-[44px] min-w-[160px] font-bold"
          >
            {generating ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang tạo đề...</span>
              </div>
            ) : starting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang bắt đầu...</span>
              </div>
            ) : resumeSessionId ? (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                <span>Tiếp tục bài đang làm</span>
              </div>
            ) : generatedTestId && actionError ? (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">refresh</span>
                <span>Thử bắt đầu lại</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">auto_awesome</span>
                <span>Tạo và bắt đầu</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
