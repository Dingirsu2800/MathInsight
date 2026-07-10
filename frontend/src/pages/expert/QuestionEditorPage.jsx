import * as React from "react";
import { useParams, useNavigate, useLocation } from "react-router-dom";
import { cn } from "../../utils/cn";
import ExpertLayout from "./ExpertLayout";
import { Button } from "../../components/ui/button";
import { Badge } from "../../components/ui/badge";
import { CustomSelect } from "../../components/ui/custom-select";
import { questionBankApi } from "../../services/questionBankApi";
import { mapQuestionDetailToEditorState, mapEditorStateToCreateUpdateRequest, flattenTopicTree, normalizeTrueFalseOptions } from "./questionMappers";
import { getQuestionTypeLabel, getQuestionPartTypeLabel } from "../../utils/questionLabels";
import LatexPreview from "../../components/expert/LatexPreview";
import { uploadQuestionImage } from "../../services/cloudinaryUploadApi";

function getRoleLabel(role) {
  if (role === "Student") return "Học sinh";
  if (role === "Expert") return "Chuyên gia";
  if (role === "Admin") return "Quản trị viên";
  return role;
}

export default function QuestionEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const isEditMode = !!id;

  const searchParams = new URLSearchParams(location.search);
  const fromReported = searchParams.get("from") === "reported";

  const [hasSavedInSession, setHasSavedInSession] = React.useState(false);
  const [pendingReports, setPendingReports] = React.useState([]);
  const [reportsLoading, setReportsLoading] = React.useState(false);
  const [updatingReportId, setUpdatingReportId] = React.useState(null);
  const [reportsError, setReportsError] = React.useState("");



  // Metadata dropdowns
  const [difficultyList, setDifficultyList] = React.useState([]);
  const [topicList, setTopicList] = React.useState([]);

  // Unified Form state
  const [form, setForm] = React.useState({
    questionContent: "",
    solutionContent: "",
    pictureUrl: "",
    grade: 12,
    questionType: "SINGLE_CHOICE",
    difficultyId: "",
    defaultPoint: 0.2,
    topics: [], // Array of { tagId, isPrimary }
    options: [
      { content: "Đáp án A", isCorrect: true },
      { content: "Đáp án B", isCorrect: false },
      { content: "Đáp án C", isCorrect: false },
      { content: "Đáp án D", isCorrect: false }
    ],
    shortAnswer: "",
    parts: [] // Array of composite parts
  });

  // Page level loading/error states
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(null);
  const [isTopicPanelOpen, setIsTopicPanelOpen] = React.useState(false);
  const [isTopicPanelClosing, setIsTopicPanelClosing] = React.useState(false);
  const [infoMessage, setInfoMessage] = React.useState(null);
  const closeTopicPanelTimerRef = React.useRef(null);
  const errorRef = React.useRef(null);
  const drawerErrorRef = React.useRef(null);

  React.useEffect(() => {
    return () => {
      if (closeTopicPanelTimerRef.current) {
        window.clearTimeout(closeTopicPanelTimerRef.current);
      }
    };
  }, []);

  const closeTopicPanel = React.useCallback(() => {
    if (isTopicPanelClosing) return;
    setIsTopicPanelClosing(true);
    setError(null);

    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const closeDelay = prefersReducedMotion ? 0 : 180;

    closeTopicPanelTimerRef.current = window.setTimeout(() => {
      setIsTopicPanelOpen(false);
      setIsTopicPanelClosing(false);
    }, closeDelay);
  }, [isTopicPanelClosing]);

  const openTopicPanel = React.useCallback(() => {
    setIsTopicPanelClosing(false);
    setIsTopicPanelOpen(true);
  }, []);

  const showError = React.useCallback((msg) => {
    setError(msg);
    setInfoMessage(null);
    window.scrollTo({ top: 0, behavior: "smooth" });
    setTimeout(() => {
      if (drawerErrorRef.current) {
        drawerErrorRef.current.focus();
      } else if (errorRef.current) {
        errorRef.current.focus();
      }
    }, 100);
  }, []);

  // OCR state helper
  const [ocrImage, setOcrImage] = React.useState(null);
  const [ocrPreviewUrl, setOcrPreviewUrl] = React.useState("");

  // Cleanup OCR object URL to prevent memory leaks
  React.useEffect(() => {
    return () => {
      if (ocrPreviewUrl) URL.revokeObjectURL(ocrPreviewUrl);
    };
  }, [ocrPreviewUrl]);

  // Cloudinary Upload States and Handler
  const fileInputRef = React.useRef(null);
  const questionTextareaRef = React.useRef(null);
  const [uploading, setUploading] = React.useState(false);
  const [uploadError, setUploadError] = React.useState(null);

  // Local UI states for helper panels
  const [isMathHelperOpen, setIsMathHelperOpen] = React.useState(false);
  const [isOcrPanelOpen, setIsOcrPanelOpen] = React.useState(false);

  const handleFileChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadError(null);

    // Validate size (max 5MB)
    const maxSize = 5 * 1024 * 1024;
    if (file.size > maxSize) {
      setUploadError("Kích thước ảnh vượt quá giới hạn 5MB.");
      return;
    }

    // Validate MIME type (jpeg, png, webp)
    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    if (!allowedTypes.includes(file.type)) {
      setUploadError("Định dạng tệp không được hỗ trợ. Vui lòng tải ảnh dạng JPEG, PNG hoặc WEBP.");
      return;
    }

    setUploading(true);
    try {
      const data = await uploadQuestionImage(file);
      if (data && data.secure_url) {
        handleFieldChange("pictureUrl", data.secure_url);
      } else {
        throw new Error("Không lấy được link ảnh trả về từ Cloudinary.");
      }
    } catch (err) {
      console.error("Upload error details:", err);
      setUploadError(err.message || "Tải ảnh lên thất bại. Hãy thử lại.");
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  // Load difficulties on mount
  React.useEffect(() => {
    questionBankApi.getDifficulties()
      .then(res => {
        setDifficultyList(res.data || []);
      })
      .catch(err => {
        console.error("Failed to load difficulties tags list:", err);
      });
  }, []);

  const fetchPendingReports = async () => {
    if (!id) return;
    setReportsLoading(true);
    setReportsError("");
    try {
      const res = await questionBankApi.getQuestionReports(id, { status: "Pending" });
      setPendingReports(res.data || []);
    } catch (err) {
      console.error("Failed to load pending reports:", err);
      setReportsError("Không thể tải các báo cáo đang chờ xử lý từ máy chủ.");
    } finally {
      setReportsLoading(false);
    }
  };

  React.useEffect(() => {
    if (isEditMode && fromReported) {
      fetchPendingReports();
    }
  }, [id, fromReported]);

  const handleResolveReport = async (reportId, nextStatus) => {
    setUpdatingReportId(reportId);
    try {
      await questionBankApi.updateQuestionReportStatus(reportId, { status: nextStatus });
      const res = await questionBankApi.getQuestionReports(id, { status: "Pending" });
      const remaining = res.data || [];
      setPendingReports(remaining);
      if (remaining.length === 0) {
        navigate("/expert/questions/reported");
      }
    } catch (err) {
      console.error(err);
      showError("Không thể cập nhật trạng thái báo cáo này.");
    } finally {
      setUpdatingReportId(null);
    }
  };

  // Fetch topics whenever grade changes
  React.useEffect(() => {
    questionBankApi.getTopicTags(form.grade)
      .then(res => {
        const flattened = flattenTopicTree(res.data || []);
        setTopicList(flattened);

        // Auto-clean any parent topics or topics not matching this grade scope
        setForm(prev => {
          const validTopics = prev.topics.filter(topic => {
            const match = flattened.find(f => f.tagId === topic.tagId);
            return match && match.depth !== 0;
          });

          let normalizedTopics = validTopics;
          if (validTopics.length > 0) {
            const hasPrimary = validTopics.some(t => t.isPrimary);
            if (!hasPrimary) {
              normalizedTopics = validTopics.map((t, idx) => ({
                ...t,
                isPrimary: idx === 0
              }));
            }
          }
          return { ...prev, topics: normalizedTopics };
        });
      })
      .catch(err => {
        console.error("Failed to load topic tags:", err);
      });
  }, [form.grade]);

  // Load question detail if in Edit Mode
  React.useEffect(() => {
    if (isEditMode) {
      setLoading(true);
      setError(null);
      questionBankApi.getQuestionDetail(id)
        .then(res => {
          const detail = res.data;
          const mapped = mapQuestionDetailToEditorState(detail);
          setForm(mapped);
          setLoading(false);
        })
        .catch(err => {
          console.error("Failed to fetch question details for editing:", err);
          const enableFallback = import.meta.env.VITE_ENABLE_MOCK_FALLBACK === "true";

          if (enableFallback) {
            setError("Lỗi kết nối API. Hiển thị dữ liệu biên tập mẫu.");
            // mock fallback
            setForm({
              questionContent: "Tính tích phân sau: I = \\int_{0}^{1} x^2 dx",
              solutionContent: "Sử dụng công thức nguyên hàm cơ bản.",
              pictureUrl: "",
              grade: 12,
              questionType: "SINGLE_CHOICE",
              difficultyId: "diff-3",
              defaultPoint: 1.0,
              topics: [{ tagId: "tag-1", isPrimary: true }],
              options: [
                { content: "\\frac{1}{3}", isCorrect: true },
                { content: "1", isCorrect: false },
                { content: "0", isCorrect: false }
              ],
              shortAnswer: "",
              parts: []
            });
          } else {
            setError(
              err.response?.data?.message ||
              err.message ||
              "Không thể tải chi tiết câu hỏi từ máy chủ backend."
            );
          }
          setLoading(false);
        });
    }
  }, [id, isEditMode]);

  // Handle core field changes
  const handleFieldChange = (field, value) => {
    setForm(prev => {
      const updated = { ...prev, [field]: value };

      // If questionType changes, initialize options with appropriate structures
      if (field === "questionType") {
        if (value === "TRUE_FALSE") {
          updated.options = normalizeTrueFalseOptions(prev.options);
        } else if (value === "SINGLE_CHOICE" || value === "MULTIPLE_CHOICE") {
          updated.options = [
            { content: "Đáp án A", isCorrect: true },
            { content: "Đáp án B", isCorrect: false },
            { content: "Đáp án C", isCorrect: false },
            { content: "Đáp án D", isCorrect: false }
          ];
        } else if (value === "COMPOSITE" && updated.parts.length === 0) {
          updated.parts = [
            {
              partOrder: 1,
              partLabel: "a",
              partContent: "Mệnh đề a...",
              partType: "TRUE_FALSE",
              correctBoolean: true,
              correctText: null,
              correctNumeric: null,
              numericTolerance: null,
              explanation: "",
              defaultPoint: 0.05
            }
          ];
        }
      }
      return updated;
    });
  };

  // Options helpers
  const handleOptionContentChange = (index, value) => {
    setForm(prev => {
      const updated = [...prev.options];
      updated[index] = { ...updated[index], content: value };
      return { ...prev, options: updated };
    });
  };

  const handleOptionCorrectChange = (index, isRadio = true) => {
    setForm(prev => {
      const updated = prev.options.map((opt, idx) => {
        if (isRadio) {
          return { ...opt, isCorrect: idx === index };
        } else {
          return idx === index ? { ...opt, isCorrect: !opt.isCorrect } : opt;
        }
      });
      return { ...prev, options: updated };
    });
  };

  const handleAddOption = () => {
    setForm(prev => {
      const char = String.fromCharCode(65 + prev.options.length);
      return {
        ...prev,
        options: [...prev.options, { content: prev.questionType === "TRUE_FALSE" ? "Mệnh đề mới" : `${char}. `, isCorrect: false }]
      };
    });
  };

  const handleRemoveOption = (index) => {
    setForm(prev => ({
      ...prev,
      options: prev.options.filter((_, idx) => idx !== index)
    }));
  };

  // Composite parts helpers
  const handlePartFieldChange = (partIndex, field, value) => {
    setForm(prev => {
      const updated = [...prev.parts];
      updated[partIndex] = { ...updated[partIndex], [field]: value };
      return { ...prev, parts: updated };
    });
  };

  const handleAddCompositePart = () => {
    setForm(prev => {
      const nextIndex = prev.parts.length;
      return {
        ...prev,
        parts: [
          ...prev.parts,
          {
            partOrder: nextIndex + 1,
            partLabel: String.fromCharCode(97 + nextIndex),
            partContent: "Mệnh đề...",
            partType: "TRUE_FALSE",
            correctBoolean: true,
            correctText: null,
            correctNumeric: null,
            numericTolerance: null,
            explanation: "",
            defaultPoint: 0.05
          }
        ]
      };
    });
  };

  const handleRemoveCompositePart = (index) => {
    setForm(prev => {
      const filtered = prev.parts.filter((_, idx) => idx !== index);
      // Re-order and re-label parts
      const reordered = filtered.map((part, idx) => ({
        ...part,
        partOrder: idx + 1,
        partLabel: String.fromCharCode(97 + idx)
      }));
      return { ...prev, parts: reordered };
    });
  };

  // Multiple Topics + Primary Topic Handlers
  const handleToggleTopic = (tagId) => {
    setForm((prev) => {
      const exists = prev.topics.some((topic) => topic.tagId === tagId);

      if (exists) {
        const remaining = prev.topics.filter((topic) => topic.tagId !== tagId);
        const hasPrimary = remaining.some((topic) => topic.isPrimary);
        let normalizedRemaining = remaining;
        if (!hasPrimary && remaining.length > 0) {
          normalizedRemaining = remaining.map((t, idx) => ({
            ...t,
            isPrimary: idx === 0
          }));
        }
        return { ...prev, topics: normalizedRemaining };
      } else {
        const isPrimary = prev.topics.length === 0;
        return {
          ...prev,
          topics: [...prev.topics, { tagId, isPrimary }]
        };
      }
    });
  };

  const handleSetPrimaryTopic = (tagId) => {
    setForm((prev) => {
      const exists = prev.topics.some((topic) => topic.tagId === tagId);
      if (!exists) return prev;

      return {
        ...prev,
        topics: prev.topics.map((topic) => ({
          ...topic,
          isPrimary: topic.tagId === tagId,
        })),
      };
    });
  };

  const getTopicDisplayLabel = (topic) => {
    if (!topic) return "";
    const rawName = topic.name || topic.tagName || topic.displayName || topic.tagId || "";
    return rawName.replace(/^\s*Lớp\s*(10|11|12)\s*-\s*/i, "").trim();
  };


  // OCR file handler
  const handleOcrImageUpload = (e) => {
    const file = e.target.files[0];
    if (file) {
      setOcrImage(file);
      setOcrPreviewUrl(URL.createObjectURL(file));
    }
  };

  // Insert LaTeX at active cursor position (and replace selection if any)
  const handleInsertLatex = (latex) => {
    const textarea = questionTextareaRef.current;
    if (textarea) {
      const startPos = textarea.selectionStart;
      const endPos = textarea.selectionEnd;
      const text = form.questionContent;

      const newText = text.substring(0, startPos) + latex + text.substring(endPos, text.length);

      // Update form state
      setForm(prev => ({
        ...prev,
        questionContent: newText
      }));

      // Restore cursor position right after newly inserted text
      setTimeout(() => {
        textarea.focus();
        textarea.setSelectionRange(startPos + latex.length, startPos + latex.length);
      }, 0);
    } else {
      // Fallback: append
      setForm(prev => ({
        ...prev,
        questionContent: prev.questionContent + " " + latex
      }));
    }
  };

  // Form validator & submit
  const handleSaveQuestion = () => {
    setError(null);

    // Core fields validation
    if (!form.questionContent.trim()) {
      showError("Nội dung câu hỏi không được để trống!");
      return;
    }
    if (!form.difficultyId) {
      showError("Vui lòng chọn độ khó cho câu hỏi!");
      return;
    }
    if (form.topics.length === 0) {
      openTopicPanel();
      showError("Vui lòng chọn ít nhất 1 chủ đề kiến thức!");
      return;
    }
    const selectedParentTopic = form.topics.find((selectedTopic) => {
      const topic = topicList.find((item) => item.tagId === selectedTopic.tagId);
      return topic?.depth === 0;
    });

    if (selectedParentTopic) {
      openTopicPanel();
      showError("Vui lòng chỉ chọn chủ đề con, không chọn nhóm chủ đề cha.");
      return;
    }
    if (form.topics.filter((topic) => topic.isPrimary).length !== 1) {
      openTopicPanel();
      showError("Vui lòng chọn đúng 1 chủ đề chính cho câu hỏi!");
      return;
    }

    // Type-specific validations
    if (form.questionType === "SINGLE_CHOICE") {
      const correctCount = form.options.filter(o => o.isCorrect).length;
      if (correctCount !== 1) {
        showError("Câu hỏi SingleChoice cần có đúng 1 đáp án được đánh dấu là ĐÚNG!");
        return;
      }
    } else if (form.questionType === "MULTIPLE_CHOICE") {
      const correctCount = form.options.filter(o => o.isCorrect).length;
      if (correctCount < 1) {
        showError("Câu hỏi MultipleChoice cần chọn ít nhất 1 đáp án là ĐÚNG!");
        return;
      }
    } else if (form.questionType === "TRUE_FALSE") {
      const tfOptions = normalizeTrueFalseOptions(form.options);
      const correctCount = tfOptions.filter(o => o.isCorrect).length;
      if (correctCount !== 1) {
        showError("Câu hỏi Đúng/Sai cần chọn chính xác một đáp án đúng.");
        return;
      }
    } else if (form.questionType === "SHORT_ANSWER") {
      if (!form.shortAnswer.trim()) {
        showError("Vui lòng nhập chuỗi đáp án ngắn chính xác!");
        return;
      }
    } else if (form.questionType === "COMPOSITE") {
      if (form.parts.length === 0) {
        showError("Mẫu câu hỏi Composite cần chứa ít nhất 1 phần câu hỏi phụ!");
        return;
      }
      for (let i = 0; i < form.parts.length; i++) {
        const part = form.parts[i];
        if (!part.partContent.trim()) {
          showError(`Nội dung câu hỏi phụ phần (${part.partLabel}) không được để trống!`);
          return;
        }
        if (part.partType === "SHORT_ANSWER" && (!part.correctText || !part.correctText.trim())) {
          showError(`Vui lòng nhập đáp án cho câu hỏi phụ phần (${part.partLabel})!`);
          return;
        }
        if (part.partType === "NUMERIC_ANSWER" && (part.correctNumeric === null || part.correctNumeric === "")) {
          showError(`Vui lòng nhập đáp án số cho câu hỏi phụ phần (${part.partLabel})!`);
          return;
        }
      }
    }

    const payload = mapEditorStateToCreateUpdateRequest(form);

    setLoading(true);
    const saveRequest = isEditMode
      ? questionBankApi.updateQuestion(id, payload)
      : questionBankApi.createQuestion(payload);

    saveRequest
      .then(() => {
        if (fromReported) {
          setHasSavedInSession(true);
          setInfoMessage("Đã lưu câu hỏi thành công. Bây giờ bạn có thể giải quyết hoặc không chấp nhận các báo cáo.");
          setLoading(false);
          fetchPendingReports();
        } else {
          navigate("/expert/questions");
        }
      })
      .catch(err => {
        console.error("Failed to save question:", err);
        showError("Lưu câu hỏi thất bại: " + (err.response?.data?.message || err.message));
        setLoading(false);
      });
  };

  const primaryTopicId = form.topics.find((topic) => topic.isPrimary)?.tagId;
  const primaryTopic = topicList.find((topic) => topic.tagId === primaryTopicId);

  const selectedTopicLabels = form.topics.map((selectedTopic) => {
    const topic = topicList.find((item) => item.tagId === selectedTopic.tagId);
    return {
      ...selectedTopic,
      label: getTopicDisplayLabel(topic) || selectedTopic.tagId,
    };
  });

  return (
    <ExpertLayout>
      <div className="p-gutter bg-canvas-white relative min-h-screen">

        {/* Error alert banner */}
        {error && (
          <div
            ref={errorRef}
            tabIndex={-1}
            role="alert"
            aria-live="assertive"
            className="p-4 mb-6 bg-error/10 border border-error/20 text-error rounded-xl text-sm font-semibold flex items-center gap-2 outline-none"
          >
            <span className="material-symbols-outlined">error</span>
            <span>{error}</span>
          </div>
        )}

        {/* Info/Notification banner */}
        {infoMessage && (
          <div
            role="status"
            aria-live="polite"
            className="p-4 mb-6 bg-primary/10 border border-primary/20 text-primary rounded-xl text-sm font-semibold flex items-center gap-2 outline-none"
          >
            <span className="material-symbols-outlined">info</span>
            <span>{infoMessage}</span>
          </div>
        )}

        {/* Editor Page Header */}
        <div className="mb-8 flex flex-wrap justify-between items-end gap-4 border-b border-whisper-border pb-4">
          <div>
            <div className="flex items-center gap-2 mb-2">
              <span className="font-bold text-[10px] uppercase tracking-wider bg-surface-container-high text-primary px-2 py-0.5 rounded">
                SOẠN THẢO
              </span>
              <span className="font-bold text-[10px] uppercase tracking-wider bg-emerald-success/10 text-emerald-success px-2 py-0.5 rounded border border-emerald-success/20">
                {isEditMode ? "CẬP NHẬT" : "TẠO MỚI"}
              </span>
            </div>
            <h2 className="text-2xl font-bold text-on-background">
              {isEditMode ? `Chỉnh sửa câu hỏi` : "Tạo câu hỏi mới"}
            </h2>
            <p className="text-xs text-on-surface-variant mt-1">
              {isEditMode ? (
                <>
                  ID: <span className="font-mono bg-surface-container-high px-1.5 py-0.5 rounded text-[10px] text-primary font-bold">#Q-{id}</span>
                </>
              ) : (
                "ID: Sẽ cấp tự động"
              )} • Lưu lần cuối: Vừa xong
            </p>
          </div>
          <div className="flex gap-3">
            <Button variant="outline" className="normal-case h-9 text-xs active:scale-[0.98] transition-all duration-150" onClick={() => navigate("/expert/questions")}>Hủy</Button>
            <Button
              className="normal-case h-9 text-xs active:scale-[0.98] transition-all duration-150"
              onClick={handleSaveQuestion}
              disabled={loading}
            >
              {isEditMode ? "Cập nhật câu hỏi" : "Lưu câu hỏi"}
            </Button>
          </div>
        </div>

        {/* Bento Grid Layout */}
        <div className="grid grid-cols-12 gap-6 items-start">

          {/* Left Column: Editor Form */}
          <div className="col-span-12 lg:col-span-8 flex flex-col gap-6">

            {/* Content Area Container */}
            <div className="bg-pure-surface rounded-xl border border-whisper-border p-6 lg:p-8 diffused-shadow min-h-[500px] flex flex-col gap-6">
              {loading && !form.questionContent && (
                <div className="flex flex-col items-center justify-center py-20 gap-3">
                  <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                  <span>Đang tải thông tin câu hỏi...</span>
                </div>
              )}

              {/* 1. Thông tin phân loại */}
              <div className="bg-surface-container-lowest p-5 rounded-xl border border-outline-variant space-y-6 shadow-inner">
                <h3 className="text-xs font-black uppercase text-primary tracking-wider border-b border-whisper-border pb-2.5 flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[16px]">label</span>
                  Thông tin phân loại
                </h3>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
                  <div className="space-y-4">
                    {/* Grade Select */}
                    <div>
                      <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Khối lớp học</label>
                      <CustomSelect
                        value={form.grade?.toString() || "12"}
                        onValueChange={(val) => {
                          setForm(prev => ({
                            ...prev,
                            grade: parseInt(val),
                            topics: []
                          }));
                          setIsTopicPanelOpen(false);
                        }}
                        placeholder="Chọn khối lớp"
                        items={[
                          { value: "10", label: "Lớp 10" },
                          { value: "11", label: "Lớp 11" },
                          { value: "12", label: "Lớp 12" }
                        ]}
                      />
                    </div>

                    {/* Difficulty Select */}
                    <div>
                      <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Độ khó câu hỏi</label>
                      <CustomSelect
                        value={form.difficultyId || "NONE"}
                        onValueChange={(val) => handleFieldChange("difficultyId", val === "NONE" ? "" : val)}
                        placeholder="Chọn độ khó"
                        items={[
                          { value: "NONE", label: "Chọn độ khó" },
                          ...difficultyList.map(d => ({ value: d.difficultyId, label: d.difficultyName }))
                        ]}
                      />
                    </div>
                  </div>

                  <div className="space-y-4">
                    {/* Default Point Input */}
                    <div>
                      <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Điểm số mặc định</label>
                      <input
                        value={form.defaultPoint}
                        onChange={(e) => handleFieldChange("defaultPoint", parseFloat(e.target.value) || 0)}
                        className="w-full p-2.5 h-10 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-mono font-semibold outline-none"
                        type="number"
                        step="0.05"
                        min="0"
                      />
                    </div>

                    {/* Topic Picker Trigger */}
                    <div>
                      <div className="flex items-center justify-between mb-1.5">
                        <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">
                          Chủ đề kiến thức
                        </label>
                        <span className="text-[10px] text-on-surface-variant font-semibold">
                          Đã chọn {form.topics.length}
                        </span>
                      </div>

                      <button
                        type="button"
                        onClick={openTopicPanel}
                        className="w-full min-h-10 border border-outline-variant rounded-xl bg-pure-surface px-3 py-2 text-left flex items-center justify-between gap-3 hover:border-primary/60 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all duration-150 active:scale-[0.98] outline-none cursor-pointer"
                      >
                        <div className="min-w-0 flex-1">
                          {form.topics.length === 0 ? (
                            <span className="text-[13px] font-semibold text-on-surface-variant">
                              Chọn chủ đề cho lớp {form.grade}
                            </span>
                          ) : (
                            <>
                              <span className="text-[13px] font-bold text-on-surface truncate block">
                                {getTopicDisplayLabel(primaryTopic) || `${form.topics.length} chủ đề đã chọn`}
                              </span>
                              <span className="text-[10px] text-on-surface-variant">
                                Chủ đề chính • Tổng {form.topics.length} chủ đề
                              </span>
                            </>
                          )}
                        </div>

                        <span className="material-symbols-outlined text-[18px] text-primary">
                          edit
                        </span>
                      </button>

                      {selectedTopicLabels.length > 0 && (
                        <div className="flex flex-wrap gap-1.5 mt-2">
                          {selectedTopicLabels.map((selectedTopic) => (
                            <span
                              key={selectedTopic.tagId}
                              className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-bold mi-chip-in ${
                                selectedTopic.isPrimary
                                  ? "bg-primary text-white"
                                  : "bg-surface-container text-on-surface-variant border border-whisper-border"
                              }`}
                            >
                              {selectedTopic.label}
                              {selectedTopic.isPrimary && <span>(Chính)</span>}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* Question Type Segmented Control */}
                <div className="border-t border-whisper-border pt-4">
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-2">Dạng câu hỏi học thuật</label>
                  <div className="bg-surface-container-low p-1.5 rounded-xl flex flex-wrap gap-1 border border-whisper-border">
                    {["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "SHORT_ANSWER", "COMPOSITE"].map((type) => (
                      <button
                        key={type}
                        type="button"
                        onClick={() => handleFieldChange("questionType", type)}
                        className={`px-3 py-1.5 text-xs font-bold rounded-lg transition-all duration-150 active:scale-[0.98] cursor-pointer flex-1 text-center min-w-[120px] ${
                          form.questionType === type
                            ? "bg-primary text-white shadow-sm"
                            : "text-on-surface-variant hover:text-on-surface hover:bg-surface-container"
                        }`}
                      >
                        {getQuestionTypeLabel(type)}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* 2. Nội dung câu hỏi */}
              <div>
                <label className="block text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-2">NỘI DUNG CÂU HỎI (Hỗ trợ LaTeX)</label>
                <div className="border border-outline-variant rounded-xl overflow-hidden focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20 transition-all">
                  <div className="bg-surface-container-low border-b border-outline-variant p-2 flex gap-1.5 flex-wrap items-center">
                    <button type="button" onClick={() => handleInsertLatex("**đậm**")} className="p-1.5 rounded hover:bg-surface-container text-on-surface-variant cursor-pointer" title="Bold"><span className="material-symbols-outlined text-[18px]">format_bold</span></button>
                    <button type="button" onClick={() => handleInsertLatex("*nghiêng*")} className="p-1.5 rounded hover:bg-surface-container text-on-surface-variant cursor-pointer" title="Italic"><span className="material-symbols-outlined text-[18px]">format_italic</span></button>
                    <button type="button" onClick={() => handleInsertLatex("$\\int_{a}^{b} f(x) dx$")} className="p-1.5 rounded hover:bg-surface-container text-on-surface-variant cursor-pointer" title="Tích phân"><span className="material-symbols-outlined text-[18px]">functions</span></button>
                    <div className="w-px h-6 bg-outline-variant mx-1 self-center"></div>
                    <button type="button" onClick={() => handleInsertLatex("$\\sqrt{x^2 + y^2}$")} className="p-1.5 rounded hover:bg-surface-container text-on-surface-variant cursor-pointer" title="Căn thức"><span className="material-symbols-outlined text-[18px]">image</span></button>
                    <button type="button" onClick={() => handleInsertLatex("$\\frac{a}{b}$")} className="p-1.5 rounded hover:bg-surface-container text-on-surface-variant cursor-pointer" title="Phân số"><span className="material-symbols-outlined text-[18px]">table_chart</span></button>

                    <div className="w-px h-6 bg-outline-variant mx-1 self-center"></div>

                    <button
                      type="button"
                      onClick={() => {
                        setIsMathHelperOpen(!isMathHelperOpen);
                        setIsOcrPanelOpen(false);
                      }}
                      className={`px-2 py-1 rounded text-xs font-bold transition-all duration-150 flex items-center gap-1 cursor-pointer active:scale-[0.97] ${
                        isMathHelperOpen
                          ? "bg-primary text-white shadow-sm scale-[1.01]"
                          : "hover:bg-surface-container hover:-translate-y-0.5 text-primary bg-primary/5"
                      }`}
                    >
                      <span className="material-symbols-outlined text-[16px]">calculate</span>
                      Mã toán
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setIsOcrPanelOpen(!isOcrPanelOpen);
                        setIsMathHelperOpen(false);
                      }}
                      className={`px-2 py-1 rounded text-xs font-bold transition-all duration-150 flex items-center gap-1 cursor-pointer active:scale-[0.97] ${
                        isOcrPanelOpen
                          ? "bg-primary text-white shadow-sm scale-[1.01]"
                          : "hover:bg-surface-container hover:-translate-y-0.5 text-primary bg-primary/5"
                      }`}
                    >
                      <span className="material-symbols-outlined text-[16px]">photo_camera</span>
                      Quét công thức
                    </button>
                  </div>

                  {/* Math Helper Panel */}
                  {isMathHelperOpen && (
                    <div className="bg-surface-container-lowest border-b border-outline-variant p-4 space-y-4 mi-panel-down">
                      <div>
                        <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Mã toán nhanh:</h5>
                        <div className="flex flex-wrap gap-2">
                          {[
                            { label: "Tích phân", code: "\\int_{a}^{b} f(x) dx" },
                            { label: "Nguyên hàm", code: "\\int f(x) dx" },
                            { label: "Đạo hàm", code: "f'(x) = \\lim_{\\Delta x \\to 0} \\frac{\\Delta y}{\\Delta x}" },
                            { label: "Giới hạn", code: "\\lim_{x \\to x_0} f(x)" },
                            { label: "Phân số", code: "\\frac{a}{b}" },
                            { label: "Căn thức", code: "\\sqrt{x^2 + 1}" }
                          ].map((sym, idx) => (
                            <button
                              key={idx}
                              type="button"
                              onClick={() => handleInsertLatex(`$${sym.code}$`)}
                              className="flex items-center gap-2 bg-pure-surface hover:bg-surface-container border border-whisper-border px-2.5 py-1.5 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer"
                            >
                              <div className="scale-90 select-none">
                                <LatexPreview content={`$${sym.code}$`} />
                              </div>
                              <span className="text-[10px] text-on-surface-variant font-mono bg-surface-container-low px-1.5 py-0.5 rounded">
                                {sym.code}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>

                      <div>
                        <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Ký tự Hy Lạp:</h5>
                        <div className="flex flex-wrap gap-1.5">
                          {["\\alpha", "\\beta", "\\gamma", "\\delta", "\\theta", "\\lambda", "\\pi", "\\omega", "\\Delta"].map((sym, idx) => (
                            <button
                              key={idx}
                              type="button"
                              onClick={() => handleInsertLatex(`$${sym}$`)}
                              className="flex flex-col items-center justify-center bg-pure-surface hover:bg-surface-container border border-whisper-border min-w-[50px] py-1 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer text-center"
                            >
                              <div className="scale-100 select-none h-6 flex items-center justify-center">
                                <LatexPreview content={`$${sym}$`} />
                              </div>
                              <span className="text-[9px] text-on-surface-variant font-mono mt-0.5">
                                {sym}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}

                  {/* OCR Scanner Panel */}
                  {isOcrPanelOpen && (
                    <div className="bg-surface-container-lowest border-b border-outline-variant p-4 space-y-4 mi-panel-down">
                      <div className="p-3.5 bg-primary/5 border border-primary/10 rounded-lg space-y-1.5">
                        <h4 className="text-[11px] font-bold text-primary flex items-center gap-1.5">
                          <span className="material-symbols-outlined text-[16px]">info</span>
                          Quét công thức toán từ ảnh chụp (Mockup)
                        </h4>
                        <p className="text-[10px] text-on-surface-variant leading-relaxed">
                          Tải lên hình ảnh chứa công thức toán học để quét và tự động chuyển đổi thành mã LaTeX.
                        </p>
                      </div>

                      <div className="flex flex-col gap-3">
                        <label className="border border-dashed border-outline-variant rounded-xl p-4 text-center hover:border-primary transition-colors flex flex-col items-center gap-1 cursor-pointer bg-pure-surface">
                          <span className="material-symbols-outlined text-[24px] text-on-surface-variant">photo_camera</span>
                          <span className="text-[11px] font-bold text-on-surface">Chọn ảnh công thức toán</span>
                          <input
                            type="file"
                            accept="image/*"
                            onChange={handleOcrImageUpload}
                            className="hidden"
                          />
                        </label>

                        {ocrPreviewUrl && (
                          <div className="border border-whisper-border rounded-xl p-3 bg-pure-surface space-y-2 max-w-md">
                            <p className="text-[10px] font-bold text-on-surface-variant">Ảnh xem trước:</p>
                            <img src={ocrPreviewUrl} alt="OCR Preview" className="max-h-32 mx-auto object-contain rounded border border-whisper-border" />
                            <div className="flex gap-2 justify-end pt-1">
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  handleInsertLatex("$f(x) = x^2 - 2x + 1$");
                                  showError("Mockup: Đã chuyển đổi công thức toán trong ảnh thành LaTeX $f(x) = x^2 - 2x + 1$");
                                }}
                                className="normal-case h-7 text-[10px] px-2.5 font-bold cursor-pointer active:scale-[0.98] transition-all"
                              >
                                Chuyển sang LaTeX
                              </Button>
                              <Button
                                type="button"
                                variant="secondary"
                                size="sm"
                                onClick={() => {
                                  showError("Mockup: Tính năng upload ảnh thật đang chờ tích hợp API lưu trữ. Giao diện hiện tại giữ tệp này làm preview tạm thời.");
                                }}
                                className="normal-case h-7 text-[10px] px-2.5 font-bold cursor-pointer active:scale-[0.98] transition-all"
                              >
                                Lưu ảnh xem trước
                              </Button>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  )}

                  <textarea
                    ref={questionTextareaRef}
                    value={form.questionContent}
                    onChange={(e) => handleFieldChange("questionContent", e.target.value)}
                    className="w-full h-44 p-4 text-[14px] bg-transparent border-0 outline-none focus:outline-none focus:ring-0 focus:border-0 shadow-none resize-none font-mono"
                    placeholder="Nhập nội dung câu hỏi hoặc mã LaTeX... Ví dụ: \\int_{0}^{1} x^2 dx"
                  />
                </div>
              </div>

              {/* 3. Cấu hình đáp án */}
              <div className="space-y-6">
                <div className="flex flex-wrap justify-between items-center pb-4 border-b border-whisper-border gap-3">
                  <h3 className="text-base font-bold text-on-surface">Cấu hình đáp án</h3>
                  <p className="text-xs font-bold text-on-surface-variant bg-surface-container-high px-3 py-1.5 rounded-lg border border-whisper-border select-none">
                    Loại câu hỏi: <span className="text-primary font-extrabold">{getQuestionTypeLabel(form.questionType)}</span>
                  </p>
                </div>

                <div key={form.questionType} className="mi-answer-switch">
                  {/* Options List Form for SINGLE_CHOICE / MULTIPLE_CHOICE */}
                {(form.questionType === "SINGLE_CHOICE" || form.questionType === "MULTIPLE_CHOICE") && (
                  <div className="space-y-4">
                    {form.options.map((opt, idx) => (
                      <div
                        key={idx}
                        className={`flex items-center gap-4 p-4 border rounded-xl relative transition-all ${
                          opt.isCorrect
                            ? "bg-emerald-success/5 border-emerald-success/30 shadow-sm"
                            : "border-whisper-border bg-surface-container-lowest"
                        }`}
                      >
                        <div className="flex-1 min-w-[200px]">
                          <span className="absolute top-0 left-0 bg-secondary-container text-on-secondary-container font-bold text-[9px] px-2.5 py-0.5 rounded-tl-xl rounded-br-lg uppercase tracking-wide">
                            Phương án {String.fromCharCode(65 + idx)}
                          </span>
                          <input
                            value={opt.content}
                            onChange={(e) => handleOptionContentChange(idx, e.target.value)}
                            className="w-full bg-transparent border-b border-outline-variant/60 hover:border-outline-variant/80 focus:border-primary focus:ring-0 px-0 py-1 text-[14px] font-semibold font-mono outline-none transition-all duration-150"
                            type="text"
                            placeholder={`Nhập phương án ${String.fromCharCode(65 + idx)}`}
                          />
                        </div>

                        <div className="flex items-center gap-3 shrink-0 mt-2">
                          <label className="flex items-center gap-1.5 cursor-pointer text-xs font-bold select-none">
                            <input
                              type={form.questionType === "SINGLE_CHOICE" ? "radio" : "checkbox"}
                              checked={opt.isCorrect}
                              onChange={() => handleOptionCorrectChange(idx, form.questionType === "SINGLE_CHOICE")}
                              className={`w-4 h-4 text-emerald-success focus:ring-emerald-success cursor-pointer ${
                                form.questionType === "SINGLE_CHOICE" ? "" : "rounded"
                              }`}
                              name="sc_mc_answer"
                            />
                            <span className={opt.isCorrect ? "text-emerald-success font-black" : "text-on-surface-variant"}>ĐÚNG</span>
                          </label>

                          {form.options.length > 2 && (
                            <button
                              type="button"
                              onClick={() => handleRemoveOption(idx)}
                              className="text-on-surface-variant hover:text-deep-rose cursor-pointer p-1 active:scale-[0.9] transition-all"
                              title="Xóa phương án này"
                            >
                              <span className="material-symbols-outlined text-[18px]">delete</span>
                            </button>
                          )}
                        </div>
                      </div>
                    ))}

                    <div className="flex justify-end pt-2">
                      <button
                        type="button"
                        onClick={handleAddOption}
                        className="py-2.5 px-4 border border-primary/20 hover:border-primary text-primary hover:bg-primary/5 rounded-xl font-bold text-xs uppercase tracking-wider transition-all duration-150 active:scale-[0.98] flex items-center gap-1.5 cursor-pointer"
                      >
                        <span className="material-symbols-outlined text-[18px]">add</span>
                        Thêm phương án đáp án
                      </button>
                    </div>
                  </div>
                )}

                {/* Form for TRUE_FALSE */}
                {form.questionType === "TRUE_FALSE" && (
                  <div className="space-y-4">
                    <div className="bg-surface-container-lowest p-5 rounded-xl border border-outline-variant space-y-3 shadow-inner">
                      <label className="block text-xs font-bold text-on-surface-variant uppercase tracking-wider">
                        Đáp án đúng của phát biểu:
                      </label>
                      <div className="flex gap-6">
                        {[
                          { label: "Đúng", isCorrect: form.options[0]?.isCorrect },
                          { label: "Sai", isCorrect: form.options[1]?.isCorrect }
                        ].map((opt, idx) => (
                          <label key={idx} className="flex items-center gap-2.5 cursor-pointer text-sm font-bold select-none">
                            <input
                              type="radio"
                              checked={!!opt.isCorrect}
                              onChange={() => {
                                const updatedOptions = [
                                  { content: "Đúng", isCorrect: idx === 0 },
                                  { content: "Sai", isCorrect: idx === 1 }
                                ];
                                handleFieldChange("options", updatedOptions);
                              }}
                              className="w-4 h-4 text-emerald-success focus:ring-emerald-success cursor-pointer"
                              name="tf_answer"
                            />
                            <span className={opt.isCorrect ? "text-emerald-success font-extrabold" : "text-on-surface-variant"}>
                              {opt.label}
                            </span>
                          </label>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {/* Form for SHORT_ANSWER */}
                {form.questionType === "SHORT_ANSWER" && (
                  <div className="space-y-4">
                    <div>
                      <label className="block text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-2">Đáp án đúng chính xác:</label>
                      <input
                        value={form.shortAnswer}
                        onChange={(e) => handleFieldChange("shortAnswer", e.target.value)}
                        className="w-full p-3 text-[14px] bg-surface-container-lowest border border-outline-variant rounded-xl focus:ring-2 focus:ring-primary focus:border-primary transition-all font-mono font-bold"
                        placeholder="Nhập chuỗi đáp án đúng (ví dụ: 1/3 hoặc x=5)"
                        type="text"
                      />
                    </div>
                  </div>
                )}

                {/* Form for COMPOSITE */}
                {form.questionType === "COMPOSITE" && (
                  <div className="space-y-6">
                    {form.parts.map((part, pIdx) => (
                      <div key={pIdx} className="border border-whisper-border rounded-xl p-5 bg-surface-container-lowest relative space-y-4 shadow-inner">
                        {/* Part Index Row */}
                        <div className="flex justify-between items-center pb-2 border-b border-whisper-border">
                          <span className="text-xs font-black uppercase text-primary">Phần #{part.partOrder || (pIdx + 1)} ({part.partLabel})</span>
                          <div className="flex items-center gap-3">
                            <CustomSelect
                              value={part.partType}
                              onValueChange={(val) => handlePartFieldChange(pIdx, "partType", val)}
                              placeholder="Chọn dạng"
                              className="w-[140px] h-8 text-[11px] py-1 px-2"
                              items={[
                                { value: "TRUE_FALSE", label: "Đúng / Sai" },
                                { value: "SHORT_ANSWER", label: "Trả lời ngắn" },
                                { value: "NUMERIC_ANSWER", label: "Điền kết quả số" }
                              ]}
                            />
                            <button
                              type="button"
                              onClick={() => handleRemoveCompositePart(pIdx)}
                              className="text-on-surface-variant hover:text-deep-rose cursor-pointer p-1"
                              title="Xóa phần này"
                            >
                              <span className="material-symbols-outlined text-[18px]">delete</span>
                            </button>
                          </div>
                        </div>

                        {/* Part Question Content */}
                        <div>
                          <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Nội dung câu hỏi phụ:</label>
                          <div className="border border-outline-variant rounded-lg overflow-hidden focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20 transition-all">
                            <textarea
                              value={part.partContent}
                              onChange={(e) => handlePartFieldChange(pIdx, "partContent", e.target.value)}
                              className="w-full h-20 p-3 text-[13px] bg-pure-surface border-0 outline-none focus:outline-none focus:ring-0 focus:border-0 shadow-none font-mono resize-none"
                              placeholder="Nhập nội dung câu hỏi phụ..."
                            />
                          </div>
                        </div>

                        {/* Part Answers based on Type */}
                        {part.partType === "TRUE_FALSE" && (
                          <div className="flex gap-4 items-center bg-pure-surface p-3 rounded-lg border border-whisper-border">
                            <span className="text-xs font-bold text-on-surface-variant">Lựa chọn đúng:</span>
                            <label className="flex items-center gap-1.5 cursor-pointer text-xs">
                              <input
                                type="radio"
                                checked={part.correctBoolean === true || part.correctBoolean === "true"}
                                onChange={() => handlePartFieldChange(pIdx, "correctBoolean", true)}
                                className="w-4 h-4 text-emerald-success"
                                name={`composite_tf_${pIdx}`}
                              />
                              <span className="font-bold text-emerald-success">ĐÚNG</span>
                            </label>
                            <label className="flex items-center gap-1.5 cursor-pointer text-xs">
                              <input
                                type="radio"
                                checked={part.correctBoolean === false || part.correctBoolean === "false"}
                                onChange={() => handlePartFieldChange(pIdx, "correctBoolean", false)}
                                className="w-4 h-4 text-emerald-success"
                                name={`composite_tf_${pIdx}`}
                              />
                              <span className="font-bold text-error">SAI</span>
                            </label>
                          </div>
                        )}

                        {part.partType === "SHORT_ANSWER" && (
                          <div>
                            <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Đáp án chuỗi đúng:</label>
                            <input
                              value={part.correctText || ""}
                              onChange={(e) => handlePartFieldChange(pIdx, "correctText", e.target.value)}
                              className="w-full p-2 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-mono font-bold outline-none"
                              placeholder="Nhập đáp án text chính xác"
                              type="text"
                            />
                          </div>
                        )}

                        {part.partType === "NUMERIC_ANSWER" && (
                          <div className="grid grid-cols-2 gap-4">
                            <div>
                              <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Đáp án số đúng:</label>
                              <input
                                value={part.correctNumeric !== null ? part.correctNumeric : ""}
                                onChange={(e) => handlePartFieldChange(pIdx, "correctNumeric", e.target.value)}
                                className="w-full p-2 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-mono font-bold outline-none"
                                placeholder="Nhập giá trị số (VD: 3.14)"
                                type="number"
                                step="any"
                              />
                            </div>
                            <div>
                              <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Sai số cho phép (Tolerance):</label>
                              <input
                                value={part.numericTolerance !== null ? part.numericTolerance : ""}
                                onChange={(e) => handlePartFieldChange(pIdx, "numericTolerance", e.target.value)}
                                className="w-full p-2 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-mono outline-none"
                                placeholder="Nhập sai số (VD: 0.01)"
                                type="number"
                                step="any"
                              />
                            </div>
                          </div>
                        )}

                        <div className="grid grid-cols-2 gap-4">
                          <div>
                            <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Lời giải thích cho phần này:</label>
                            <input
                              value={part.explanation || ""}
                              onChange={(e) => handlePartFieldChange(pIdx, "explanation", e.target.value)}
                              className="w-full p-2 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all outline-none font-medium"
                              placeholder="Nhập giải thích ngắn"
                              type="text"
                            />
                          </div>
                          <div>
                            <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Điểm số cho phần này:</label>
                            <input
                              value={part.defaultPoint}
                              onChange={(e) => handlePartFieldChange(pIdx, "defaultPoint", e.target.value)}
                              className="w-full p-2 text-[13px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:border-primary focus:ring-2 focus:ring-primary/20 transition-all font-mono font-semibold outline-none"
                              placeholder="Điểm phụ"
                              type="number"
                              step="0.05"
                            />
                          </div>
                        </div>
                      </div>
                    ))}

                    <button
                      type="button"
                      onClick={handleAddCompositePart}
                      className="w-full py-3 border-2 border-dashed border-outline-variant rounded-xl text-on-surface-variant font-bold text-xs uppercase tracking-wider hover:border-primary hover:text-primary transition-all duration-150 active:scale-[0.98] flex justify-center items-center gap-2 cursor-pointer outline-none"
                    >
                      <span className="material-symbols-outlined text-[18px]">add</span>
                      Thêm phần câu hỏi phụ (Composite Part)
                    </button>
                  </div>
                )}
              </div>
            </div>

              {/* 4. Lời giải chi tiết */}
              <div>
                <label className="block text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-2">LỜI GIẢI CHI TIẾT</label>
                <div className="border border-outline-variant rounded-xl overflow-hidden focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20 transition-all">
                  <textarea
                    value={form.solutionContent}
                    onChange={(e) => handleFieldChange("solutionContent", e.target.value)}
                    className="w-full h-32 p-4 text-[14px] bg-transparent border-0 outline-none focus:outline-none focus:ring-0 focus:border-0 shadow-none resize-none font-mono"
                    placeholder="Nhập lời giải chi tiết giúp học sinh dễ dàng hiểu bài..."
                  />
                </div>
              </div>

              {/* 5. Hình ảnh minh họa */}
              <div>
                <label className="block text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-2">Hình ảnh minh họa</label>
                <div className="flex flex-col gap-3 p-4 border border-outline-variant bg-surface-container-lowest rounded-xl">

                  {/* Image Preview */}
                  {form.pictureUrl ? (
                    <div className="relative group max-w-xs border border-whisper-border rounded-lg overflow-hidden bg-surface-container-low">
                      <img
                        src={form.pictureUrl}
                        alt="Ảnh minh họa"
                        className="max-h-40 w-full object-contain mx-auto"
                      />
                      <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                        <button
                          type="button"
                          onClick={() => handleFieldChange("pictureUrl", "")}
                          className="bg-deep-rose text-white p-1.5 rounded-full hover:scale-105 active:scale-95 transition-all cursor-pointer"
                          title="Xóa ảnh"
                        >
                          <span className="material-symbols-outlined text-[18px]">delete</span>
                        </button>
                      </div>
                    </div>
                  ) : (
                    <div className="border border-dashed border-outline-variant rounded-lg p-5 flex flex-col items-center justify-center bg-pure-surface text-center">
                      <span className="material-symbols-outlined text-[28px] text-on-surface-variant mb-1">image</span>
                      <p className="text-[11px] text-on-surface-variant">Chưa có ảnh minh họa câu hỏi</p>
                    </div>
                  )}

                  {/* Controls */}
                  <div className="flex flex-wrap items-center gap-2.5">
                    <input
                      type="file"
                      ref={fileInputRef}
                      onChange={handleFileChange}
                      accept="image/jpeg,image/png,image/webp"
                      className="hidden"
                    />
                    <Button
                      type="button"
                      variant="primary"
                      size="sm"
                      disabled={uploading}
                      onClick={() => fileInputRef.current?.click()}
                      className="gap-1.5 cursor-pointer h-8 text-[11px] font-bold active:scale-[0.98] transition-all duration-150"
                    >
                      {uploading ? (
                        <>
                          <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                          Đang tải lên...
                        </>
                      ) : (
                        <>
                          <span className="material-symbols-outlined text-[16px]">upload</span>
                          Tải ảnh lên
                        </>
                      )}
                    </Button>

                    {form.pictureUrl && (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => handleFieldChange("pictureUrl", "")}
                        className="text-deep-rose border-deep-rose hover:bg-deep-rose/5 h-8 text-[11px] font-bold cursor-pointer active:scale-[0.98] transition-all duration-150"
                      >
                        Xóa ảnh
                      </Button>
                    )}

                    <span className="text-[10px] text-on-surface-variant font-medium">
                      Hỗ trợ: JPEG, PNG, WEBP (Tối đa 5MB)
                    </span>
                  </div>

                  {/* Error Message */}
                  {uploadError && (
                    <p className="text-[11px] text-deep-rose font-semibold bg-deep-rose/5 p-2 rounded border border-deep-rose/15 leading-relaxed">
                      {uploadError}
                    </p>
                  )}

                  {/* Manual Entry */}
                  <details className="mt-1">
                    <summary className="text-[10px] text-on-surface-variant hover:text-primary cursor-pointer transition-colors select-none">
                      Nhập URL ảnh thủ công
                    </summary>
                    <div className="mt-2">
                      <input
                        value={form.pictureUrl}
                        onChange={(e) => handleFieldChange("pictureUrl", e.target.value)}
                        className="w-full p-2.5 text-[12px] bg-pure-surface border border-outline-variant rounded-lg hover:border-outline-variant/80 focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all font-mono outline-none"
                        placeholder="https://example.com/image.png"
                        type="url"
                      />
                    </div>
                  </details>

                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Properties Summary & Live Preview */}
          <div className="col-span-12 lg:col-span-4 lg:sticky lg:top-6 lg:max-h-[calc(100vh-3rem)] lg:overflow-y-auto flex flex-col gap-6">

            {/* Pending Reports Panel */}
            {fromReported && (
              <div className="bg-pure-surface rounded-xl border border-error/20 p-5 lg:p-6 diffused-shadow shadow-sm">
                <h3 className="text-xs font-bold text-error mb-4 tracking-wider flex items-center gap-1.5 border-b border-error/10 pb-2.5 uppercase">
                  <span className="material-symbols-outlined text-[16px]">report</span>
                  BÁO CÁO ĐANG CHỜ XỬ LÝ ({pendingReports.length})
                </h3>

                {reportsError ? (
                  <div className="p-3 text-xs text-error bg-error/5 border border-error/10 rounded-lg text-center font-semibold">
                    <p className="mb-2">{reportsError}</p>
                    <Button variant="outline" size="sm" onClick={fetchPendingReports} className="text-[10px] h-7">Thử lại</Button>
                  </div>
                ) : reportsLoading && pendingReports.length === 0 ? (
                  <div className="py-4 text-center text-xs text-on-surface-variant flex items-center justify-center gap-2">
                    <div className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                    <span>Đang tải các báo cáo...</span>
                  </div>
                ) : pendingReports.length === 0 ? (
                  <div className="p-3 text-xs text-emerald-success bg-emerald-success/5 border border-emerald-success/15 rounded-lg text-center font-bold">
                    Không còn báo cáo nào đang chờ xử lý.
                  </div>
                ) : (
                  <div className="space-y-4 max-h-60 overflow-y-auto pr-1">
                    {pendingReports.map((rep) => {
                      const reportIdVal = rep.reportId || rep.id;
                      const time = rep.createdTime ? new Date(rep.createdTime).toLocaleString("vi-VN") : "Chưa rõ thời gian";
                      const isUpdatingThisReport = updatingReportId === reportIdVal;
                      const isAnyReportUpdating = updatingReportId !== null;

                      return (
                        <div key={reportIdVal} className="p-3 bg-error/5 border border-error/10 rounded-lg text-xs space-y-2">
                          <div className="flex justify-between items-center text-[10px] font-mono text-on-surface-variant/60">
                            <span className="font-bold text-error bg-error/10 px-1.5 py-0.5 rounded uppercase">
                              {getRoleLabel(rep.reporterRole || rep.role)}
                            </span>
                            <span>{time}</span>
                          </div>
                          <p className="text-on-surface font-medium leading-relaxed italic">
                            &ldquo;{rep.reportReason || rep.reason}&rdquo;
                          </p>
                          <div className="flex justify-end gap-2 pt-1 border-t border-error/10">
                            <button
                              type="button"
                              disabled={!hasSavedInSession || isAnyReportUpdating}
                              onClick={() => handleResolveReport(reportIdVal, "Resolved")}
                              className={cn(
                                "px-2.5 py-1 rounded text-[10px] font-bold transition-all border outline-none flex items-center justify-center min-w-[85px] h-7",
                                hasSavedInSession && !isAnyReportUpdating
                                  ? "bg-emerald-success text-white border-transparent hover:bg-emerald-success/90 cursor-pointer active:scale-95"
                                  : "bg-outline-variant/10 text-on-surface-variant/40 border-outline-variant/20 cursor-not-allowed"
                              )}
                              title={!hasSavedInSession ? "Hãy lưu câu hỏi trước khi xử lý báo cáo" : "Đánh dấu là đã khắc phục lỗi"}
                            >
                              {isUpdatingThisReport ? (
                                <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                              ) : (
                                "Đã khắc phục"
                              )}
                            </button>
                            <button
                              type="button"
                              disabled={!hasSavedInSession || isAnyReportUpdating}
                              onClick={() => handleResolveReport(reportIdVal, "Dismissed")}
                              className={cn(
                                "px-2.5 py-1 rounded text-[10px] font-bold transition-all border outline-none flex items-center justify-center min-w-[85px] h-7",
                                hasSavedInSession && !isAnyReportUpdating
                                  ? "bg-pure-surface text-on-surface-variant border-outline-variant hover:bg-surface-container cursor-pointer active:scale-95"
                                  : "bg-outline-variant/10 text-on-surface-variant/40 border-outline-variant/20 cursor-not-allowed"
                              )}
                              title={!hasSavedInSession ? "Hãy lưu câu hỏi trước khi xử lý báo cáo" : "Không chấp nhận báo cáo này"}
                            >
                              {isUpdatingThisReport ? (
                                <div className="w-3.5 h-3.5 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                              ) : (
                                "Không chấp nhận"
                              )}
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
                {!hasSavedInSession && !reportsError && pendingReports.length > 0 && (
                  <p className="text-[10px] text-on-surface-variant/75 mt-3 italic leading-relaxed text-center">
                    * Các nút xử lý báo cáo sẽ hoạt động sau khi bạn ấn &ldquo;Lưu câu hỏi&rdquo; thành công ít nhất một lần.
                  </p>
                )}
              </div>
            )}

            {/* Meta Properties Summary Card */}
            <div className="bg-pure-surface rounded-xl border border-whisper-border p-5 lg:p-6 diffused-shadow">
              <h3 className="text-xs font-bold text-on-surface-variant mb-4 tracking-wider flex items-center gap-1.5 border-b border-whisper-border pb-2.5 uppercase">
                <span className="material-symbols-outlined text-[16px]">tune</span>
                THUỘC TÍNH CÂU HỎI
              </h3>

              <div className="space-y-4">
                <div>
                  <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Loại câu hỏi:</label>
                  <p className="font-bold text-[14px] text-primary">{getQuestionTypeLabel(form.questionType)}</p>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Độ khó:</label>
                    <p className="font-semibold text-[13px] text-on-surface">
                      {difficultyList.find(d => d.difficultyId === form.difficultyId)?.difficultyName || "Chưa chọn"}
                    </p>
                  </div>
                  <div>
                    <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Khối lớp:</label>
                    <p className="font-semibold text-[13px] text-on-surface">Lớp {form.grade}</p>
                  </div>
                </div>
                <div>
                  <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Chủ đề chính:</label>
                  <p className="font-semibold text-[13px] text-on-surface truncate">
                    {primaryTopic ? getTopicDisplayLabel(primaryTopic) : "Chưa chọn"}
                  </p>
                </div>
                <div>
                  <label className="block text-[11px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Điểm mặc định:</label>
                  <p className="font-bold text-[14px] text-on-surface font-mono">{form.defaultPoint} điểm</p>
                </div>
              </div>
            </div>

            {/* Live Preview Panel */}
            <div className="glass-panel rounded-xl p-5 lg:p-6 relative overflow-hidden flex flex-col h-[400px]">
              <div className="absolute top-0 right-0 w-32 h-32 bg-primary/5 rounded-full blur-2xl -mr-10 -mt-10"></div>

              <h3 className="text-xs font-bold text-primary mb-4 tracking-wider flex items-center gap-1.5 relative z-10 uppercase">
                <span className="material-symbols-outlined text-[16px]">visibility</span>
                XEM TRƯỚC (LIVE PREVIEW)
              </h3>

              <div className="flex-1 bg-pure-surface rounded-xl border border-whisper-border p-4 overflow-y-auto relative z-10 diffused-shadow">
                <div className="text-[13px] text-on-surface space-y-4 leading-relaxed font-medium">
                  <div>
                    <span className="font-bold text-primary mr-1">Nội dung câu hỏi:</span>
                    <div className="p-3 bg-surface-container rounded-lg text-[13px] text-on-surface break-words mt-1">
                      {form.questionContent ? <LatexPreview content={form.questionContent} /> : <span className="italic text-on-surface-variant font-body">Đang nhập nội dung...</span>}
                    </div>
                  </div>

                  {(form.pictureUrl || ocrPreviewUrl) && (
                    <div className="rounded-lg overflow-hidden border border-whisper-border">
                      <img src={form.pictureUrl || ocrPreviewUrl} alt="Minh họa" className="max-h-32 mx-auto object-contain" />
                    </div>
                  )}

                  {/* Preview based on Question Type */}
                  <div>
                    <span className="font-bold text-primary">Các lựa chọn trả lời:</span>

                    {/* SINGLE_CHOICE / MULTIPLE_CHOICE / TRUE_FALSE */}
                    {(form.questionType === "SINGLE_CHOICE" || form.questionType === "MULTIPLE_CHOICE" || form.questionType === "TRUE_FALSE") && (
                      <div className="space-y-2 mt-1.5">
                        {(form.questionType === "TRUE_FALSE" ? normalizeTrueFalseOptions(form.options) : form.options).map((opt, oIdx) => (
                          <div
                            key={oIdx}
                            className={`p-2 border rounded-lg flex items-center justify-between text-[12px] ${
                              opt.isCorrect
                                ? "border-emerald-success bg-emerald-success/5 text-emerald-success font-semibold"
                                : "border-whisper-border text-on-surface-variant"
                            }`}
                          >
                            <div className="flex items-center gap-2">
                              <div className={`w-4 h-4 rounded-full border flex items-center justify-center text-[10px] ${
                                opt.isCorrect ? "bg-emerald-success border-transparent text-white" : "border-outline-variant"
                              }`}>
                                {String.fromCharCode(65 + oIdx)}
                              </div>
                              <span className="font-mono">{opt.content}</span>
                            </div>
                            {form.questionType === "TRUE_FALSE" && (
                              <Badge variant={opt.isCorrect ? "approved" : "secondary"} className="scale-90 origin-right">
                                {opt.isCorrect ? "Đúng" : "Sai"}
                              </Badge>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* SHORT_ANSWER */}
                    {form.questionType === "SHORT_ANSWER" && (
                      <div className="p-2 bg-surface-container rounded-lg border border-whisper-border font-mono text-[12px] text-primary font-bold mt-1.5 text-center">
                        Đáp án đúng: {form.shortAnswer || <span className="italic text-on-surface-variant font-body">Trống</span>}
                      </div>
                    )}

                    {/* COMPOSITE */}
                    {form.questionType === "COMPOSITE" && (
                      <div className="space-y-2 mt-1.5">
                        {form.parts.map((part, pIdx) => (
                          <div key={pIdx} className="p-2 border border-whisper-border bg-surface-container-low rounded-lg text-[12px] space-y-1">
                            <div className="flex justify-between items-center">
                              <span className="font-black text-[9px] uppercase text-primary">Phần {part.partLabel}: {getQuestionPartTypeLabel(part.partType)}</span>
                              <span className="text-[9px] font-bold text-on-surface-variant">{part.defaultPoint} đ</span>
                            </div>
                            <div className="text-[12px] mt-1">
                              <LatexPreview content={part.partContent} />
                            </div>
                            {part.partType === "TRUE_FALSE" && (
                              <p className="text-[10px] text-emerald-success font-bold font-mono">
                                Đáp án: {part.correctBoolean === true || part.correctBoolean === "true" ? "ĐÚNG" : "SAI"}
                              </p>
                            )}
                            {part.partType === "SHORT_ANSWER" && (
                              <p className="text-[10px] text-emerald-success font-bold font-mono">
                                Đáp án: {part.correctText}
                              </p>
                            )}
                            {part.partType === "NUMERIC_ANSWER" && (
                              <p className="text-[10px] text-emerald-success font-bold font-mono">
                                Số đúng: {part.correctNumeric} (±{part.numericTolerance})
                              </p>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {form.solutionContent && (
                      <div className="mt-4 border-t border-dashed border-whisper-border pt-3">
                        <span className="font-bold text-primary mr-1">Lời giải chi tiết:</span>
                        <div className="p-3 bg-surface-container rounded-lg text-[13px] text-on-surface break-words mt-1">
                          <LatexPreview content={form.solutionContent} />
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>

          </div>

        </div>

      </div>

      {/* Right-side Slide-over Panel */}
      {isTopicPanelOpen && (
        <div className={`fixed inset-0 z-50 flex justify-end ${isTopicPanelClosing ? "mi-backdrop-out" : "mi-backdrop-in"}`}>
          <button
            type="button"
            aria-label="Đóng chọn chủ đề"
            className="absolute inset-0 bg-black/30 cursor-default border-0 outline-none"
            onClick={closeTopicPanel}
          />

          <section
            id="topic-drawer-section"
            className={`relative z-10 h-full w-full max-w-xl bg-pure-surface border-l border-whisper-border diffused-shadow flex flex-col ${isTopicPanelClosing ? "mi-drawer-out" : "mi-drawer-in"}`}
          >
            {/* Header */}
            <header className="px-5 py-4 border-b border-whisper-border flex items-start justify-between gap-3">
              <div>
                <p className="text-[10px] font-black uppercase tracking-wider text-primary">
                  Chủ đề kiến thức
                </p>
                <h3 className="text-lg font-bold text-on-surface">
                  Chọn chủ đề cho lớp {form.grade}
                </h3>
                <p className="text-xs text-on-surface-variant mt-1">
                  Chọn một hoặc nhiều chủ đề con và đặt đúng một chủ đề làm chính.
                </p>
              </div>

              <button
                type="button"
                onClick={closeTopicPanel}
                className="p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer"
                aria-label="Đóng"
              >
                <span className="material-symbols-outlined">close</span>
              </button>
            </header>

            {/* Error banner inside Drawer */}
            {error && (
              <div className="px-5 pt-3">
                <div
                  ref={drawerErrorRef}
                  tabIndex={-1}
                  role="alert"
                  aria-live="assertive"
                  className="p-3 bg-error/10 border border-error/20 text-error rounded-xl text-xs font-semibold flex items-center gap-2 outline-none"
                >
                  <span className="material-symbols-outlined text-[14px]">error</span>
                  <span>{error}</span>
                </div>
              </div>
            )}

            {/* Selected Summary */}
            <div className="px-5 py-3 border-b border-whisper-border bg-surface-container-lowest">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                  Đã chọn {form.topics.length}
                </span>
                <span className="text-[11px] text-on-surface-variant">
                  Chủ đề chính: {getTopicDisplayLabel(primaryTopic) || "Chưa chọn"}
                </span>
              </div>

              {selectedTopicLabels.length === 0 ? (
                <p className="text-xs text-on-surface-variant font-semibold">
                  Chưa có chủ đề nào được chọn.
                </p>
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {selectedTopicLabels.map((selectedTopic) => (
                    <span
                      key={selectedTopic.tagId}
                      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[10px] font-bold mi-chip-in ${
                        selectedTopic.isPrimary
                          ? "bg-primary text-white"
                          : "bg-surface-container text-on-surface-variant border border-whisper-border"
                      }`}
                    >
                      {selectedTopic.label}
                      {selectedTopic.isPrimary && <span>(Chính)</span>}
                    </span>
                  ))}
                </div>
              )}
            </div>

            {/* Topic Tree List */}
            <div className="flex-1 overflow-y-auto p-5">
              <div className="border border-outline-variant rounded-xl bg-pure-surface divide-y divide-whisper-border overflow-hidden">
                {topicList.length === 0 ? (
                  <div className="p-4 text-sm text-on-surface-variant">
                    Chưa có chủ đề cho khối lớp này.
                  </div>
                ) : (
                  topicList.map((topic) => {
                    const isParentTopic = topic.depth === 0;

                    if (isParentTopic) {
                      return (
                        <div
                          key={topic.tagId}
                          className="sticky top-0 z-10 bg-surface-container-low px-3 py-2 text-[10px] font-black uppercase text-primary border-b border-whisper-border select-none"
                        >
                          {getTopicDisplayLabel(topic)}
                        </div>
                      );
                    }

                    const isSelected = form.topics.some((selected) => selected.tagId === topic.tagId);
                    const isPrimary = form.topics.some(
                      (selected) => selected.tagId === topic.tagId && selected.isPrimary
                    );

                    return (
                      <div
                        key={topic.tagId}
                        className={`flex items-center gap-3 p-3 pl-5 text-[13px] transition-colors duration-150 ${
                          isSelected ? "bg-primary/5" : "hover:bg-surface-container-low"
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => handleToggleTopic(topic.tagId)}
                          className="h-4 w-4 accent-primary cursor-pointer"
                        />

                        <button
                          type="button"
                          onClick={() => handleToggleTopic(topic.tagId)}
                          className="flex-1 text-left font-semibold text-on-surface truncate cursor-pointer"
                          title={getTopicDisplayLabel(topic)}
                        >
                          {getTopicDisplayLabel(topic)}
                        </button>

                        {isSelected && (
                          <button
                            type="button"
                            onClick={() => handleSetPrimaryTopic(topic.tagId)}
                            className={`px-2.5 py-1.5 rounded-lg text-[10px] font-bold border transition-all duration-150 active:scale-[0.98] ${
                              isPrimary
                                ? "bg-primary text-white border-primary cursor-default"
                                : "text-primary border-primary/30 hover:bg-primary/10 hover:scale-[1.02] cursor-pointer"
                            }`}
                            title="Đặt làm chủ đề chính"
                          >
                            {isPrimary ? "Chính" : "Đặt chính"}
                          </button>
                        )}
                      </div>
                    );
                  })
                )}
              </div>
            </div>

            {/* Footer */}
            <footer className="px-5 py-4 border-t border-whisper-border bg-pure-surface flex items-center justify-between gap-3">
              <p className="text-xs text-on-surface-variant font-semibold">
                {form.topics.length === 0
                  ? "Chọn ít nhất một chủ đề con."
                  : `Đã chọn ${form.topics.length} chủ đề.`}
              </p>

              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={closeTopicPanel}
                  className="px-4 py-2 rounded-lg bg-primary text-white text-[12px] font-bold hover:bg-primary/90 transition-colors cursor-pointer"
                >
                  Hoàn tất
                </button>
              </div>
            </footer>
          </section>
        </div>
      )}

    </ExpertLayout>
  );
}
