import * as React from "react";
import { useNavigate } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import { Badge } from "../../components/ui/badge";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { questionBankApi } from "../../services/questionBankApi";
import { getAccountId } from "../../services/authStorage";
import { mapQuestionDetailToViewModel } from "./questionMappers";
import { getQuestionTypeShortLabel, getQuestionStatusLabel, getQuestionStatusVariant, getQuestionPartTypeLabel } from "../../utils/questionLabels";
import LatexPreview from "../../components/expert/LatexPreview";
import { cn } from "../../utils/cn";

function getRoleLabel(role) {
  if (role === "Student") return "Học sinh";
  if (role === "Expert") return "Chuyên gia";
  if (role === "Admin") return "Quản trị viên";
  return role;
}

export default function ReportedQuestionsPage() {
  const navigate = useNavigate();
  const currentAccountId = getAccountId();

  const [loading, setLoading] = React.useState(false);
  const [questions, setQuestions] = React.useState([]);
  const [error, setError] = React.useState("");

  // Pagination
  const [pageIndex, setPageIndex] = React.useState(1);
  const [totalPages, setTotalPages] = React.useState(1);
  const [totalCount, setTotalCount] = React.useState(0);

  // Detail modal
  const [selectedQuestion, setSelectedQuestion] = React.useState(null);
  const [selectedQuestionDetails, setSelectedQuestionDetails] = React.useState(null);
  const [isPreviewOpen, setIsPreviewOpen] = React.useState(false);
  const [detailsLoading, setDetailsLoading] = React.useState(false);

  const fetchReportedQuestions = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await questionBankApi.getMyReportedQuestions({
        status: "Pending",
        pageIndex,
        pageSize: 10
      });
      // Handle standard paginated payload
      const items = res.data?.items || [];
      setQuestions(items);
      setTotalPages(res.data?.totalPages || 1);
      setTotalCount(res.data?.totalCount || items.length);
    } catch (err) {
      console.error(err);
      setError("Không thể tải danh sách báo cáo đang chờ xử lý.");
    } finally {
      setLoading(false);
    }
  };

  React.useEffect(() => {
    fetchReportedQuestions();
  }, [pageIndex]);

  const handleOpenPreview = (q) => {
    setSelectedQuestion(q);
    setIsPreviewOpen(true);
    setDetailsLoading(true);
    setSelectedQuestionDetails(null);

    questionBankApi.getQuestionDetail(q.questionId)
      .then((res) => {
        setSelectedQuestionDetails(mapQuestionDetailToViewModel(res.data));
        setDetailsLoading(false);
      })
      .catch((err) => {
        console.error(err);
        setError("Không thể tải thông tin chi tiết của câu hỏi này.");
        setIsPreviewOpen(false);
        setDetailsLoading(false);
      });
  };

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">

        {/* Page Header */}
        <DashboardPageHeader
          title="Câu hỏi bị báo cáo"
          subtitle="Quản lý các câu hỏi do bạn sở hữu nhận được phản hồi/báo cáo từ học sinh, chuyên gia khác hoặc quản trị viên."
        />

        {/* Reported Questions Data Table */}
        <div className="w-full bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr className="text-on-surface-variant uppercase text-[11px] font-bold tracking-wider">
                  <th className="py-3 px-4 max-w-md">Câu hỏi bị báo cáo</th>
                  <th className="py-3 px-4 w-40">Phân loại</th>
                  <th className="py-3 px-4 w-28 text-center">Báo cáo chờ</th>
                  <th className="py-3 px-4 max-w-xs">Lý do mới nhất</th>
                  <th className="py-3 px-4 w-32 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border bg-pure-surface text-[14px]">
                {loading ? (
                  <tr>
                    <td colSpan={5} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải danh sách báo cáo...</span>
                      </div>
                    </td>
                  </tr>
                ) : error ? (
                  <tr>
                    <td colSpan={5} className="py-20 text-center text-error font-semibold">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[32px]">error</span>
                        <span>{error}</span>
                        <Button variant="outline" size="sm" onClick={fetchReportedQuestions} className="mt-2">
                          Thử lại
                        </Button>
                      </div>
                    </td>
                  </tr>
                ) : questions.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">assignment_turned_in</span>
                        Tuyệt vời! Không có câu hỏi nào bị báo cáo.
                      </div>
                    </td>
                  </tr>
                ) : (
                  questions.map((q) => {
                    const qId = q.questionId;
                    const qContent = q.questionContent || "";
                    const pendingCount = q.pendingReportCount || 0;
                    const formattedTime = q.latestReportAt ? new Date(q.latestReportAt).toLocaleString("vi-VN") : "Chưa xác định";
                    const reporterRolesList = q.reporterRoles || [];

                    const activeStatuses = Array.isArray(q.activeReportStatuses)
                      ? q.activeReportStatuses
                      : ["Pending"];
                    const hasPendingOrPendingFix = activeStatuses.some(s => s === "Pending" || s === "PendingFix");
                    const hasPendingReview = activeStatuses.some(s => s === "PendingReview");

                    return (
                      <tr key={qId} className="hover:bg-surface-bright transition-all group duration-150">
                        <td className="py-4 px-4 max-w-md">
                          <div className="flex flex-wrap items-center gap-2 mb-1">
                            <span className="font-mono text-[10px] text-primary bg-primary/10 border border-primary/20 px-2 py-0.5 rounded font-bold">
                              Q-{qId}
                            </span>
                            {hasPendingOrPendingFix && (
                              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-amber-500/10 text-amber-600 border border-amber-500/20">
                                Cần xử lý
                              </span>
                            )}
                            {hasPendingReview && (
                              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-primary/10 text-primary border border-primary/20">
                                Đang chờ Admin duyệt
                              </span>
                            )}
                          </div>
                          <div className="font-semibold text-on-surface text-[13px] leading-relaxed mi-line-clamp-2" title={qContent}>
                            <LatexPreview content={qContent} />
                          </div>
                        </td>
                        <td className="py-4 px-4">
                          <div className="flex flex-col gap-1.5">
                            <span className="font-bold text-[13px] text-on-surface truncate max-w-[150px]" title={q.topics?.map(t => t.tagName || t.name).join(", ")}>
                              {q.topics?.map(t => t.tagName || t.name).join(", ") || q.topic || "Chưa phân loại"}
                            </span>
                            <div className="flex gap-2">
                              <span className="text-[10px] uppercase font-black tracking-wider text-on-surface-variant bg-surface px-2 py-0.5 rounded border border-whisper-border">
                                Lớp {q.grade || "12"}
                              </span>
                              <span className="text-[10px] uppercase font-black tracking-wider px-2.5 py-0.5 rounded bg-surface-container-high border border-whisper-border text-on-secondary-fixed">
                                {getQuestionTypeShortLabel(q.questionType)}
                              </span>
                            </div>
                          </div>
                        </td>
                        <td className="py-4 px-4 text-center font-mono font-bold text-deep-rose">
                          <span className="px-2 py-0.5 rounded bg-deep-rose/10 border border-deep-rose/20">
                            {pendingCount}
                          </span>
                        </td>
                        <td className="py-4 px-4 max-w-xs text-xs">
                          <div className="flex flex-wrap items-center gap-1.5 mb-1">
                            {reporterRolesList.map((role, idx) => (
                              <span key={idx} className="font-bold text-[9px] uppercase tracking-wider bg-deep-rose/10 border border-deep-rose/20 text-deep-rose px-1.5 py-0.5 rounded">
                                {getRoleLabel(role)}
                              </span>
                            ))}
                            {reporterRolesList.length === 0 && (
                              <span className="font-bold text-[9px] uppercase tracking-wider bg-deep-rose/10 border border-deep-rose/20 text-deep-rose px-1.5 py-0.5 rounded">
                                Báo cáo
                              </span>
                            )}
                            <span className="text-on-surface-variant/60 font-medium font-mono ml-1">{formattedTime}</span>
                          </div>
                          <p className="text-on-surface-variant font-medium leading-relaxed line-clamp-2" title={q.latestReportReason}>
                            {q.latestReportReason || "Không cung cấp lý do chi tiết."}
                          </p>
                        </td>
                        <td className="py-4 px-4 text-right">
                          <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-200">
                            <button
                              onClick={() => handleOpenPreview(q)}
                              className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                              aria-label="Xem chi tiết câu hỏi"
                              title="Xem chi tiết"
                            >
                              <span className="material-symbols-outlined text-[18px]">visibility</span>
                            </button>
                            {hasPendingOrPendingFix ? (
                              <button
                                onClick={() => navigate(`/expert/questions/${qId}/edit?from=reported`)}
                                className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                                aria-label="Xử lý báo cáo"
                                title="Xử lý báo cáo"
                              >
                                <span className="material-symbols-outlined text-[18px]">edit</span>
                              </button>
                            ) : (
                              <button
                                onClick={() => navigate(`/expert/questions/${qId}/edit?from=reported`)}
                                className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                                aria-label="Theo dõi trạng thái"
                                title="Theo dõi trạng thái"
                              >
                                <span className="material-symbols-outlined text-[18px]">rate_review</span>
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          <div className="bg-surface-container-low border-t border-whisper-border p-4 flex items-center justify-between">
            <span className="text-xs text-on-surface-variant font-bold">
              Hiển thị {questions.length} trong số {totalCount} câu hỏi bị báo cáo
            </span>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex(p => Math.max(1, p - 1))}
                disabled={pageIndex <= 1 || loading}
              >
                Trước
              </Button>
              <div className="flex items-center justify-center bg-pure-surface border border-whisper-border rounded px-3 text-xs font-bold select-none text-on-surface">
                {pageIndex} / {totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex(p => Math.min(totalPages, p + 1))}
                disabled={pageIndex >= totalPages || loading}
              >
                Tiếp
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* DETAILED PREVIEW DIALOG */}
      <Dialog isOpen={isPreviewOpen} onClose={() => setIsPreviewOpen(false)} variant="modal">
        {selectedQuestion && (
          <>
            <DialogHeader>
              <div className="flex items-center gap-2 mb-1">
                <Badge variant="primary">Q-{selectedQuestion.questionId}</Badge>
                <Badge variant={getQuestionStatusVariant(selectedQuestion.status)}>{getQuestionStatusLabel(selectedQuestion.status)}</Badge>
              </div>
              <DialogTitle>Chi tiết Câu hỏi</DialogTitle>
              <DialogDescription>Xem thông tin chi tiết cấu hình câu hỏi bị báo cáo.</DialogDescription>
            </DialogHeader>

            <DialogContent className="space-y-4">
              {detailsLoading ? (
                <div className="flex flex-col items-center justify-center py-10 gap-2">
                  <div className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                  <span className="text-xs text-on-surface-variant">Đang tải chi tiết câu hỏi...</span>
                </div>
              ) : selectedQuestionDetails ? (
                <>
                  {/* Question Content */}
                  <div>
                    <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Nội dung câu hỏi:</h4>
                    <div className="p-4 bg-surface-container rounded-xl text-[14px] leading-relaxed border border-whisper-border break-words">
                      <LatexPreview content={selectedQuestionDetails.content} />
                    </div>
                  </div>

                  {/* Image */}
                  {selectedQuestionDetails.pictureUrl && (
                    <div className="rounded-xl overflow-hidden border border-whisper-border">
                      <img src={selectedQuestionDetails.pictureUrl} alt="Hình minh họa" className="max-h-48 mx-auto object-contain" />
                    </div>
                  )}

                  {/* Attributes */}
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Khối lớp:</h4>
                      <p className="font-semibold text-on-surface text-[14px]">
                        Lớp {selectedQuestionDetails.grade} ({selectedQuestionDetails.difficulty})
                      </p>
                    </div>
                    <div>
                      <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Trọng số:</h4>
                      <p className="font-bold text-primary text-[14px]">
                        {selectedQuestionDetails.weight}
                      </p>
                    </div>
                  </div>

                  {/* Answers */}
                  <div>
                    <h4 className="text-xs font-bold text-on-surface-variant mb-2 uppercase tracking-wider">Đáp án & Lời giải:</h4>

                    {/* Single / Multiple Choice / True False options */}
                    {(selectedQuestionDetails.type === "SINGLE_CHOICE" || selectedQuestionDetails.type === "MULTIPLE_CHOICE" || selectedQuestionDetails.type === "TRUE_FALSE") && (
                      <div className="space-y-2 mb-4">
                        {(selectedQuestionDetails.answers || []).map((opt, idx) => (
                          <div
                            key={idx}
                            className={cn(
                              "p-3 rounded-lg border flex items-center justify-between text-[13px] transition-all",
                              opt.isCorrect
                                ? "bg-emerald-success/10 border-emerald-success/30 text-emerald-success font-bold"
                                : "bg-pure-surface border-whisper-border text-on-surface-variant"
                            )}
                          >
                            <div className="flex items-center gap-3">
                              <div className={cn(
                                "w-5 h-5 rounded-full flex items-center justify-center border font-bold text-[10px]",
                                opt.isCorrect ? "bg-emerald-success text-on-primary border-transparent" : "border-outline-variant text-on-surface-variant"
                              )}>
                                {String.fromCharCode(65 + idx)}
                              </div>
                              <span className="font-mono">{opt.answerContent || opt.content}</span>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Short Answer */}
                    {selectedQuestionDetails.type === "SHORT_ANSWER" && (
                      <div className="p-3 bg-surface-container rounded-lg border border-whisper-border font-mono text-[13px] text-primary font-bold mb-4">
                        Đáp án đúng: {selectedQuestionDetails.answers?.find(a => a.isCorrect)?.answerContent || selectedQuestionDetails.answers?.[0]?.answerContent || "Chưa thiết lập"}
                      </div>
                    )}

                    {/* Composite nested parts */}
                    {selectedQuestionDetails.type === "COMPOSITE" && (
                      <div className="space-y-3 mb-4">
                        {(selectedQuestionDetails.parts || []).map((part, idx) => (
                          <div key={idx} className="border border-whisper-border rounded-xl p-3 bg-canvas-white">
                            <div className="flex items-center justify-between mb-1.5">
                              <span className="text-[10px] font-black uppercase text-primary">Phần {part.partOrder || (idx + 1)}: {getQuestionPartTypeLabel(part.partType)}</span>
                              <Badge variant="outline" className="scale-90">Trọng số {part.defaultWeight}</Badge>
                            </div>
                            <div className="mb-2">
                              <LatexPreview content={part.partContent} />
                            </div>

                            {part.partType === "TRUE_FALSE" && (
                              <div className="flex gap-2">
                                <Badge variant={part.correctBoolean ? "approved" : "secondary"}>
                                  Đúng: {part.correctBoolean ? "Có" : "Không"}
                                </Badge>
                              </div>
                            )}

                            {part.partType === "SHORT_ANSWER" && (
                              <p className="text-[12px] text-emerald-success font-mono font-bold">
                                Đáp án đúng: <span className="underline">{part.correctText}</span>
                              </p>
                            )}

                            {part.partType === "NUMERIC_ANSWER" && (
                              <div className="space-y-0.5">
                                <p className="text-[12px] text-emerald-success font-mono font-bold">
                                  Số đúng: <span className="underline">{part.correctNumeric}</span>
                                </p>
                              </div>
                            )}

                            {part.explanation && (
                              <div className="text-[11px] text-on-surface-variant mt-1.5 border-t border-dashed border-whisper-border pt-1">
                                <span className="font-semibold mr-1">Lời giải phụ:</span>
                                <LatexPreview content={part.explanation} />
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Solution Explanation */}
                    {selectedQuestionDetails.solutionContent && (
                      <div className="mt-3">
                        <h5 className="text-[12px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Lời giải chi tiết:</h5>
                        <div className="p-3 bg-surface-container-high/60 rounded-lg text-[13px] text-on-surface font-medium leading-relaxed border border-whisper-border/30">
                          <LatexPreview content={selectedQuestionDetails.solutionContent} />
                        </div>
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <p className="text-sm text-error text-center py-4">Không thể tải thông tin chi tiết.</p>
              )}
            </DialogContent>

            <DialogFooter>
              <Button variant="outline" onClick={() => setIsPreviewOpen(false)} className="normal-case h-9 text-xs">
                Đóng
              </Button>
              {selectedQuestionDetails && selectedQuestionDetails.expertId === currentAccountId && (
                <Button
                  onClick={() => {
                    setIsPreviewOpen(false);
                    navigate(`/expert/questions/${selectedQuestion.questionId}/edit?from=reported`);
                  }}
                  disabled={detailsLoading}
                  className="normal-case h-9 text-xs"
                >
                  Khắc phục câu hỏi
                </Button>
              )}
            </DialogFooter>
          </>
        )}
      </Dialog>
    </ExpertLayout>
  );
}
