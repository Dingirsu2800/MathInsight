import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";

export default function GenerateSharedTestDialog({ isOpen, onClose, blueprint }) {
  const navigate = useNavigate();

  const [testName, setTestName] = useState("");
  const [durationMinutes, setDurationMinutes] = useState(90);
  const [fieldErrors, setFieldErrors] = useState({});
  const [apiError, setApiError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const submittingRef = useRef(false);
  const nameInputRef = useRef(null);

  // Initialize form fields when dialog opens or blueprint changes
  useEffect(() => {
    if (isOpen && blueprint) {
      setTestName(blueprint.blueprintName ? `${blueprint.blueprintName} - Đề thi` : "");
      setDurationMinutes(blueprint.durationMinutes || 90);
      setFieldErrors({});
      setApiError("");
      setSubmitting(false);
      submittingRef.current = false;

      // Focus name input when opened
      const frameId = requestAnimationFrame(() => {
        nameInputRef.current?.focus();
      });
      return () => cancelAnimationFrame(frameId);
    }
  }, [isOpen, blueprint]);

  const validateForm = () => {
    const errors = {};
    const trimmedName = testName.trim();

    if (!trimmedName) {
      errors.testName = "Vui lòng nhập tên đề thi.";
    } else if (trimmedName.length > 100) {
      errors.testName = "Tên đề thi không được vượt quá 100 ký tự.";
    }

    const durationNum = parseInt(durationMinutes, 10);
    if (isNaN(durationNum) || durationNum <= 0) {
      errors.durationMinutes = "Thời gian làm bài phải là số nguyên dương.";
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!blueprint?.blueprintId) return;

    if (!validateForm()) return;

    // Prevent duplicate in-flight requests (double click guard)
    if (submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setApiError("");

    try {
      const payload = {
        testName: testName.trim(),
        durationMinutes: parseInt(durationMinutes, 10)
      };

      const response = await testGeneratorApi.generateSharedBlueprintExam(blueprint.blueprintId, payload);
      const generatedTest = response.data;

      if (generatedTest && generatedTest.testId) {
        onClose();
        navigate(`/expert/tests/${generatedTest.testId}/preview`);
      } else {
        throw new Error("Dữ liệu phản hồi không hợp lệ.");
      }
    } catch (err) {
      setApiError(getTestGenErrorMessage(err, "Không thể hoàn tất sinh đề dùng chung. Vui lòng thử lại."));
      submittingRef.current = false;
      setSubmitting(false);
    }
  };

  const handleSafeClose = () => {
    if (!submitting) {
      onClose();
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={handleSafeClose} isCloseDisabled={submitting}>
      <form onSubmit={handleSubmit} className="flex flex-col h-full">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">auto_awesome</span>
            Sinh đề thi dùng chung
          </DialogTitle>
          <DialogDescription>
            Tạo biến thể đề thi dùng chung cố định (Shared Blueprint Exam) từ cấu trúc <span className="font-bold text-on-surface">"{blueprint?.blueprintName}"</span>.
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 select-text">
            {/* Top Info Notice */}
            <div className="bg-surface-container-low border border-whisper-border p-3.5 rounded-xl text-xs text-on-surface-variant flex items-start gap-2.5">
              <span className="material-symbols-outlined text-primary text-[18px] shrink-0 mt-0.5">info</span>
              <p className="leading-relaxed">
                Hệ thống sẽ lấy ngẫu nhiên các câu hỏi từ ngân hàng câu hỏi khớp với tỷ lệ ma trận và chốt thành một bản đề thi cố định cho thí sinh.
              </p>
            </div>

            {/* Test Name Field */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="generate-test-name" className="text-xs font-bold text-on-surface-variant">
                Tên đề thi <span className="text-error">*</span>
              </label>
              <input
                id="generate-test-name"
                ref={nameInputRef}
                type="text"
                disabled={submitting}
                value={testName}
                onChange={(e) => {
                  setTestName(e.target.value);
                  if (fieldErrors.testName) {
                    setFieldErrors((prev) => ({ ...prev, testName: undefined }));
                  }
                }}
                placeholder="Nhập tên đề thi thi..."
                className="w-full rounded-lg border border-outline-variant p-2.5 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
              />
              {fieldErrors.testName && (
                <span className="text-error text-[11px] font-semibold">{fieldErrors.testName}</span>
              )}
            </div>

            {/* Duration Field */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="generate-duration" className="text-xs font-bold text-on-surface-variant">
                Thời gian làm bài (phút) <span className="text-error">*</span>
              </label>
              <input
                id="generate-duration"
                type="number"
                min="1"
                disabled={submitting}
                value={durationMinutes}
                onChange={(e) => {
                  setDurationMinutes(e.target.value);
                  if (fieldErrors.durationMinutes) {
                    setFieldErrors((prev) => ({ ...prev, durationMinutes: undefined }));
                  }
                }}
                placeholder="Ví dụ: 90"
                className="w-full rounded-lg border border-outline-variant p-2.5 text-xs text-on-surface font-mono focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
              />
              {fieldErrors.durationMinutes && (
                <span className="text-error text-[11px] font-semibold">{fieldErrors.durationMinutes}</span>
              )}
            </div>

            {/* API Error Banner */}
            {apiError && (
              <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-start gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
                <p className="flex-1">{apiError}</p>
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
            type="submit"
            variant="primary"
            disabled={submitting}
            className="min-h-[44px] min-w-[130px] font-bold"
          >
            {submitting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang sinh đề...</span>
              </div>
            ) : (
              <div className="flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">auto_awesome</span>
                <span>Sinh đề thi</span>
              </div>
            )}
          </Button>
        </DialogFooter>
      </form>
    </Dialog>
  );
}
