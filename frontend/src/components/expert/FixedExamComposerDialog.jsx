import React, { useState, useEffect, useMemo } from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import FixedExamRequirementPanel from "./FixedExamRequirementPanel";
import FixedExamCandidateTable from "./FixedExamCandidateTable";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";

export default function FixedExamComposerDialog({
  isOpen,
  onClose,
  blueprint,
  onSuccess
}) {
  const blueprintDetails = useMemo(() => {
    if (blueprint?.sections && blueprint.sections.length > 0) {
      return blueprint.sections.flatMap((section) =>
        (section.details ?? []).map((detail) => ({
          ...detail,
          sectionName: section.sectionName || (section.sectionOrder ? `Phần ${section.sectionOrder}` : undefined),
          sectionOrder: section.sectionOrder,
          questionType: detail.questionType || section.questionType,
        }))
      );
    }
    return blueprint?.blueprintDetails ?? blueprint?.details ?? [];
  }, [blueprint]);

  const [testName, setTestName] = useState("");
  const [durationMinutes, setDurationMinutes] = useState("90");
  const [selectedQuestions, setSelectedQuestions] = useState([]);
  const [activeDetailId, setActiveDetailId] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  useEffect(() => {
    if (isOpen && blueprint) {
      const codeSuffix = Math.floor(100 + Math.random() * 900);
      const baseName = blueprint.blueprintName || blueprint.title || blueprint.name || "Đề thi";
      setTestName(`${baseName} - Mã đề 0${codeSuffix}`);
      setDurationMinutes(blueprint.durationMinutes ? String(blueprint.durationMinutes) : "90");
      setSelectedQuestions([]);
      setErrorMessage("");
      if (blueprintDetails.length > 0) {
        const firstId = blueprintDetails[0].blueprintDetailId || blueprintDetails[0].id;
        setActiveDetailId(firstId);
      }
    }
  }, [isOpen, blueprint, blueprintDetails]);


  const totalRequired = useMemo(() => {
    return blueprintDetails.reduce((acc, d) => acc + (d.quantity || 0), 0);
  }, [blueprintDetails]);

  const activeDetailObj = useMemo(() => {
    return blueprintDetails.find(d => (d.blueprintDetailId || d.id) === activeDetailId);
  }, [blueprintDetails, activeDetailId]);

  const isActiveDetailQuotaReached = useMemo(() => {
    if (!activeDetailObj) return false;
    const count = selectedQuestions.filter(q => q.blueprintDetailId === activeDetailId).length;
    const req = activeDetailObj.quantity || 0;
    return count >= req;
  }, [activeDetailObj, selectedQuestions, activeDetailId]);

  // Count check by detail
  const isDetailsQuotaSatisfied = useMemo(() => {
    const map = {};
    selectedQuestions.forEach(q => {
      map[q.blueprintDetailId] = (map[q.blueprintDetailId] || 0) + 1;
    });
    for (const d of blueprintDetails) {
      const dId = d.blueprintDetailId || d.id;
      const req = d.quantity || 0;
      const sel = map[dId] || 0;
      if (sel !== req) return false;
    }
    return true;
  }, [blueprintDetails, selectedQuestions]);

  const isValidToSubmit = useMemo(() => {
    if (!testName.trim()) return false;
    const duration = parseInt(durationMinutes);
    if (isNaN(duration) || duration <= 0) return false;
    if (selectedQuestions.length !== totalRequired) return false;
    return isDetailsQuotaSatisfied;
  }, [testName, durationMinutes, selectedQuestions, totalRequired, isDetailsQuotaSatisfied]);

  const handleAddQuestion = (candidate) => {
    if (!activeDetailId) return;
    const qId = candidate.questionId || candidate.id;
    const detailObj = blueprintDetails.find(d => (d.blueprintDetailId || d.id) === activeDetailId);

    setSelectedQuestions((previous) => {
      if (previous.some((q) => q.questionId === qId)) return previous;

      const selectedForDetail = previous.filter(
        (q) => q.blueprintDetailId === activeDetailId
      ).length;

      if (selectedForDetail >= (detailObj?.quantity ?? 0)) return previous;

      return [
        ...previous,
        {
          questionId: qId,
          blueprintDetailId: activeDetailId,
          content: candidate.content || candidate.questionContent || candidate.statement || "Nội dung câu hỏi",
          topicName: candidate.topicName || detailObj?.topicName || "",
          difficultyName: candidate.difficultyName || detailObj?.difficultyName || "",
          questionOrder: previous.length + 1
        }
      ];
    });
  };

  const handleRemoveQuestion = (index) => {
    setSelectedQuestions(prev => {
      const next = prev.filter((_, i) => i !== index);
      return next.map((q, i) => ({ ...q, questionOrder: i + 1 }));
    });
  };

  const handleMoveUp = (index) => {
    if (index <= 0) return;
    setSelectedQuestions(prev => {
      const next = [...prev];
      const temp = next[index - 1];
      next[index - 1] = next[index];
      next[index] = temp;
      return next.map((q, i) => ({ ...q, questionOrder: i + 1 }));
    });
  };

  const handleMoveDown = (index) => {
    if (index >= selectedQuestions.length - 1) return;
    setSelectedQuestions(prev => {
      const next = [...prev];
      const temp = next[index + 1];
      next[index + 1] = next[index];
      next[index] = temp;
      return next.map((q, i) => ({ ...q, questionOrder: i + 1 }));
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!isValidToSubmit || submitting) return;

    setSubmitting(true);
    setErrorMessage("");

    const payload = {
      testName: testName.trim(),
      durationMinutes: parseInt(durationMinutes),
      questions: selectedQuestions.map((q, idx) => ({
        questionId: q.questionId,
        blueprintDetailId: q.blueprintDetailId,
        questionOrder: idx + 1
      }))
    };

    try {
      const blueprintId = blueprint.blueprintId || blueprint.id;
      const res = await testGeneratorApi.generateFixedBlueprintExam(blueprintId, payload);
      const newTest = res.data;
      if (onSuccess) {
        onSuccess(newTest);
      }
      onClose();
    } catch (err) {
      console.error(err);
      setErrorMessage(getTestGenErrorMessage(err, "Không thể tạo đề thi cố định. Vui lòng kiểm tra lại thứ tự và danh sách câu hỏi."));
    } finally {
      setSubmitting(false);
    }
  };

  if (!blueprint) return null;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={() => !submitting && onClose()}
      isCloseDisabled={submitting}
      className="w-[96vw] max-w-[1500px] h-[90vh] sm:max-w-[1500px]"
    >
      <div className="flex flex-col h-full max-w-7xl mx-auto w-full select-none">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-on-surface">
            <span className="material-symbols-outlined text-primary text-[22px]">playlist_add_check</span>
            Tạo đề thi cố định cho chuyên gia
          </DialogTitle>
          <DialogDescription>
            Tự chọn câu hỏi cho từng phần trong ma trận để tạo đề thi cố định dùng chung cho học sinh.
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4">

            {/* Error Banner */}
            {errorMessage && (
              <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                <span className="flex-1">{errorMessage}</span>
              </div>
            )}

            {/* General Exam Form Header */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 bg-surface-container-low/40 p-3 rounded-xl border border-whisper-border">
              <div className="md:col-span-2">
                <label className="block text-[10px] font-bold uppercase text-on-surface-variant mb-1">
                  Tên đề thi <span className="text-error">*</span>
                </label>
                <input
                  type="text"
                  value={testName}
                  onChange={(e) => setTestName(e.target.value)}
                  placeholder="Ví dụ: Đề thi thử THPT Quốc gia 2025 - Mã 0101"
                  className="w-full h-9 px-3 bg-pure-surface border border-outline-variant rounded-lg text-xs font-semibold text-on-surface focus:border-primary outline-none"
                  required
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold uppercase text-on-surface-variant mb-1">
                  Thời gian (phút) <span className="text-error">*</span>
                </label>
                <input
                  type="number"
                  min="5"
                  max="300"
                  value={durationMinutes}
                  onChange={(e) => setDurationMinutes(e.target.value)}
                  className="w-full h-9 px-3 bg-pure-surface border border-outline-variant rounded-lg text-xs font-bold font-mono text-on-surface focus:border-primary outline-none"
                  required
                />
              </div>
            </div>

            {/* Main 3-Column Composer Workspace */}
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start min-h-[480px]">

              {/* Left Column: Requirements Matrix (3 cols) */}
              <div className="lg:col-span-3">
                <FixedExamRequirementPanel
                  blueprintDetails={blueprintDetails}
                  selectedQuestions={selectedQuestions}
                  activeDetailId={activeDetailId}
                  onSelectDetail={(id) => setActiveDetailId(id)}
                />
              </div>

              {/* Center Column: Question Candidate Search & Picker (5 cols) */}
              <div className="lg:col-span-5 bg-surface-container-low/30 border border-whisper-border rounded-xl p-3 flex flex-col">
                <h4 className="text-xs font-bold text-on-surface uppercase tracking-wider mb-2 flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-primary text-[18px]">find_in_page</span>
                  Danh sách câu hỏi khả dụng
                </h4>
                <FixedExamCandidateTable
                  blueprintId={blueprint.blueprintId || blueprint.id}
                  activeDetailId={activeDetailId}
                  activeDetail={activeDetailObj}
                  isQuotaReached={isActiveDetailQuotaReached}
                  selectedQuestions={selectedQuestions}
                  onAddQuestion={handleAddQuestion}
                />
              </div>

              {/* Right Column: Ordered Selected Questions List (4 cols) */}
              <div className="lg:col-span-4 bg-pure-surface border border-whisper-border rounded-xl p-3 flex flex-col gap-2 max-h-[540px] overflow-hidden">
                <div className="flex items-center justify-between border-b border-whisper-border pb-2">
                  <h4 className="text-xs font-bold text-on-surface uppercase tracking-wider flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-primary text-[18px]">format_list_numbered</span>
                    Thứ tự đề thi ({selectedQuestions.length}/{totalRequired})
                  </h4>
                  <span className="text-[10px] text-on-surface-variant font-mono">
                    {selectedQuestions.length === totalRequired ? "Đủ số lượng" : "Cần chọn thêm"}
                  </span>
                </div>

                <div className="flex flex-col gap-2 overflow-y-auto pr-1 flex-1 min-h-[380px]">
                  {selectedQuestions.length === 0 ? (
                    <div className="p-8 text-center text-xs text-on-surface-variant">
                      Chưa có câu hỏi nào được chọn. Chọn câu hỏi ở cột giữa để thêm vào đề.
                    </div>
                  ) : (
                    selectedQuestions.map((q, idx) => (
                      <div
                        key={q.questionId}
                        className="p-2.5 rounded-xl border border-whisper-border bg-surface-container-low/40 flex items-center justify-between gap-2 text-xs"
                      >
                        <div className="flex items-center gap-2 min-w-0 flex-1">
                          <span className="w-6 h-6 rounded-full bg-primary/10 text-primary font-bold text-[11px] font-mono flex items-center justify-center shrink-0">
                            {idx + 1}
                          </span>
                          <span className="text-on-surface font-medium truncate line-clamp-1">
                            {q.content}
                          </span>
                        </div>

                        <div className="flex items-center gap-1 shrink-0">
                          <button
                            type="button"
                            disabled={idx === 0}
                            onClick={() => handleMoveUp(idx)}
                            className="p-1 rounded text-on-surface-variant hover:text-primary disabled:opacity-30"
                            title="Di chuyển lên"
                          >
                            <span className="material-symbols-outlined text-[18px]">arrow_upward</span>
                          </button>
                          <button
                            type="button"
                            disabled={idx === selectedQuestions.length - 1}
                            onClick={() => handleMoveDown(idx)}
                            className="p-1 rounded text-on-surface-variant hover:text-primary disabled:opacity-30"
                            title="Di chuyển xuống"
                          >
                            <span className="material-symbols-outlined text-[18px]">arrow_downward</span>
                          </button>
                          <button
                            type="button"
                            onClick={() => handleRemoveQuestion(idx)}
                            className="p-1 rounded text-error hover:bg-error/10"
                            title="Xóa khỏi đề"
                          >
                            <span className="material-symbols-outlined text-[18px]">delete</span>
                          </button>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>

            </div>

          </div>
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            disabled={submitting}
            onClick={onClose}
          >
            Hủy
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={!isValidToSubmit || submitting}
            onClick={handleSubmit}
            className="min-w-[160px] font-bold"
          >
            {submitting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                <span>Đang tạo đề...</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">playlist_add_check</span>
                <span>Tạo đề cố định</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
