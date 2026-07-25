import * as React from "react";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent } from "../../components/ui/dialog";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { cn } from "../../utils/cn";
import { questionBankApi } from "../../services/questionBankApi";
import LatexPreview from "../../components/expert/LatexPreview";
import { getQuestionStatusLabel, getQuestionStatusVariant, getQuestionTypeLabel, getQuestionPartTypeLabel, normalizeQuestionType, normalizeQuestionPartType } from "../../utils/questionLabels";

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


function parseSnapshot(snapshot, contentOverride, solutionOverride, imageOverride) {
  if (!snapshot) {
    return {
      rawType: "",
      grade: "",
      defaultWeight: "",
      topicsList: [],
      answersList: [],
      partsList: [],
      solutionExplanation: solutionOverride || "",
      pictureUrl: imageOverride || "",
      content: contentOverride || "",
      isParsed: false
    };
  }

  const parsed = safeParseJson(snapshot);
  if (!parsed || typeof parsed !== "object") {
    return {
      rawType: "",
      grade: "",
      defaultWeight: "",
      topicsList: [],
      answersList: [],
      partsList: [],
      solutionExplanation: solutionOverride || "",
      pictureUrl: imageOverride || "",
      content: contentOverride || "",
      isParsed: false
    };
  }

  // Extract Question Type (with normalization)
  const rawType = parsed.QuestionType || parsed.questionType || parsed.type || "";

  // V2 uses weight; V1 point fallbacks keep legacy snapshots readable.
  const grade = parsed.Grade || parsed.grade || "";
  const defaultWeight = parsed.DefaultWeight ?? parsed.defaultWeight
    ?? parsed.DefaultPoint ?? parsed.defaultPoint ?? parsed.points ?? "";

  // Extract Topics (array of objects with TagName or name)
  const rawTopics = parsed.Topics || parsed.topics || [];
  const topicsList = Array.isArray(rawTopics) ? rawTopics : [];

  // Extract Answers (Options)
  const rawAnswers = parsed.Answers || parsed.answers || parsed.options || [];
  const answersList = Array.isArray(rawAnswers) ? rawAnswers : [];

  // Extract Parts (for Composite questions)
  const rawParts = parsed.Parts || parsed.parts || [];
  const partsList = Array.isArray(rawParts) ? rawParts : [];

  // Solution explanation
  const solutionExplanation = parsed.solution_explanation || parsed.solutionContent || parsed.questionAnswer || solutionOverride || "";

  // Picture Url
  const pictureUrl = parsed.pictureUrl || parsed.PictureUrl || imageOverride || "";

  // Content
  const content = parsed.questionContent || parsed.content || contentOverride || "";

  return {
    rawType,
    grade,
    defaultWeight,
    topicsList,
    answersList,
    partsList,
    solutionExplanation,
    pictureUrl,
    content,
    isParsed: true
  };
}

function renderSnapshotDetails(parsedSnapshot) {
  if (!parsedSnapshot) {
    return (
      <div className="p-3 text-xs text-on-surface-variant bg-surface-container rounded-lg italic text-center">
        Không thể đọc dữ liệu chi tiết của phiên bản này.
      </div>
    );
  }

  const {
    rawType,
    grade,
    defaultWeight,
    topicsList,
    answersList,
    partsList,
    solutionExplanation,
    pictureUrl,
    content,
    isParsed
  } = parsedSnapshot;

  const normalizedType = normalizeQuestionType(rawType);
  const typeLabel = getQuestionTypeLabel(normalizedType);

  return (
    <div className="space-y-4 text-xs text-on-surface">
      {/* Show question content */}
      {content && (
        <div className="p-3 bg-surface-container rounded-lg text-sm text-on-surface break-words">
          <LatexPreview content={content} />
        </div>
      )}

      {/* Picture illustration */}
      {pictureUrl && (
        <div className="border border-whisper-border rounded-lg overflow-hidden max-w-md bg-surface-container">
          <img src={pictureUrl} alt="Hình ảnh minh họa" className="max-h-48 object-contain mx-auto" />
        </div>
      )}

      {/* If parser failed, render the Vietnamese fallback */}
      {!isParsed ? (
        <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-lg italic text-center">
          Không thể đọc dữ liệu chi tiết của phiên bản này.
        </div>
      ) : (
        <>
          {/* Classification pills row */}
          <div className="flex flex-wrap gap-2 items-center text-[10px]">
            <span className="font-bold text-on-surface-variant bg-surface px-2 py-0.5 rounded border border-whisper-border">
              {typeLabel}
            </span>
            {grade && (
              <span className="font-bold text-on-surface-variant bg-surface px-2 py-0.5 rounded border border-whisper-border">
                Lớp {grade}
              </span>
            )}
            {defaultWeight !== undefined && defaultWeight !== null && defaultWeight !== "" && (
              <span className="font-bold text-primary bg-primary/10 px-2 py-0.5 rounded">
                Trọng số {defaultWeight}
              </span>
            )}
          </div>

          {/* Topics */}
          {topicsList.length > 0 && (
            <div className="flex flex-wrap gap-1.5 items-center">
              <span className="font-semibold text-on-surface-variant mr-1">Chủ đề:</span>
              {topicsList.map((topic, i) => {
                const tagName = topic.TagName || topic.tagName || topic.name || topic.Name || "";
                const isPrimary = !!(topic.IsPrimary || topic.isPrimary || topic.primary);
                if (!tagName) return null;
                return (
                  <span
                    key={i}
                    className={cn(
                      "px-2 py-0.5 rounded-full text-[10px] border",
                      isPrimary
                        ? "bg-primary/5 border-primary/20 text-primary font-bold"
                        : "bg-surface-container border-whisper-border text-on-surface-variant"
                    )}
                  >
                    {tagName} {isPrimary && "★"}
                  </span>
                );
              })}
            </div>
          )}

          {/* Answers / Parts */}
          {normalizedType === "COMPOSITE" ? (
            <div className="space-y-3 pl-3 border-l border-whisper-border/60 ml-1">
              <h5 className="font-bold text-on-surface-variant uppercase tracking-wider text-[10px]">Mệnh đề câu hỏi:</h5>
              {partsList.map((p, idx) => {
                const label = p.PartLabel ?? p.partLabel ?? String.fromCharCode(97 + idx); // a, b, c
                const pType = p.PartType || p.partType || p.type || "";
                const pTypeLabel = getQuestionPartTypeLabel(pType);
                const pContent = p.PartContent || p.partContent || p.content || "";
                const pSolution = p.Explanation || p.explanation || p.SolutionContent || p.solutionContent || p.solution_explanation || "";

                // Part answers options
                const pAnswers = p.Answers || p.answers || p.options || [];

                // Correct answer extraction using ?? for booleans/numbers
                const correctBoolean = p.CorrectBoolean !== undefined ? p.CorrectBoolean : p.correctBoolean;
                const correctText = p.CorrectText !== undefined ? p.CorrectText : p.correctText;
                const correctNumeric = p.CorrectNumeric !== undefined ? p.CorrectNumeric : p.correctNumeric;
                const tolerance = p.NumericTolerance !== undefined ? p.NumericTolerance : p.numericTolerance;

                let hasCorrectAnswer = false;
                let correctAnswerJSX = null;

                if (correctBoolean !== undefined && correctBoolean !== null) {
                  hasCorrectAnswer = true;
                  correctAnswerJSX = (
                    <div className="mt-2 text-xs text-emerald-success font-semibold flex items-center gap-1.5">
                      <span className="font-bold">Đáp án đúng:</span>
                      <span>{correctBoolean ? "Đúng" : "Sai"}</span>
                    </div>
                  );
                } else if (correctText !== undefined && correctText !== null && String(correctText).trim() !== "") {
                  hasCorrectAnswer = true;
                  correctAnswerJSX = (
                    <div className="mt-2 text-xs text-emerald-success font-semibold flex items-start gap-1.5">
                      <span className="font-bold shrink-0">Đáp án đúng:</span>
                      <span className="break-words">
                        <LatexPreview content={String(correctText)} />
                      </span>
                    </div>
                  );
                } else if (correctNumeric !== undefined && correctNumeric !== null) {
                  hasCorrectAnswer = true;
                  const toleranceText = (tolerance !== undefined && tolerance !== null && Number(tolerance) > 0)
                    ? ` (sai số ± ${tolerance})`
                    : "";
                  correctAnswerJSX = (
                    <div className="mt-2 text-xs text-emerald-success font-semibold flex items-center gap-1.5">
                      <span className="font-bold">Đáp án đúng:</span>
                      <span>{correctNumeric}{toleranceText}</span>
                    </div>
                  );
                }

                return (
                  <div key={idx} className="space-y-1.5 py-3 first:pt-0 last:pb-0 border-b border-whisper-border/30 last:border-0 text-xs">
                    <div className="flex items-center gap-2">
                      <span className="font-bold text-primary text-[13px]">{label})</span>
                      <span className="text-[10px] text-on-surface-variant bg-surface px-1.5 py-0.5 rounded border border-whisper-border/50 font-semibold uppercase">
                        {pTypeLabel}
                      </span>
                    </div>
                    <div className="text-on-surface leading-relaxed break-words pl-1">
                      <LatexPreview content={pContent} />
                    </div>

                    {/* Part answers details */}
                    {pAnswers.length > 0 && (
                      <div className="space-y-1 mt-2 pl-2">
                        {pAnswers.map((opt, oIdx) => {
                          const optContent = opt.AnswerContent || opt.answerContent || opt.content || opt.text || "";
                          const optIsCorrect = !!(opt.IsCorrect || opt.isCorrect || opt.correct);
                          return (
                            <div key={oIdx} className="flex items-start gap-2 text-xs">
                              <span className={cn(
                                "w-4 h-4 rounded-full flex items-center justify-center text-[10px] font-bold border shrink-0",
                                optIsCorrect
                                  ? "bg-emerald-success/10 border-emerald-success text-emerald-success"
                                  : "bg-surface border-whisper-border text-on-surface-variant"
                              )}>
                                ✓
                              </span>
                              <span className={cn(
                                "break-words",
                                optIsCorrect ? "font-bold text-emerald-success" : "text-on-surface-variant"
                              )}>
                                <LatexPreview content={optContent} />
                              </span>
                            </div>
                          );
                        })}
                      </div>
                    )}

                    {/* Correct answer text for other part types */}
                    {pAnswers.length === 0 && hasCorrectAnswer && correctAnswerJSX}

                    {/* Part solution explanation */}
                    {pSolution && (
                      <div className="mt-2 text-[11px] text-on-surface-variant/80 italic pl-1 border-l-2 border-primary/20">
                        <span className="font-bold">Giải thích: </span>
                        <LatexPreview content={pSolution} />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            // Regular questions
            <div className="space-y-3">
              {answersList.length > 0 && (
                <div className="space-y-2">
                  <h5 className="font-bold text-on-surface-variant uppercase tracking-wider text-[10px]">Phương án lựa chọn:</h5>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                    {answersList.map((ans, idx) => {
                      const prefix = String.fromCharCode(65 + idx); // A, B, C...
                      const ansContent = ans.AnswerContent || ans.answerContent || ans.content || ans.text || "";
                      const ansIsCorrect = !!(ans.IsCorrect || ans.isCorrect || ans.correct);
                      return (
                        <div
                          key={idx}
                          className={cn(
                            "p-3 rounded-lg border flex items-start gap-2.5 transition-all text-xs break-words",
                            ansIsCorrect
                              ? "bg-emerald-success/5 border-emerald-success/40 text-emerald-success ring-1 ring-emerald-success/10"
                              : "bg-surface-container-lowest border-whisper-border text-on-surface"
                          )}
                        >
                          <span className={cn(
                            "w-5 h-5 rounded-full flex items-center justify-center font-bold text-[11px] shrink-0 border",
                            ansIsCorrect
                              ? "bg-emerald-success text-white border-transparent"
                              : "bg-surface border-whisper-border text-on-surface-variant"
                          )}>
                            {prefix}
                          </span>
                          <div className="flex-1 min-w-0">
                            <LatexPreview content={ansContent} />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Correct answer text (short answer) */}
              {answersList.length === 0 && (parsedSnapshot.correctAnswer || parsedSnapshot.CorrectAnswer || parsedSnapshot.correctValue || parsedSnapshot.CorrectValue) && (
                <div className="p-3 rounded-lg bg-emerald-success/5 border border-emerald-success/15 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[16px] text-emerald-success">check_circle</span>
                  <span className="text-xs font-bold text-emerald-success">Đáp án đúng:</span>
                  <span className="text-xs font-semibold text-on-surface">
                    {parsedSnapshot.correctAnswer || parsedSnapshot.CorrectAnswer || parsedSnapshot.correctValue || parsedSnapshot.CorrectValue}
                  </span>
                </div>
              )}
            </div>
          )}

          {/* Lời giải chi tiết */}
          {solutionExplanation && (
            <div className="mt-4 pt-3 border-t border-whisper-border space-y-1.5">
              <h5 className="font-bold text-on-surface flex items-center gap-1.5 text-xs">
                <span className="material-symbols-outlined text-[16px] text-primary">lightbulb</span>
                Lời giải chi tiết
              </h5>
              <div className="text-on-surface-variant leading-relaxed text-xs break-words pl-5">
                <LatexPreview content={solutionExplanation} />
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
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
              updatedAt: currentDetails.updatedTime || new Date().toISOString(),
              status: currentDetails.status || "APPROVED",
              content: currentDetails.questionContent,
              answersSnapshot: {
                type: currentDetails.questionType,
                grade: currentDetails.grade,
                defaultWeight: currentDetails.defaultWeight,
                topics: currentDetails.topics,
                answers: currentDetails.answers,
                parts: currentDetails.parts,
                solution_explanation: currentDetails.solutionContent,
                pictureUrl: currentDetails.pictureUrl
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
                    {(() => {
                      const parsed = parseSnapshot(currentVersion.answersSnapshot, currentVersion.content);
                      return renderSnapshotDetails(parsed);
                    })()}
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
                      {(() => {
                        const parsed = parseSnapshot(ver.answersSnapshot, ver.questionContent, ver.questionAnswer, ver.pictureUrl);
                        return renderSnapshotDetails(parsed);
                      })()}
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
