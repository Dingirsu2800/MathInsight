import React, { useState } from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../ui/dialog";
import { Button } from "../ui/button";
import client from "../../services/questionBankApiClient";
import { clearCachedProfile } from "../../hooks/useCurrentUser";
import { toast } from "../common/Toast";
import { cn } from "../../utils/cn";

export default function GradeOnboardingModal({ profile }) {
  const [selectedGrade, setSelectedGrade] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async () => {
    if (!selectedGrade) {
      setError("Vui lòng chọn một khối lớp.");
      return;
    }

    setSubmitting(true);
    setError("");

    try {
      // Create a payload that only updates currentGrade. 
      // The backend partial update logic treats missing fields as "keep original".
      await client.put("/api/v1/accounts/profile", {
        currentGrade: Number(selectedGrade)
      });
      
      clearCachedProfile();
      toast.success("Đã lưu khối lớp thành công!");
      window.location.reload(); // Reload the app to update routes and fetching logic
    } catch (err) {
      console.error("Failed to update grade", err);
      setError("Có lỗi xảy ra khi lưu khối lớp. Vui lòng thử lại.");
      setSubmitting(false);
    }
  };

  const grades = [10, 11, 12];

  // We set isOpen=true and onClose to empty, with isCloseDisabled=true to prevent dismissing.
  return (
    <Dialog isOpen={true} onClose={() => {}} isCloseDisabled={true}>
      <div className="flex flex-col h-full select-none">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">school</span>
            Chào mừng bạn đến với MathInsight!
          </DialogTitle>
          <DialogDescription>
            Trước khi bắt đầu, vui lòng cho chúng tôi biết bạn đang học lớp mấy để hệ thống gợi ý bài giảng và đề thi phù hợp nhất nhé.
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <div className="flex flex-col gap-4 py-2">
            <div className="grid grid-cols-3 gap-3">
              {grades.map((grade) => {
                const isSelected = selectedGrade === grade;
                return (
                  <button
                    key={grade}
                    type="button"
                    disabled={submitting}
                    onClick={() => {
                      setSelectedGrade(grade);
                      if (error) setError("");
                    }}
                    className={cn(
                      "flex flex-col items-center justify-center p-4 rounded-xl border-2 transition-all duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                      isSelected
                        ? "border-primary bg-primary/10 shadow-sm"
                        : "border-whisper-border bg-surface-container-low hover:bg-surface-container hover:border-primary/40 text-on-surface-variant hover:text-on-surface",
                      submitting && "opacity-50 cursor-not-allowed"
                    )}
                  >
                    <span className={cn("text-2xl font-black mb-1", isSelected && "text-primary")}>
                      {grade}
                    </span>
                    <span className="text-xs font-semibold">Khối lớp</span>
                  </button>
                );
              })}
            </div>

            {error && (
              <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2">
                <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                <span>{error}</span>
              </div>
            )}
          </div>
        </DialogContent>

        <DialogFooter>
          <Button
            type="button"
            variant="primary"
            disabled={submitting || !selectedGrade}
            onClick={handleSubmit}
            className="w-full h-11 font-bold text-sm justify-center"
          >
            {submitting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                <span>Đang lưu...</span>
              </div>
            ) : (
              "Lưu và Bắt đầu"
            )}
          </Button>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
