import React, { useState, useEffect } from "react";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getQuestionTypeLabel, getStatusLabel, getStatusBadgeVariant } from "../../utils/blueprintLabels";
import { getBlueprintActions } from "../../utils/blueprintAuth";
import { getBlueprintErrorMessage } from "../../utils/blueprintErrorLocalizer";
import { validateBlueprintForSubmit } from "../../utils/blueprintValidation";
import { cn } from "../../utils/cn";

export default function BlueprintDetailPage() {
  const navigate = useNavigate();
  const { blueprintId } = useParams();
  const location = useLocation();

  // Blueprint data state
  const [blueprint, setBlueprint] = useState(null);
  const [loading, setLoading] = useState(true);
  const [pageError, setPageError] = useState(null);

  // Interaction / Mutation states
  const [isMutating, setIsMutating] = useState(false);
  const [localFeedback, setLocalFeedback] = useState(null);
  const [submitErrors, setSubmitErrors] = useState([]);

  // Modal dialog states
  const [isSubmitOpen, setIsSubmitOpen] = useState(false);
  const [isApproveOpen, setIsApproveOpen] = useState(false);
  const [isRejectOpen, setIsRejectOpen] = useState(false);
  const [isCloneOpen, setIsCloneOpen] = useState(false);
  const [isDeleteOpen, setIsDeleteOpen] = useState(false);

  // Review states
  const [rejectNote, setRejectNote] = useState("");
  const [rejectError, setRejectError] = useState("");

  const currentAccountId = localStorage.getItem("AccountId");

  // Read location state feedback once
  useEffect(() => {
    if (location.state?.feedback) {
      setLocalFeedback(location.state.feedback);
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location, navigate]);

  // Fetch blueprint details
  const fetchDetail = async () => {
    setLoading(true);
    setPageError(null);
    try {
      const res = await testGeneratorApi.getBlueprintDetail(blueprintId);
      setBlueprint(res.data);
    } catch (err) {
      setPageError(getBlueprintErrorMessage(err, "Không thể tải chi tiết cấu trúc đề. Vui lòng thử lại."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDetail();
  }, [blueprintId]);

  if (loading) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col items-center justify-center min-h-[300px]">
          <div className="w-10 h-10 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p className="mt-4 text-sm text-on-surface-variant font-semibold">Đang tải chi tiết cấu trúc đề...</p>
        </div>
      </ExpertLayout>
    );
  }

  if (pageError || !blueprint) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col gap-4 max-w-xl mx-auto text-center mt-12 select-text">
          <span className="material-symbols-outlined text-[48px] text-error">error</span>
          <h2 className="text-xl font-bold text-on-background">Đã xảy ra lỗi</h2>
          <p className="text-sm text-on-surface-variant">{pageError || "Không tìm thấy cấu trúc đề này."}</p>
          <Button className="mt-4" onClick={() => navigate("/expert/blueprints")}>Quay lại danh sách</Button>
        </div>
      </ExpertLayout>
    );
  }

  const actions = getBlueprintActions(blueprint, currentAccountId);

  // Submit flow
  const handleSubmitOpen = () => {
    setSubmitErrors([]);
    setLocalFeedback(null);

    // Call submit validator before opening the confirm dialog
    const validationResult = validateBlueprintForSubmit(blueprint);
    if (!validationResult.isValid) {
      setSubmitErrors(validationResult.errors);
      setLocalFeedback({
        type: "error",
        message: "Không thể gửi phản biện do cấu trúc đề chưa khớp số lượng câu hỏi."
      });
      window.scrollTo({ top: 0, behavior: "smooth" });
      return;
    }

    setIsSubmitOpen(true);
  };

  const handleSubmitConfirm = async () => {
    setIsMutating(true);
    setLocalFeedback(null);
    try {
      await testGeneratorApi.submitBlueprintForReview(blueprintId);
      setIsSubmitOpen(false);
      setLocalFeedback({ type: "success", message: "Gửi phản biện cấu trúc đề thành công!" });
      fetchDetail();
    } catch (err) {
      handleMutationError(err, "Không thể gửi phản biện. Vui lòng thử lại.");
    } finally {
      setIsMutating(false);
    }
  };

  // Approve flow
  const handleApproveConfirm = async () => {
    setIsMutating(true);
    setLocalFeedback(null);
    try {
      await testGeneratorApi.reviewBlueprint(blueprintId, { action: "Approve", reviewNote: null });
      setIsApproveOpen(false);
      setLocalFeedback({ type: "success", message: "Phê duyệt cấu trúc đề thành công!" });
      fetchDetail();
    } catch (err) {
      handleMutationError(err, "Không thể phê duyệt cấu trúc đề. Vui lòng thử lại.");
    } finally {
      setIsMutating(false);
    }
  };

  // Reject flow
  const handleRejectConfirm = async () => {
    const trimmed = rejectNote.trim();
    if (!trimmed) {
      setRejectError("Vui lòng nhập lý do từ chối.");
      return;
    }
    if (trimmed.length > 2000) {
      setRejectError("Lý do từ chối không được vượt quá 2000 ký tự.");
      return;
    }

    setIsMutating(true);
    setRejectError("");
    setLocalFeedback(null);
    try {
      await testGeneratorApi.reviewBlueprint(blueprintId, { action: "Reject", reviewNote: trimmed });
      setIsRejectOpen(false);
      setRejectNote("");
      setLocalFeedback({ type: "success", message: "Từ chối cấu trúc đề thành công." });
      fetchDetail();
    } catch (err) {
      handleMutationError(err, "Không thể từ chối cấu trúc đề. Vui lòng thử lại.");
    } finally {
      setIsMutating(false);
    }
  };

  // Clone flow
  const handleCloneConfirm = async () => {
    setIsMutating(true);
    setLocalFeedback(null);
    try {
      const res = await testGeneratorApi.cloneBlueprint(blueprintId);
      setIsCloneOpen(false);
      navigate(`/expert/blueprints/${res.data.blueprintId}/edit`, {
        state: {
          feedback: {
            type: "success",
            message: `Nhân bản cấu trúc đề thành công thành bản sao nháp: "${res.data.blueprintName}"`
          }
        }
      });
    } catch (err) {
      setLocalFeedback({
        type: "error",
        message: getBlueprintErrorMessage(err, "Không thể nhân bản cấu trúc đề này. Vui lòng thử lại.")
      });
      setIsCloneOpen(false);
    } finally {
      setIsMutating(false);
    }
  };

  // Delete flow
  const handleDeleteConfirm = async () => {
    setIsMutating(true);
    setLocalFeedback(null);
    try {
      const res = await testGeneratorApi.deleteBlueprint(blueprintId);
      setIsDeleteOpen(false);

      const successMsg = res.data?.wasDeactivated
        ? "Đã ngừng sử dụng cấu trúc đề thành công."
        : "Đã xóa cấu trúc đề thành công.";

      navigate("/expert/blueprints", {
        state: { feedback: { type: "success", message: successMsg } }
      });
    } catch (err) {
      handleMutationError(err, "Không thể xóa cấu trúc đề. Vui lòng thử lại.");
      setIsDeleteOpen(false);
    } finally {
      setIsMutating(false);
    }
  };

  // Helper for mutation errors (handles BLUEPRINT_STATUS_INVALID reload)
  const handleMutationError = (err, defaultMsg) => {
    const errorMsg = getBlueprintErrorMessage(err, defaultMsg);
    setLocalFeedback({ type: "error", message: errorMsg });

    const code = err.response?.data?.code;
    if (code === "BLUEPRINT_STATUS_INVALID") {
      // Reload detail to sync status first, then unlock actions
      fetchDetail();
    }
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  // Dialog safe onClose wrappers
  const handleDialogClose = (setter) => {
    if (!isMutating) {
      setter(false);
    }
  };

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">

        {/* Page Header */}
        <DashboardPageHeader
          title={blueprint.blueprintName}
          subtitle="Xem chi tiết thiết lập phần thi, chủ đề phân bổ và thực hiện các quy trình kiểm soát."
        >
          <div className="flex flex-wrap gap-2.5">
            <Button
              variant="outline"
              disabled={isMutating}
              onClick={() => navigate("/expert/blueprints")}
            >
              Quay lại
            </Button>

            {actions.canEdit && (
              <Button
                variant="outline"
                disabled={isMutating}
                onClick={() => navigate(`/expert/blueprints/${blueprintId}/edit`)}
              >
                <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">edit</span>
                Chỉnh sửa
              </Button>
            )}

            {actions.canSubmit && (
              <Button
                variant="primary"
                disabled={isMutating}
                onClick={handleSubmitOpen}
              >
                <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">send</span>
                Gửi phản biện
              </Button>
            )}

            {actions.canReview && (
              <>
                <Button
                  variant="primary"
                  className="bg-emerald-success text-pure-surface hover:bg-emerald-success/90"
                  disabled={isMutating}
                  onClick={() => setIsApproveOpen(true)}
                >
                  <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">check</span>
                  Phê duyệt
                </Button>
                <Button
                  variant="destructive"
                  disabled={isMutating}
                  onClick={() => {
                    setRejectNote("");
                    setRejectError("");
                    setIsRejectOpen(true);
                  }}
                >
                  <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">close</span>
                  Từ chối
                </Button>
              </>
            )}

            {actions.canClone && (
              <Button
                variant="outline"
                disabled={isMutating}
                onClick={() => setIsCloneOpen(true)}
              >
                <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">content_copy</span>
                Sao chép
              </Button>
            )}

            {(actions.canDelete || actions.canDeactivate) && (
              <Button
                variant="destructive"
                disabled={isMutating}
                onClick={() => setIsDeleteOpen(true)}
              >
                <span className="material-symbols-outlined text-[16px] mr-1.5 font-bold">delete</span>
                {actions.canDeactivate ? "Ngừng sử dụng" : "Xóa cấu trúc"}
              </Button>
            )}
          </div>
        </DashboardPageHeader>

        {/* Feedback Alert Banners */}
        {localFeedback && (
          <div className={cn(
            "p-4 rounded-xl border flex items-start gap-3 relative select-text whitespace-pre-line",
            {
              "bg-emerald-success/10 border-emerald-success/20 text-emerald-success": localFeedback.type === "success",
              "bg-error/10 border-error/20 text-error": localFeedback.type === "error"
            }
          )}>
            <span className="material-symbols-outlined mt-0.5 shrink-0">
              {localFeedback.type === "success" ? "check_circle" : "warning"}
            </span>
            <div className="flex-1 pr-8">
              <p className="text-xs font-bold leading-relaxed">{localFeedback.message}</p>
              {submitErrors.length > 0 && (
                <ul className="list-disc pl-4 mt-2 text-[11px] text-error font-medium flex flex-col gap-1">
                  {submitErrors.map((errText, idx) => (
                    <li key={idx}>{errText}</li>
                  ))}
                </ul>
              )}
            </div>
            <button
              onClick={() => setLocalFeedback(null)}
              aria-label="Đóng thông báo"
              className="absolute top-3 right-3 text-on-surface-variant hover:text-on-surface transition-colors cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {/* Rejection Note Prominent Banner */}
        {blueprint.status === "Rejected" && blueprint.reviewNote && (
          <div className="p-5 rounded-xl border bg-error/10 border-error/20 text-error flex items-start gap-4 select-text">
            <span className="material-symbols-outlined mt-0.5 shrink-0 text-[24px]">cancel</span>
            <div className="flex-1">
              <h3 className="text-sm font-bold">Cấu trúc đề bị từ chối phê duyệt</h3>
              <p className="text-xs mt-1.5 leading-relaxed bg-pure-surface/50 border border-error/10 p-3 rounded-lg text-on-surface font-semibold">
                {blueprint.reviewNote}
              </p>
              {blueprint.approvedByName && (
                <p className="text-[10px] text-on-surface-variant mt-2">
                  Phản biện bởi: <span className="font-bold text-on-surface">{blueprint.approvedByName}</span>
                </p>
              )}
            </div>
          </div>
        )}

        {/* Detail content grid layout */}
        <div className="grid grid-cols-12 gap-6 items-start">

          {/* Left Column: Sections list matrix (Spans 8) */}
          <div className="col-span-12 lg:col-span-8 flex flex-col gap-6">
            {blueprint.sections && blueprint.sections.length === 0 ? (
              <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant shadow-sm">
                Không tìm thấy cấu trúc phần thi nào.
              </div>
            ) : (
              (blueprint.sections || []).map((sec, secIdx) => {
                const isComposite = sec.questionType === "Composite";

                return (
                  <div key={sec.blueprintSectionId || secIdx} className="bg-pure-surface border border-whisper-border rounded-xl p-6 shadow-sm flex flex-col gap-4">
                    <div className="flex justify-between items-center border-b border-whisper-border pb-3">
                      <h3 className="text-sm font-bold text-on-surface">
                        Phần {secIdx + 1}: {sec.sectionName}
                      </h3>
                      <div className="flex gap-2">
                        {sec.sectionCode && (
                          <span className="bg-surface px-2.5 py-0.5 rounded border border-whisper-border text-[10px] font-bold text-on-surface">
                            Mã: {sec.sectionCode}
                          </span>
                        )}
                        <span className="bg-primary/10 text-primary px-2.5 py-0.5 rounded border border-primary/20 text-[10px] font-bold uppercase tracking-wider">
                          {getQuestionTypeLabel(sec.questionType)}
                        </span>
                      </div>
                    </div>

                    {/* Instruction text */}
                    {sec.instructionText && (
                      <div className="bg-surface-container-low border border-whisper-border p-3.5 rounded-lg text-xs text-on-surface-variant leading-relaxed select-text">
                        <span className="font-bold block text-[10px] uppercase text-on-surface-variant tracking-wider mb-1">
                          Hướng dẫn làm bài:
                        </span>
                        {sec.instructionText}
                      </div>
                    )}

                    {/* Meta info row */}
                    <div className="flex gap-6 text-xs text-on-surface-variant select-text">
                      <div>
                        Số lượng câu hỏi: <span className="font-bold text-on-surface font-mono">{sec.totalQuestions} câu</span>
                      </div>
                      <div className="border-l border-whisper-border pl-6">
                        Điểm mặc định: <span className="font-bold text-on-surface font-mono">{sec.defaultPointPerQuestion}đ/câu</span>
                      </div>
                      {isComposite && (
                        <>
                          <div className="border-l border-whisper-border pl-6">
                            Số phần: <span className="font-bold text-primary font-mono">{sec.partCountPerQuestion} phần/câu</span>
                          </div>
                          <div className="border-l border-whisper-border pl-6">
                            Điểm/Phần: <span className="font-bold text-primary font-mono">{sec.defaultPointPerPart}đ/phần</span>
                          </div>
                        </>
                      )}
                    </div>

                    {/* Allocation Details Table */}
                    <div className="border border-whisper-border rounded-xl overflow-hidden mt-2">
                      <table className="w-full text-left border-collapse select-text">
                        <thead className="bg-surface-container-low border-b border-whisper-border text-[11px] font-bold text-on-surface-variant">
                          <tr>
                            <th className="p-2.5 pl-4">Chủ đề</th>
                            <th className="p-2.5 w-48">Độ khó</th>
                            <th className="p-2.5 w-32 text-right pr-6">Số lượng câu</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-whisper-border bg-pure-surface text-xs">
                          {(sec.details || []).map((det, detIdx) => (
                            <tr key={det.blueprintDetailId || detIdx} className="hover:bg-surface-bright transition-colors">
                              <td className="p-2.5 pl-4 font-semibold text-on-surface">
                                {det.tagName || "Chủ đề chưa xác định"}
                              </td>
                              <td className="p-2.5">
                                <span className={cn(
                                  "text-[10px] font-bold uppercase tracking-wide px-2.5 py-0.5 rounded-full border",
                                  {
                                    "text-emerald-success bg-emerald-success/10 border-emerald-success/20": det.difficultyLevel === 1,
                                    "text-primary bg-primary/10 border-primary/20": det.difficultyLevel === 2,
                                    "text-amber-warning bg-amber-warning/10 border-amber-warning/20": det.difficultyLevel === 3,
                                    "text-deep-rose bg-deep-rose/10 border-deep-rose/20": det.difficultyLevel === 4
                                  }
                                )}>
                                  {det.difficultyName || "Không xác định"}
                                </span>
                              </td>
                              <td className="p-2.5 text-right pr-6 font-bold text-on-surface font-mono">
                                {det.quantity} câu
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>

                  </div>
                );
              })
            )}
          </div>

          {/* Right Column: Metadata info panel (Spans 4) */}
          <div className="col-span-12 lg:col-span-4 sticky top-6">
            <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm flex flex-col gap-4 select-text">
              <h4 className="text-xs font-bold text-on-surface border-b border-whisper-border pb-3 uppercase tracking-wider">
                Thông tin chung cấu trúc
              </h4>
              <ul className="flex flex-col gap-3.5 text-xs">
                <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                  <span className="text-on-surface-variant font-medium">Khối lớp</span>
                  <span className="text-on-surface font-bold">Khối {blueprint.grade}</span>
                </li>
                <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                  <span className="text-on-surface-variant font-medium">Tổng số câu hỏi</span>
                  <span className="text-on-surface font-bold font-mono">{blueprint.totalQuestions} câu</span>
                </li>
                <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                  <span className="text-on-surface-variant font-medium">Thời gian làm bài</span>
                  <span className="text-on-surface font-bold">{blueprint.durationMinutes} phút</span>
                </li>
                <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                  <span className="text-on-surface-variant font-medium">Người lập cấu trúc</span>
                  <span className="text-on-surface font-bold">{blueprint.expertName || "Chưa xác định"}</span>
                </li>
                <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                  <span className="text-on-surface-variant font-medium">Trạng thái</span>
                  <Badge variant={getStatusBadgeVariant(blueprint.status)}>
                    {getStatusLabel(blueprint.status)}
                  </Badge>
                </li>
                {blueprint.approvedByName && (
                  <>
                    <li className="flex justify-between items-center border-b border-whisper-border/30 pb-2">
                      <span className="text-on-surface-variant font-medium">Người phản biện</span>
                      <span className="text-on-surface font-bold">{blueprint.approvedByName}</span>
                    </li>
                    {blueprint.reviewTime && (
                      <li className="flex justify-between items-center pb-1">
                        <span className="text-on-surface-variant font-medium">Thời gian kiểm soát</span>
                        <span className="text-on-surface font-bold">
                          {new Date(blueprint.reviewTime).toLocaleString("vi-VN", { timeZone: "UTC" })} (UTC)
                        </span>
                      </li>
                    )}
                  </>
                )}
              </ul>
            </div>
          </div>

        </div>

      </div>

      {/* Submit Confirmation Dialog */}
      <Dialog isOpen={isSubmitOpen} onClose={() => handleDialogClose(setIsSubmitOpen)}>
        <DialogHeader>
          <DialogTitle>Gửi yêu cầu phản biện cấu trúc</DialogTitle>
          <DialogDescription>
            Cấu trúc đề sẽ được chuyển sang trạng thái chờ phản biện và khóa chỉnh sửa.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Bạn có chắc chắn muốn gửi cấu trúc đề này cho các chuyên gia khác kiểm tra và phản biện?
            Sau khi gửi, bạn sẽ không thể chỉnh sửa trừ khi cấu trúc đề bị từ chối phê duyệt.
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={isMutating}
            onClick={() => setIsSubmitOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="primary"
            disabled={isMutating}
            onClick={handleSubmitConfirm}
          >
            {isMutating ? "Đang xử lý..." : "Xác nhận gửi"}
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Approve Confirmation Dialog */}
      <Dialog isOpen={isApproveOpen} onClose={() => handleDialogClose(setIsApproveOpen)}>
        <DialogHeader>
          <DialogTitle>Phê duyệt cấu trúc đề thi</DialogTitle>
          <DialogDescription>
            Xác nhận cấu trúc đề thi này đạt tiêu chuẩn chất lượng.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Bạn có chắc chắn phê duyệt cấu trúc đề này? Cấu trúc sau khi được duyệt sẽ sẵn sàng để sinh đề thi
            cho học sinh làm bài trên hệ thống. Hành động này không thể hoàn tác.
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={isMutating}
            onClick={() => setIsApproveOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="primary"
            className="bg-emerald-success text-pure-surface hover:bg-emerald-success/90 border-emerald-success/25"
            disabled={isMutating}
            onClick={handleApproveConfirm}
          >
            {isMutating ? "Đang phê duyệt..." : "Xác nhận Phê duyệt"}
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Reject Dialog */}
      <Dialog isOpen={isRejectOpen} onClose={() => handleDialogClose(setIsRejectOpen)}>
        <DialogHeader>
          <DialogTitle className="text-error flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[20px]">warning</span>
            Từ chối phê duyệt cấu trúc
          </DialogTitle>
          <DialogDescription>
            Vui lòng ghi rõ lý do để tác giả cấu trúc đề chỉnh sửa.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <div className="flex flex-col gap-1.5 select-text">
            <label className="text-[11px] font-bold text-on-surface-variant" htmlFor="reject-reason-input">
              LÝ DO TỪ CHỐI <span className="text-error">*</span>
            </label>
            <textarea
              id="reject-reason-input"
              rows={4}
              value={rejectNote}
              onChange={(e) => {
                setRejectNote(e.target.value);
                setRejectError("");
              }}
              placeholder="Nhập lý do chi tiết (ví dụ: Tỷ lệ độ khó Nhận biết ở Phần 1 quá cao, cần phân bổ thêm câu hỏi Vận dụng...)"
              className="w-full rounded-lg border border-outline-variant p-2.5 text-xs text-on-surface focus:outline-none focus:border-error focus:ring-1 focus:ring-error transition-all resize-none"
            />
            <div className="flex justify-between items-center mt-1">
              <span className="text-error text-[10px] font-bold">{rejectError}</span>
              <span className="text-[10px] text-on-surface-variant font-mono">
                {rejectNote.trim().length} / 2000
              </span>
            </div>
          </div>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={isMutating}
            onClick={() => setIsRejectOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="destructive"
            disabled={isMutating}
            onClick={handleRejectConfirm}
          >
            {isMutating ? "Đang gửi..." : "Từ chối cấu trúc"}
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Clone Confirmation Dialog */}
      <Dialog isOpen={isCloneOpen} onClose={() => handleDialogClose(setIsCloneOpen)}>
        <DialogHeader>
          <DialogTitle>Sao chép cấu trúc đề thi</DialogTitle>
          <DialogDescription>
            Nhân bản cấu trúc này thành một bản sao nháp (Draft) thuộc quyền sở hữu của bạn.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Hệ thống sẽ nhân bản sâu toàn bộ phần thi và phân bổ chủ đề của cấu trúc đề thi hiện tại.
            Bạn có chắc chắn muốn tiến hành sao chép bản mới?
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={isMutating}
            onClick={() => setIsCloneOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="primary"
            disabled={isMutating}
            onClick={handleCloneConfirm}
          >
            {isMutating ? "Đang sao chép..." : "Xác nhận Sao chép"}
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Delete/Deactivate Confirmation Dialog */}
      <Dialog isOpen={isDeleteOpen} onClose={() => handleDialogClose(setIsDeleteOpen)}>
        <DialogHeader>
          <DialogTitle>Xác nhận hành động xóa cấu trúc</DialogTitle>
          <DialogDescription>
            {actions.canDeactivate
              ? "Ngừng sử dụng cấu trúc đề thi đang hoạt động."
              : "Xóa cấu trúc đề thi khỏi hệ thống."}
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            {blueprint.status === "Approved" ? (
              <span>
                Cấu trúc đề sẽ bị xóa hẳn nếu chưa từng được sử dụng để tạo đề thi thực tế,
                ngược lại sẽ được tự động chuyển sang trạng thái <strong>Ngừng sử dụng</strong> để bảo toàn lịch sử hệ thống.
              </span>
            ) : actions.canDeactivate ? (
              <span>
                Hành động này sẽ chuyển trạng thái của cấu trúc đề sang <strong>Ngừng sử dụng (Deactivated)</strong>.
                Cấu trúc sẽ không còn hiển thị để tạo mới đề thi nhưng lịch sử trước đó được giữ lại.
              </span>
            ) : (
              <span>Bạn có chắc chắn muốn xóa vĩnh viễn cấu trúc đề thi nháp/từ chối này? Hành động này không thể hoàn tác.</span>
            )}
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            disabled={isMutating}
            onClick={() => setIsDeleteOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="destructive"
            disabled={isMutating}
            onClick={handleDeleteConfirm}
          >
            {isMutating
              ? "Đang xử lý..."
              : actions.canDeactivate
                ? "Ngừng sử dụng"
                : "Xác nhận Xóa"}
          </Button>
        </DialogFooter>
      </Dialog>

    </ExpertLayout>
  );
}
