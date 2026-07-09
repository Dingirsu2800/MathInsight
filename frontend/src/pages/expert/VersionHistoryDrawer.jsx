import * as React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent } from "../../components/ui/dialog";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { cn } from "../../utils/cn";
import { questionBankApi } from "../../services/questionBankApi";
import LatexPreview from "../../components/expert/LatexPreview";
import { getQuestionStatusLabel, getQuestionStatusVariant } from "../../utils/questionLabels";

function formatExpertDisplay(version) {
  if (!version) return "Hệ thống";
  const name = version.expertName || version.expertDisplayName || version.updatedByName;
  if (name) return name;

  const id = version.expertId;
  if (!id) return "Hệ thống";

  if (id.length <= 12) return `ID: ${id}`;
  return `ID: ${id.slice(0, 8)}...${id.slice(-4)}`;
}

function safeParseJson(value) {
  if (!value) return null;
  if (typeof value === "object") return value;
  try {
    return JSON.parse(value);
  } catch (e) {
    return value;
  }
}

export default function VersionHistoryDrawer({ isOpen, onClose, questionId, questionTitle }) {
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(null);
  
  const [currentVersion, setCurrentVersion] = React.useState(null);
  const [historyVersions, setHistoryVersions] = React.useState([]);

  React.useEffect(() => {
    if (isOpen && questionId) {
      setLoading(true);
      setError(null);
      setCurrentVersion(null);
      setHistoryVersions([]);

      // Fetch current question details and history versions in parallel
      Promise.all([
        questionBankApi.getQuestionDetail(questionId)
          .then(res => res.data)
          .catch(err => {
            console.error("Failed to load current version detail:", err);
            return null;
          }),
        questionBankApi.getQuestionVersions(questionId)
          .then(res => res.data)
          .catch(err => {
            console.error("Failed to load history versions:", err);
            throw err;
          })
      ])
        .then(([currentDetails, versions]) => {
          if (currentDetails) {
            setCurrentVersion({
              expertId: currentDetails.expertId || "Hệ thống",
              expertName: currentDetails.expertName || "",
              updatedAt: currentDetails.updatedAt || new Date().toISOString(),
              status: currentDetails.status || "APPROVED",
              content: currentDetails.questionContent,
              answersSnapshot: {
                type: currentDetails.questionType,
                answers: currentDetails.answers,
                parts: currentDetails.parts,
                solution_explanation: currentDetails.solutionContent
              }
            });
          }

          // Format history versions
          const items = Array.isArray(versions) 
            ? versions 
            : (versions?.items && Array.isArray(versions.items) ? versions.items : []);
          
          setHistoryVersions(items);
          setLoading(false);
        })
        .catch((err) => {
          console.error("VersionHistoryDrawer load error:", err);
          const enableFallback = import.meta.env.VITE_ENABLE_MOCK_FALLBACK === "true";

          if (enableFallback) {
            setError("Lỗi kết nối API. Đang hiển thị dữ liệu lịch sử mẫu.");
            // mock data fallback
            setCurrentVersion({
              expertId: "EXP-9921",
              expertName: "Chuyên gia mẫu",
              updatedAt: "2026-07-08T14:30:00Z",
              status: "APPROVED",
              content: `Tính đạo hàm của hàm số y = \\sin(2x) tại điểm x = \\frac{\\pi}{4}.`,
              answersSnapshot: {
                type: "MULTIPLE_CHOICE",
                options: [
                  { id: "A", content: "y'(\\frac{\\pi}{4}) = 0", isCorrect: true },
                  { id: "B", content: "y'(\\frac{\\pi}{4}) = 1", isCorrect: false }
                ],
                solution_explanation: "Ta có y' = 2\\cos(2x)..."
              }
            });
            setHistoryVersions([
              {
                versionId: "mock-v2",
                expertId: "EXP-4402",
                expertName: "Chuyên gia mẫu",
                createdTime: "2026-07-07T09:15:00Z",
                status: "APPROVED",
                questionContent: `Tính đạo hàm của y = \\sin(2x) tại điểm x = \\frac{\\pi}{4}.`,
                answersSnapshot: JSON.stringify({
                  type: "MULTIPLE_CHOICE",
                  options: [
                    { id: "A", content: "y' = 0", isCorrect: true },
                    { id: "B", content: "y' = 1", isCorrect: false }
                  ]
                })
              }
            ]);
          } else {
            setError(
              err.response?.data?.message ||
              err.message ||
              "Không thể kết nối tải dữ liệu lịch sử phiên bản từ backend."
            );
          }
          setLoading(false);
        });
    }
  }, [isOpen, questionId]);

  return (
    <Dialog isOpen={isOpen} onClose={onClose} variant="drawer">
      <DialogHeader>
        <div className="flex items-center gap-2 mb-1">
          <Badge variant="primary">Q-{questionId}</Badge>
          <span className="text-[12px] text-on-surface-variant font-bold">LỊCH SỬ PHIÊN BẢN</span>
        </div>
        <DialogTitle className="text-xl">Lịch sử thay đổi câu hỏi</DialogTitle>
        <DialogDescription className="truncate max-w-lg">
          {questionTitle || "Quản lý và so sánh lịch sử cập nhật dữ liệu của câu hỏi."}
        </DialogDescription>
      </DialogHeader>

      <DialogContent className="py-4">
        {error && (
          <div className="p-3 mb-4 bg-error/10 border border-error/20 text-error rounded-lg text-xs font-semibold">
            {error}
          </div>
        )}

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
            <p className="text-sm text-on-surface-variant">Đang tải lịch sử phiên bản...</p>
          </div>
        ) : (
          <div className="relative pl-6 before:absolute before:left-[11px] before:top-2 before:bottom-2 before:w-[2px] before:bg-whisper-border space-y-8">
            
            {/* CURRENT VERSION CARD */}
            {currentVersion && (
              <div className="relative">
                <div className="absolute -left-[24px] top-1.5 flex items-center justify-center w-5 h-5 rounded-full border-2 bg-pure-surface shadow-sm z-10 border-primary text-primary">
                  <span className="material-symbols-outlined text-[12px] font-bold">star</span>
                </div>

                <div className="border rounded-xl p-5 bg-pure-surface relative shadow-sm border-primary/50 ring-1 ring-primary/20">
                  <div className="absolute -top-3 right-4 bg-primary text-on-primary font-bold text-[9px] uppercase tracking-wider px-2 py-0.5 rounded-full shadow-sm">
                    Hiện tại
                  </div>

                  <div className="flex flex-wrap justify-between items-center gap-2 mb-4 pb-3 border-b border-whisper-border">
                    <div className="space-y-1">
                      <p className="text-xs text-on-surface-variant flex items-center gap-1.5">
                        <span className="material-symbols-outlined text-[14px]">person</span>
                        Chuyên gia: <span className="font-bold text-on-surface">{formatExpertDisplay(currentVersion)}</span>
                      </p>
                      <p className="text-xs text-on-surface-variant flex items-center gap-1.5">
                        <span className="material-symbols-outlined text-[14px]">schedule</span>
                        {new Date(currentVersion.updatedAt).toLocaleString("vi-VN", {
                          day: "2-digit",
                          month: "2-digit",
                          year: "numeric",
                          hour: "2-digit",
                          minute: "2-digit"
                        })}
                      </p>
                    </div>
                    <Badge variant={getQuestionStatusVariant(currentVersion.status)}>{getQuestionStatusLabel(currentVersion.status)}</Badge>
                  </div>

                  <div className="space-y-4">
                    <div>
                      <h4 className="text-xs font-bold text-on-surface mb-1.5">Nội dung câu hỏi:</h4>
                      <div className="p-3 bg-surface-container rounded-lg text-sm text-on-surface break-words">
                        <LatexPreview content={currentVersion.content} />
                      </div>
                    </div>

                    <div>
                      <h4 className="text-xs font-bold text-on-surface mb-1.5">Snapshot kỹ thuật (JSON):</h4>
                      <pre className="text-[12px] font-mono bg-charcoal-ink text-inverse-on-surface p-3.5 rounded-lg overflow-x-auto border border-whisper-border/20 leading-relaxed shadow-inner max-h-48">
                        <code>{JSON.stringify(currentVersion.answersSnapshot, null, 2)}</code>
                      </pre>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* HISTORICAL VERSIONS */}
            {historyVersions.map((ver, idx) => {
              const snapshot = safeParseJson(ver.answersSnapshot);
              return (
                <div key={ver.versionId || idx} className="relative">
                  <div className="absolute -left-[24px] top-1.5 flex items-center justify-center w-5 h-5 rounded-full border-2 bg-pure-surface shadow-sm z-10 border-outline-variant text-on-surface-variant">
                    <span className="material-symbols-outlined text-[12px] font-bold">edit_document</span>
                  </div>

                  <div className="border border-whisper-border hover:border-outline-variant rounded-xl p-5 bg-pure-surface relative shadow-sm transition-all">
                    <div className="flex flex-wrap justify-between items-center gap-2 mb-4 pb-3 border-b border-whisper-border">
                      <div className="space-y-1">
                        <p className="text-xs text-on-surface-variant flex items-center gap-1.5">
                          <span className="material-symbols-outlined text-[14px]">person</span>
                          Chuyên gia: <span className="font-bold text-on-surface">{formatExpertDisplay(ver)}</span>
                        </p>
                        <p className="text-xs text-on-surface-variant flex items-center gap-1.5">
                          <span className="material-symbols-outlined text-[14px]">schedule</span>
                          {new Date(ver.createdTime).toLocaleString("vi-VN", {
                            day: "2-digit",
                            month: "2-digit",
                            year: "numeric",
                            hour: "2-digit",
                            minute: "2-digit"
                          })}
                        </p>
                      </div>

                      <div className="flex items-center gap-2">
                        <Badge variant={getQuestionStatusVariant(ver.status || "APPROVED")}>
                          {getQuestionStatusLabel(ver.status || "APPROVED")}
                        </Badge>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled
                          title="Tính năng khôi phục chưa được hỗ trợ trên API backend"
                          className="opacity-60 cursor-not-allowed hover:bg-transparent normal-case text-xs px-2 h-7"
                        >
                          <span className="material-symbols-outlined text-[14px] mr-1">restore</span>
                          Khôi phục
                        </Button>
                      </div>
                    </div>

                    <div className="space-y-4">
                      <div>
                        <h4 className="text-xs font-bold text-on-surface mb-1.5">Nội dung câu hỏi:</h4>
                        <div className="p-3 bg-surface-container rounded-lg text-sm text-on-surface break-words">
                          <LatexPreview content={ver.questionContent} />
                        </div>
                      </div>

                      {snapshot && (
                        <div>
                          <h4 className="text-xs font-bold text-on-surface mb-1.5">Snapshot kỹ thuật (JSON):</h4>
                          <pre className="text-[12px] font-mono bg-charcoal-ink text-inverse-on-surface p-3.5 rounded-lg overflow-x-auto border border-whisper-border/20 leading-relaxed shadow-inner max-h-48">
                            <code>{JSON.stringify(snapshot, null, 2)}</code>
                          </pre>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}

            {!currentVersion && historyVersions.length === 0 && !loading && (
              <p className="text-xs text-on-surface-variant text-center py-6">Không có dữ liệu lịch sử phiên bản nào.</p>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
