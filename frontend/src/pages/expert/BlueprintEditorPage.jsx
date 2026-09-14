import React, { useState, useEffect } from "react";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import { CustomSelect } from "../../components/ui/custom-select";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import BlueprintTopicPicker from "../../components/expert/BlueprintTopicPicker";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { questionBankApi } from "../../services/questionBankApi";
import {
  detailToEditorState,
  editorStateToBlueprintRequest,
  defaultScoringRule,
  generateClientId,
  isDetailRowComplete,
  hasIncompleteAllocationRows,
  editorStateToAvailabilityRequest
} from "../../utils/blueprintMappers";
import { validateBlueprint, validateBlueprintForDraft, validateBlueprintForSubmit } from "../../utils/blueprintValidation";
import { getBlueprintErrorMessage } from "../../utils/blueprintErrorLocalizer";
import { getBlueprintActions } from "../../utils/blueprintAuth";
import { getQuestionTypeLabel } from "../../utils/blueprintLabels";
import { flattenTopicTree } from "./questionMappers";
import { getAccountId } from "../../services/authStorage";
import { cn } from "../../utils/cn";

export default function BlueprintEditorPage() {
  const navigate = useNavigate();
  const { blueprintId } = useParams();
  const location = useLocation();
  const currentAccountId = getAccountId();

  // Track draft identity created in this session or loaded from route
  const [persistedBlueprintId, setPersistedBlueprintId] = useState(blueprintId || null);
  const persistedBlueprintIdRef = React.useRef(blueprintId || null);
  const loadedBlueprintIdRef = React.useRef(null);
  const activeBlueprintId = blueprintId || persistedBlueprintId;
  const isEditMode = Boolean(activeBlueprintId);

  useEffect(() => {
    if (blueprintId) {
      persistedBlueprintIdRef.current = blueprintId;
      setPersistedBlueprintId(blueprintId);
    } else {
      persistedBlueprintIdRef.current = null;
      setPersistedBlueprintId(null);
    }
  }, [blueprintId]);

  // Form state
  const [form, setForm] = useState({
    blueprintName: "",
    grade: "12",
    totalQuestions: "",
    totalScore: 10,
    durationMinutes: 90,
    sections: []
  });

  // UI state
  const [loading, setLoading] = useState(false);
  const [pageError, setPageError] = useState(null);
  const [isMutating, setIsMutating] = useState(false);
  const [feedback, setFeedback] = useState(null);

  // Taxonomy states
  const [topicList, setTopicList] = useState([]);
  const [difficultyList, setDifficultyList] = useState([]);

  // Grade Change Dialog state
  const [isGradeConfirmOpen, setIsGradeConfirmOpen] = useState(false);
  const [pendingGrade, setPendingGrade] = useState("");

  // Section Mode Change Dialog state
  const [modeConfirmState, setModeConfirmState] = useState({
    isOpen: false,
    secIndex: null,
    targetType: null
  });

  // Availability state
  const [availabilityLoading, setAvailabilityLoading] = useState(false);
  const [availabilityError, setAvailabilityError] = useState(null);
  const [availabilityData, setAvailabilityData] = useState({
    checkedAt: null,
    wholeBlueprintFeasible: null,
    availabilityCode: null,
    rowMap: {}
  });
  const latestRequestIdRef = React.useRef(0);
  const debounceTimerRef = React.useRef(null);

  // Initial state setup
  const createEmptySection = () => ({
    clientSectionId: generateClientId("sec"),
    sectionCode: "",
    sectionName: "",
    questionType: "SingleChoice",
    instructionText: "",
    totalQuestions: "",
    scoreBudget: 10,
    scoringRule: "AllOrNothing",
    partCountPerQuestion: null,
    details: [
      {
        clientRowId: generateClientId("row"),
        tagId: "",
        difficultyId: "",
        quantity: 1,
        questionType: null,
        scoringRule: null
      }
    ]
  });

  // Handle location state feedback
  useEffect(() => {
    if (location.state?.feedback) {
      setFeedback(location.state.feedback);
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location, navigate]);

  // Load difficulties on mount
  useEffect(() => {
    questionBankApi.getDifficulties()
      .then((res) => {
        const activeDiffs = (res.data || []).filter(
          (difficulty) => difficulty.isActive !== false && difficulty.active !== false && difficulty.status !== "Inactive"
        );
        setDifficultyList(activeDiffs);
      })
      .catch((err) => {
        console.error("Lỗi khi tải danh sách độ khó:", err);
      });
  }, []);

  // Fetch topics whenever grade changes
  useEffect(() => {
    if (!form.grade) return;
    questionBankApi.getTopicTags(parseInt(form.grade, 10))
      .then((res) => {
        const flattened = flattenTopicTree(res.data || []);
        const activeTopics = flattened.filter(
          (topic) => topic.isActive !== false && topic.active !== false && topic.status !== "Inactive"
        );
        setTopicList(activeTopics);
      })
      .catch((err) => {
        console.error("Lỗi khi tải danh sách chủ đề:", err);
      });
  }, [form.grade]);

  // Load blueprint detail in Edit Mode
  useEffect(() => {
    if (blueprintId) {
      if (loadedBlueprintIdRef.current === blueprintId) {
        return;
      }
      setLoading(true);
      setPageError(null);
      testGeneratorApi.getBlueprintDetail(blueprintId)
        .then((res) => {
          const actions = getBlueprintActions(res.data, currentAccountId);
          if (!actions.canEdit) {
            setPageError("Bạn không có quyền chỉnh sửa cấu trúc đề này ở trạng thái hiện tại.");
            return;
          }

          const editorState = detailToEditorState(res.data);
          if (editorState) {
            setForm(editorState);
            loadedBlueprintIdRef.current = blueprintId;
          } else {
            setPageError("Dữ liệu cấu trúc đề không hợp lệ.");
          }
        })
        .catch((err) => {
          setPageError(getBlueprintErrorMessage(err, "Không thể tải chi tiết cấu trúc đề để chỉnh sửa."));
        })
        .finally(() => {
          setLoading(false);
        });
    } else {
      // In create mode (/expert/blueprints/new), reset form and draft tracking if not already clean
      loadedBlueprintIdRef.current = null;
      persistedBlueprintIdRef.current = null;
      setPersistedBlueprintId(null);
      setPageError(null);
      setFeedback(null);
      setForm({
        blueprintName: "",
        grade: "12",
        totalQuestions: "",
        totalScore: 10,
        durationMinutes: 90,
        sections: [createEmptySection()]
      });
    }
  }, [blueprintId, currentAccountId]);

  // Handle grade change with confirm safeguard
  const handleGradeChange = (newGrade) => {
    const hasAllocations = form.sections.some(sec => sec.details.some(det => det.tagId));
    if (hasAllocations) {
      setPendingGrade(newGrade);
      setIsGradeConfirmOpen(true);
    } else {
      setForm(prev => ({ ...prev, grade: newGrade }));
    }
  };

  const confirmGradeChange = () => {
    // Confirm and reset tagId in all sections details
    setForm(prev => ({
      ...prev,
      grade: pendingGrade,
      sections: prev.sections.map(sec => ({
        ...sec,
        details: sec.details.map(det => ({
          ...det,
          tagId: "" // Reset tagId to empty string
        }))
      }))
    }));
    setIsGradeConfirmOpen(false);
    setPendingGrade("");
  };

  // Section Action Handlers
  const addSection = () => {
    setForm(prev => ({
      ...prev,
      sections: [...prev.sections, createEmptySection()]
    }));
  };

  const removeSection = (secIndex) => {
    if (form.sections.length <= 1) {
      setFeedback({ type: "error", message: "Cấu trúc đề phải chứa ít nhất một phần thi." });
      return;
    }
    setForm(prev => ({
      ...prev,
      sections: prev.sections.filter((_, idx) => idx !== secIndex)
    }));
  };

  const moveSection = (index, direction) => {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= form.sections.length) return;

    setForm(prev => {
      const list = [...prev.sections];
      const temp = list[index];
      list[index] = list[nextIndex];
      list[nextIndex] = temp;
      return { ...prev, sections: list };
    });
  };

  const applySectionTypeChange = (secIndex, targetType) => {
    setForm(prev => ({
      ...prev,
      sections: prev.sections.map((sec, idx) => {
        if (idx !== secIndex) return sec;
        if (targetType === "Mixed") {
          const prevType = sec.questionType || "SingleChoice";
          const prevRule = sec.scoringRule || defaultScoringRule(prevType);
          return {
            ...sec,
            questionType: "Mixed",
            scoringRule: null,
            details: sec.details.map(det => {
              const rowType = det.questionType || prevType;
              const rowRule = det.scoringRule || (rowType === "Composite" ? (prevRule || "WeightedParts") : "AllOrNothing");
              return {
                ...det,
                questionType: rowType,
                scoringRule: rowRule
              };
            })
          };
        } else {
          return {
            ...sec,
            questionType: targetType,
            scoringRule: defaultScoringRule(targetType),
            details: sec.details.map(det => ({
              ...det,
              questionType: null,
              scoringRule: null
            }))
          };
        }
      })
    }));
  };

  const updateSectionField = (secIndex, field, value) => {
    if (field === "questionType") {
      const currentSec = form.sections[secIndex];
      const currentType = currentSec?.questionType;

      if (currentType === value) return;

      if (currentType === "Mixed" && value !== "Mixed") {
        // Mixed -> Homogeneous: Check if any rows are incompatible or if collapsing would create duplicate topic+difficulty pairs
        const hasIncompatibleRow = (currentSec.details || []).some(det => {
          if (!det.questionType) return false;
          if (det.questionType !== value) return true;
          if (value === "Composite") {
            return det.scoringRule && !["TieredTrueFalse", "WeightedParts"].includes(det.scoringRule);
          }
          return det.scoringRule && det.scoringRule !== "AllOrNothing";
        });

        const seenPairs = new Set();
        let hasDuplicatePairs = false;
        for (const det of currentSec.details || []) {
          if (!det.tagId || !det.difficultyId) continue;
          const pairKey = `${det.tagId}\u001F${det.difficultyId}`;
          if (seenPairs.has(pairKey)) {
            hasDuplicatePairs = true;
            break;
          }
          seenPairs.add(pairKey);
        }

        if (hasIncompatibleRow || hasDuplicatePairs) {
          setModeConfirmState({
            isOpen: true,
            secIndex,
            targetType: value
          });
          return;
        }

        applySectionTypeChange(secIndex, value);
        return;
      }

      applySectionTypeChange(secIndex, value);
      return;
    }

    setForm(prev => ({
      ...prev,
      sections: prev.sections.map((sec, idx) => {
        if (idx !== secIndex) return sec;
        return { ...sec, [field]: value };
      })
    }));
  };

  const confirmSectionTypeChange = () => {
    if (modeConfirmState.secIndex !== null && modeConfirmState.targetType) {
      applySectionTypeChange(modeConfirmState.secIndex, modeConfirmState.targetType);
    }
    setModeConfirmState({ isOpen: false, secIndex: null, targetType: null });
  };

  const cancelSectionTypeChange = () => {
    setModeConfirmState({ isOpen: false, secIndex: null, targetType: null });
  };

  // Detail/Allocation Row Action Handlers
  const addDetailRow = (secIndex) => {
    setForm(prev => ({
      ...prev,
      sections: prev.sections.map((sec, idx) => {
        if (idx !== secIndex) return sec;
        const isMixed = sec.questionType === "Mixed";
        return {
          ...sec,
          details: [
            ...sec.details,
            {
              clientRowId: generateClientId("row"),
              tagId: "",
              difficultyId: "",
              quantity: 1,
              questionType: isMixed ? "SingleChoice" : null,
              scoringRule: isMixed ? "AllOrNothing" : null
            }
          ]
        };
      })
    }));
  };

  const removeDetailRow = (secIndex, detIndex) => {
    setForm(prev => ({
      ...prev,
      sections: prev.sections.map((sec, idx) => {
        if (idx !== secIndex) return sec;
        if (sec.details.length <= 1) return sec; // Keep at least one
        return {
          ...sec,
          details: sec.details.filter((_, dIdx) => dIdx !== detIndex)
        };
      })
    }));
  };

  const updateDetailField = (secIndex, detIndex, field, value) => {
    setForm(prev => ({
      ...prev,
      sections: prev.sections.map((sec, idx) => {
        if (idx !== secIndex) return sec;
        const isMixed = sec.questionType === "Mixed";
        return {
          ...sec,
          details: sec.details.map((det, dIdx) => {
            if (dIdx !== detIndex) return det;
            const updated = { ...det, [field]: value };

            if (field === "questionType") {
              if (value === "Composite") {
                if (!["TieredTrueFalse", "WeightedParts"].includes(updated.scoringRule)) {
                  updated.scoringRule = "WeightedParts";
                }
              } else {
                updated.scoringRule = "AllOrNothing";
              }
            }

            const duplicatesExistingAllocation = sec.details.some((other, otherIndex) => {
              if (otherIndex === detIndex) return false;
              if (isMixed) {
                return (
                  other.tagId === updated.tagId &&
                  other.difficultyId === updated.difficultyId &&
                  (other.questionType || "SingleChoice") === (updated.questionType || "SingleChoice") &&
                  (other.scoringRule || "AllOrNothing") === (updated.scoringRule || "AllOrNothing")
                );
              }
              return (
                other.tagId === updated.tagId &&
                other.difficultyId === updated.difficultyId
              );
            });

            // A topic change must not silently create a duplicate allocation pair.
            if (field === "tagId" && updated.difficultyId && duplicatesExistingAllocation) {
              updated.difficultyId = "";
            }

            return updated;
          })
        };
      })
    }));
  };


  // Keep invalid decimal input visible in the summary instead of silently truncating it.
  const toFiniteNumberOrZero = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  };

  // Calc Summary metrics
  const targetTotal = toFiniteNumberOrZero(form.totalQuestions);
  const sectionsTotalSum = form.sections.reduce(
    (sum, sec) => sum + toFiniteNumberOrZero(sec.totalQuestions),
    0
  );

  // Mismatch warnings & Validation state
  const validation = validateBlueprintForDraft(form);

  // Handle Save (Draft or Update Draft)
  const handleSave = async (e) => {
    e.preventDefault();
    setFeedback(null);

    // 1. Run Client-side validator first to prevent invalid submissions
    const validationResult = validateBlueprint(form, true);
    if (!validationResult.isValid) {
      setFeedback({
        type: "error",
        message: `Dữ liệu không hợp lệ. Vui lòng sửa các lỗi sau:\n` + validationResult.errors.join("\n")
      });
      // Scroll to top to see error banner
      window.scrollTo?.({ top: 0, behavior: "smooth" });
      return;
    }

    setIsMutating(true);
    try {
      // 2. Map payload after validator has passed safely
      const payload = editorStateToBlueprintRequest(form);

      let response;
      const targetId = activeBlueprintId;
      if (targetId) {
        response = await testGeneratorApi.updateBlueprint(targetId, payload);
        navigate(`/expert/blueprints/${targetId}`, {
          state: { feedback: { type: "success", message: "Cập nhật cấu trúc đề nháp thành công!" } }
        });
      } else {
        response = await testGeneratorApi.createBlueprint(payload);
        const newId = response.data?.blueprintId || response.data?.id;
        navigate(`/expert/blueprints/${newId}`, {
          state: {
            newlyCreatedBlueprintId: newId,
            feedback: { type: "success", message: "Tạo cấu trúc đề nháp thành công!" }
          }
        });
      }
    } catch (err) {
      setFeedback({
        type: "error",
        message: getBlueprintErrorMessage(err, "Không thể lưu dữ liệu cấu trúc đề. Vui lòng thử lại.")
      });
      window.scrollTo?.({ top: 0, behavior: "smooth" });
    } finally {
      setIsMutating(false);
    }
  };

  // Handle Save and Submit for Review (Draft save + Submit gate with form data preserved)
  const handleSaveAndSubmit = async (e) => {
    if (e) e.preventDefault();
    setFeedback(null);

    const validationResult = validateBlueprint(form, true);
    if (!validationResult.isValid) {
      setFeedback({
        type: "error",
        message: `Dữ liệu không hợp lệ. Vui lòng sửa các lỗi sau:\n` + validationResult.errors.join("\n")
      });
      window.scrollTo?.({ top: 0, behavior: "smooth" });
      return;
    }

    setIsMutating(true);
    let targetId = activeBlueprintId;
    try {
      const payload = editorStateToBlueprintRequest(form);
      if (targetId) {
        await testGeneratorApi.updateBlueprint(targetId, payload);
      } else {
        const createRes = await testGeneratorApi.createBlueprint(payload);
        targetId = createRes.data?.blueprintId || createRes.data?.id;
        persistedBlueprintIdRef.current = targetId;
        loadedBlueprintIdRef.current = targetId;
        setPersistedBlueprintId(targetId);

        // Transition router to edit route while preserving in-memory draft edits and row IDs
        navigate(`/expert/blueprints/${targetId}/edit`, { replace: true });
      }

      await testGeneratorApi.submitBlueprintForReview(targetId);
      navigate(`/expert/blueprints/${targetId}`, {
        state: { feedback: { type: "success", message: "Gửi phản biện cấu trúc đề thành công!" } }
      });
    } catch (err) {
      // Preserve form state, client row IDs, and draft identity on submit 409 or network error
      const errorMsg = getBlueprintErrorMessage(err, "Không thể gửi phản biện. Vui lòng thử lại.");
      setFeedback({
        type: "error",
        message: errorMsg
      });
      window.scrollTo?.({ top: 0, behavior: "smooth" });
    } finally {
      setIsMutating(false);
    }
  };

  // Live availability runner
  const runAvailabilityCheck = (currentForm = form) => {
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }

    const payload = editorStateToAvailabilityRequest(currentForm);
    if (!payload) {
      setAvailabilityLoading(false);
      setAvailabilityError(null);
      setAvailabilityData(prev => ({
        ...prev,
        checkedAt: null,
        wholeBlueprintFeasible: null,
        availabilityCode: null,
        rowMap: {}
      }));
      return;
    }

    const reqId = ++latestRequestIdRef.current;
    setAvailabilityLoading(true);
    setAvailabilityError(null);

    testGeneratorApi.checkBlueprintAvailability(payload)
      .then((res) => {
        if (reqId !== latestRequestIdRef.current) return;
        const data = res.data || {};
        const newRowMap = {};
        for (const sec of (data.sections || [])) {
          for (const r of (sec.rows || [])) {
            newRowMap[r.clientRowId] = {
              status: "complete",
              availableCount: r.availableCount ?? 0,
              requiredCount: r.requiredCount ?? 0,
              shortage: r.shortage ?? 0
            };
          }
        }
        setAvailabilityData({
          checkedAt: data.checkedAt,
          wholeBlueprintFeasible: data.wholeBlueprintFeasible,
          availabilityCode: data.availabilityCode,
          rowMap: newRowMap
        });
        setAvailabilityLoading(false);
        setAvailabilityError(null);
      })
      .catch((err) => {
        if (reqId !== latestRequestIdRef.current) return;
        const errorMsg = getBlueprintErrorMessage(err, "Không thể kiểm tra số lượng câu hỏi khả dụng.");
        setAvailabilityError(errorMsg);
        setAvailabilityLoading(false);

        // Mark sent rows as "error" - never set availableCount to 0
        setAvailabilityData(prev => {
          const updatedRowMap = { ...prev.rowMap };
          for (const sec of (payload.sections || [])) {
            for (const r of (sec.rows || [])) {
              updatedRowMap[r.clientRowId] = {
                status: "error",
                error: errorMsg
              };
            }
          }
          return {
            ...prev,
            wholeBlueprintFeasible: null,
            availabilityCode: null,
            rowMap: updatedRowMap
          };
        });
      });
  };

  // Debounced availability check effect
  useEffect(() => {
    // Invalidate old counts immediately when inputs change
    setAvailabilityData(prev => {
      const updatedRowMap = {};
      for (const sec of form.sections) {
        for (const det of sec.details) {
          const id = det.clientRowId;
          const complete = isDetailRowComplete(sec, det);
          if (!complete) {
            updatedRowMap[id] = { status: "incomplete" };
          } else {
            updatedRowMap[id] = { status: "loading" };
          }
        }
      }
      return {
        ...prev,
        rowMap: updatedRowMap
      };
    });

    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    debounceTimerRef.current = setTimeout(() => {
      runAvailabilityCheck(form);
    }, 400);

    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, [form.grade, form.sections]);


  if (loading) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col items-center justify-center min-h-[300px]">
          <div className="w-10 h-10 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p className="mt-4 text-sm text-on-surface-variant font-semibold">Đang tải dữ liệu cấu trúc đề...</p>
        </div>
      </ExpertLayout>
    );
  }

  if (pageError) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col gap-4 max-w-xl mx-auto text-center mt-12">
          <span className="material-symbols-outlined text-[48px] text-error">error</span>
          <h2 className="text-xl font-bold text-on-background">Đã xảy ra lỗi</h2>
          <p className="text-sm text-on-surface-variant">{pageError}</p>
          <Button className="mt-4" onClick={() => navigate("/expert/blueprints")}>Quay lại danh sách</Button>
        </div>
      </ExpertLayout>
    );
  }

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">

        {/* Page Header */}
        <DashboardPageHeader
          title={isEditMode ? "Chỉnh sửa cấu trúc đề" : "Tạo cấu trúc đề mới"}
          subtitle="Thiết lập các phần kiểm tra, số lượng câu hỏi và tỷ lệ phân bổ chi tiết."
        >
          <div className="flex gap-2.5">
            <Button
              variant="outline"
              disabled={isMutating}
              onClick={() => navigate(activeBlueprintId ? `/expert/blueprints/${activeBlueprintId}` : "/expert/blueprints")}
            >
              Hủy
            </Button>
            <Button
              variant="outline"
              disabled={isMutating}
              onClick={handleSaveAndSubmit}
            >
              {isMutating ? "Đang xử lý..." : "Lưu & gửi phản biện"}
            </Button>
            <Button
              variant="primary"
              disabled={isMutating}
              onClick={handleSave}
            >
              {isMutating ? "Đang lưu..." : isEditMode ? "Lưu thay đổi" : "Lưu bản nháp"}
            </Button>
          </div>
        </DashboardPageHeader>

        {/* Feedback Alert Banner */}
        {feedback && (
          <div className={cn(
            "p-4 rounded-xl border flex items-start gap-3 relative select-text whitespace-pre-line",
            {
              "bg-emerald-success/10 border-emerald-success/20 text-emerald-success": feedback.type === "success",
              "bg-error/10 border-error/20 text-error": feedback.type === "error"
            }
          )}>
            <span className="material-symbols-outlined mt-0.5 shrink-0">
              {feedback.type === "success" ? "check_circle" : "warning"}
            </span>
            <div className="flex-1 pr-8">
              <p className="text-xs font-bold leading-relaxed">{feedback.message}</p>
            </div>
            <button
              onClick={() => setFeedback(null)}
              aria-label="Đóng thông báo"
              className="absolute top-3 right-3 text-on-surface-variant hover:text-on-surface transition-colors cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {/* Workspace Layout */}
        <div className="grid grid-cols-12 gap-6 items-start">

          {/* Left Column: Form Editors (Spans 8) */}
          <div className="col-span-12 lg:col-span-8 flex flex-col gap-6">

            {/* General Info Card */}
            <div className="bg-pure-surface border border-whisper-border rounded-xl p-6 shadow-sm flex flex-col gap-4">
              <h2 className="text-sm font-bold text-primary border-b border-whisper-border pb-2 uppercase tracking-wider">
                Thông tin chung
              </h2>
              <div className="grid grid-cols-12 gap-4">
                <div className="col-span-12 select-text">
                  <label className="block text-xs font-bold text-on-surface-variant mb-1">
                    Tên cấu trúc đề <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    value={form.blueprintName}
                    onChange={(e) => setForm(prev => ({ ...prev, blueprintName: e.target.value }))}
                    placeholder="Ví dụ: Đề thi cuối kỳ 1 Toán học 12"
                    className="w-full rounded-lg border border-outline-variant p-2 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                  />
                </div>

                <div className="col-span-3">
                  <label className="block text-xs font-bold text-on-surface-variant mb-1">
                    Khối lớp <span className="text-error">*</span>
                  </label>
                  <CustomSelect
                    value={form.grade}
                    onValueChange={handleGradeChange}
                    items={[
                      { value: "10", label: "Lớp 10" },
                      { value: "11", label: "Lớp 11" },
                      { value: "12", label: "Lớp 12" }
                    ]}
                  />
                </div>

                <div className="col-span-3 select-text">
                  <label className="block text-xs font-bold text-on-surface-variant mb-1">
                    Số câu của đề <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    value={form.totalQuestions}
                    onChange={(e) => setForm(prev => ({ ...prev, totalQuestions: e.target.value }))}
                    placeholder="Ví dụ: 50"
                    min="0"
                    className="w-full rounded-lg border border-outline-variant p-2 h-10 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                  />
                </div>

                <div className="col-span-3 select-text">
                  <label className="block text-xs font-bold text-on-surface-variant mb-1">
                    Tổng điểm <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    step="0.25"
                    value={form.totalScore}
                    onChange={(e) => setForm(prev => ({ ...prev, totalScore: e.target.value }))}
                    min="0.01"
                    max="100"
                    className="w-full rounded-lg border border-outline-variant p-2 h-10 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                  />
                </div>

                <div className="col-span-3 select-text">
                  <label className="block text-xs font-bold text-on-surface-variant mb-1">
                    Thời gian (phút) <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    value={form.durationMinutes}
                    onChange={(e) => setForm(prev => ({ ...prev, durationMinutes: e.target.value }))}
                    placeholder="Ví dụ: 90"
                    min="0"
                    className="w-full rounded-lg border border-outline-variant p-2 h-10 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                  />
                </div>
              </div>
            </div>

            {/* Sections Loop */}
            <div className="flex flex-col gap-6">
              {form.sections.map((sec, secIdx) => {
                const isComposite = sec.questionType === "Composite";
                const secLabel = `Phần ${secIdx + 1}`;

                return (
                  <div key={secIdx} className="bg-pure-surface border border-whisper-border rounded-xl p-6 shadow-sm relative group flex flex-col gap-4">

                    {/* Move controls and delete section button */}
                    <div className="flex items-center justify-between border-b border-whisper-border pb-3">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-sm text-primary uppercase tracking-wider">{secLabel}</span>
                        <div className="flex border border-whisper-border rounded-lg overflow-hidden">
                          <button
                            type="button"
                            disabled={secIdx === 0}
                            onClick={() => moveSection(secIdx, -1)}
                            className="p-1 hover:bg-surface-container disabled:opacity-30 text-on-surface-variant transition-colors cursor-pointer"
                            title="Di chuyển lên"
                            aria-label="Di chuyển phần thi lên"
                          >
                            <span className="material-symbols-outlined text-[16px] font-bold">keyboard_arrow_up</span>
                          </button>
                          <button
                            type="button"
                            disabled={secIdx === form.sections.length - 1}
                            onClick={() => moveSection(secIdx, 1)}
                            className="p-1 border-l border-whisper-border hover:bg-surface-container disabled:opacity-30 text-on-surface-variant transition-colors cursor-pointer"
                            title="Di chuyển xuống"
                            aria-label="Di chuyển phần thi xuống"
                          >
                            <span className="material-symbols-outlined text-[16px] font-bold">keyboard_arrow_down</span>
                          </button>
                        </div>
                      </div>

                      <button
                        type="button"
                        onClick={() => removeSection(secIdx)}
                        className="p-1 text-on-surface-variant hover:text-error hover:bg-error/5 rounded-lg transition-colors cursor-pointer flex items-center gap-1 text-[11px] font-bold"
                        title="Xóa phần thi"
                        aria-label="Xóa phần thi"
                      >
                        <span className="material-symbols-outlined text-[18px]">delete</span>
                        XÓA PHẦN
                      </button>
                    </div>

                    {/* Section Fields Row */}
                    <div className="grid grid-cols-12 gap-4 select-text">
                      <div className="col-span-12 sm:col-span-2">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Mã (Tùy chọn)</label>
                        <input
                          type="text"
                          value={sec.sectionCode}
                          onChange={(e) => updateSectionField(secIdx, "sectionCode", e.target.value)}
                          placeholder="VD: P1"
                          className="w-full rounded-lg border border-outline-variant p-2 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                        />
                      </div>
                      <div className="col-span-12 sm:col-span-6">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Tên phần thi <span className="text-error">*</span></label>
                        <input
                          type="text"
                          value={sec.sectionName}
                          onChange={(e) => updateSectionField(secIdx, "sectionName", e.target.value)}
                          placeholder="VD: Trắc nghiệm khách quan nhiều lựa chọn"
                          className="w-full rounded-lg border border-outline-variant p-2 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                        />
                      </div>
                      <div className="col-span-12 sm:col-span-4">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Loại câu hỏi <span className="text-error">*</span></label>
                        <CustomSelect
                          value={sec.questionType}
                          onValueChange={(val) => updateSectionField(secIdx, "questionType", val)}
                          items={[
                            { value: "SingleChoice", label: "Trắc nghiệm một lựa chọn" },
                            { value: "MultipleChoice", label: "Trắc nghiệm nhiều lựa chọn" },
                            { value: "TrueFalse", label: "Đúng/Sai" },
                            { value: "ShortAnswer", label: "Tự luận ngắn" },
                            { value: "Composite", label: "Câu hỏi gồm nhiều mệnh đề" },
                            { value: "Mixed", label: "Hỗn hợp" }
                          ]}
                        />
                      </div>
                    </div>

                    {/* Instruction, question count and section score budget */}
                    <div className="grid grid-cols-12 gap-4 select-text">
                      <div className="col-span-12 sm:col-span-6">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Hướng dẫn làm bài</label>
                        <textarea
                          rows={2}
                          value={sec.instructionText}
                          onChange={(e) => updateSectionField(secIdx, "instructionText", e.target.value)}
                          placeholder="Nhập hướng dẫn làm bài cho thí sinh..."
                          className="w-full rounded-lg border border-outline-variant p-2 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all resize-none"
                        />
                      </div>
                      <div className="col-span-6 sm:col-span-3">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Số câu <span className="text-error">*</span></label>
                        <input
                          type="number"
                          value={sec.totalQuestions}
                          onChange={(e) => updateSectionField(secIdx, "totalQuestions", e.target.value)}
                          placeholder="VD: 10"
                          min="0"
                          className="w-full rounded-lg border border-outline-variant p-2 h-10 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                        />
                      </div>
                      <div className="col-span-6 sm:col-span-3">
                        <label className="block text-[11px] font-bold text-on-surface-variant mb-1">Tổng điểm của phần <span className="text-error">*</span></label>
                        <input
                          type="number"
                          step="0.25"
                          value={sec.scoreBudget}
                          onChange={(e) => updateSectionField(secIdx, "scoreBudget", e.target.value)}
                          placeholder="VD: 3.0"
                          min="0.01"
                          max="100"
                          className="w-full rounded-lg border border-outline-variant p-2 h-10 text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
                        />
                      </div>
                    </div>

                    {/* Composite Fields (Rendered only if questionType is Composite) */}
                    {isComposite && (
                      <div className="bg-surface-container-low p-4 rounded-xl border border-whisper-border select-text space-y-3">
                        <div className="w-full md:w-1/2">
                          <label className="block text-xs font-bold text-primary mb-1">
                            Quy tắc chấm <span className="text-error">*</span>
                          </label>
                          <CustomSelect
                            value={sec.scoringRule}
                            onValueChange={(value) => updateSectionField(secIdx, "scoringRule", value)}
                            items={[
                              { value: "WeightedParts", label: "Theo trọng số từng phần" },
                              { value: "TieredTrueFalse", label: "Đúng/Sai phân bậc (giảm một nửa điểm còn lại)" }
                            ]}
                          />
                        </div>
                        <p className="text-[13px] text-on-surface-variant leading-relaxed">
                          {sec.scoringRule === "TieredTrueFalse"
                            ? "Mỗi mệnh đề sai hoặc bỏ trống làm giảm một nửa điểm còn lại. Không đúng mệnh đề nào: 0 điểm."
                            : "Điểm từng phần được tính theo tỷ trọng mặc định được cấu hình trong ngân hàng câu hỏi."}
                        </p>
                      </div>
                    )}

                    {/* Allocation Table */}
                    <div className="border border-whisper-border rounded-xl overflow-x-auto mt-2">
                      <div className="bg-surface-container-low px-4 py-2 border-b border-whisper-border flex justify-between items-center min-w-full">
                        <h3 className="text-xs font-bold text-on-surface">Phân bổ nội dung câu hỏi</h3>
                        <button
                          type="button"
                          onClick={() => addDetailRow(secIdx)}
                          className="h-8 rounded-lg border border-primary/20 px-3 text-[10px] font-bold uppercase tracking-wider text-primary transition-all duration-150 hover:border-primary hover:bg-primary/5 active:scale-[0.98] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 flex items-center gap-1.5 cursor-pointer"
                        >
                          <span className="material-symbols-outlined text-[16px]">add</span>
                          Thêm dòng phân bổ
                        </button>
                      </div>

                      <table className="w-full min-w-[760px] text-left border-collapse select-text">
                        <thead className="bg-surface-container-lowest border-b border-whisper-border">
                          <tr>
                            <th className="text-[11px] font-bold text-on-surface-variant p-2.5">Chủ đề <span className="text-error">*</span></th>
                            <th className="text-[11px] font-bold text-on-surface-variant p-2.5 w-40">Độ khó <span className="text-error">*</span></th>
                            {sec.questionType === "Mixed" && (
                              <>
                                <th className="text-[11px] font-bold text-on-surface-variant p-2.5 w-44">Loại câu hỏi <span className="text-error">*</span></th>
                                <th className="text-[11px] font-bold text-on-surface-variant p-2.5 w-40">Quy tắc chấm <span className="text-error">*</span></th>
                              </>
                            )}
                            <th className="text-[11px] font-bold text-on-surface-variant p-2.5 w-28 text-center">Khả dụng</th>
                            <th className="text-[11px] font-bold text-on-surface-variant p-2.5 w-24 text-center">Số lượng <span className="text-error">*</span></th>
                            <th className="p-2.5 w-12 text-center"></th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-whisper-border bg-pure-surface">
                          {sec.details.map((det, detIdx) => {
                            const isMixed = sec.questionType === "Mixed";
                            const isRowComposite = det.questionType === "Composite";
                            const isDuplicate = isMixed
                              ? sec.details.some((other, otherIdx) =>
                                  otherIdx !== detIdx &&
                                  other.tagId === det.tagId &&
                                  other.difficultyId === det.difficultyId &&
                                  (other.questionType || "SingleChoice") === (det.questionType || "SingleChoice") &&
                                  (other.scoringRule || "AllOrNothing") === (det.scoringRule || "AllOrNothing"))
                              : sec.details.some((other, otherIdx) =>
                                  otherIdx !== detIdx &&
                                  other.tagId === det.tagId &&
                                  other.difficultyId === det.difficultyId);

                            return (
                              <tr key={det.clientRowId || detIdx} className="hover:bg-surface-bright transition-colors">
                                <td className="p-2 align-top">
                                  <BlueprintTopicPicker
                                    value={det.tagId}
                                    topics={topicList}
                                    placeholder="Chọn chủ đề của lớp..."
                                    onValueChange={(val) => updateDetailField(secIdx, detIdx, "tagId", val)}
                                  />
                                </td>
                                <td className="p-2 align-top">
                                  <CustomSelect
                                    value={det.difficultyId}
                                    placeholder="Chọn độ khó..."
                                    onValueChange={(val) => updateDetailField(secIdx, detIdx, "difficultyId", val)}
                                    items={difficultyList
                                      .map(d => ({ value: d.difficultyId || d.id, label: d.difficultyName || d.name }))
                                      .filter(difficulty =>
                                        difficulty.value === det.difficultyId ||
                                        !sec.details.some((other, otherIdx) => {
                                          if (otherIdx === detIdx) return false;
                                          if (isMixed) {
                                            return (
                                              other.tagId === det.tagId &&
                                              other.difficultyId === difficulty.value &&
                                              (other.questionType || "SingleChoice") === (det.questionType || "SingleChoice") &&
                                              (other.scoringRule || "AllOrNothing") === (det.scoringRule || "AllOrNothing")
                                            );
                                          }
                                          return (
                                            other.tagId === det.tagId &&
                                            other.difficultyId === difficulty.value
                                          );
                                        }))}
                                  />
                                  {det.tagId && det.difficultyId && isDuplicate && (
                                    <p className="mt-1 text-[10px] font-semibold text-error">
                                      {isMixed
                                        ? "Trùng chủ đề, độ khó, loại câu và quy tắc chấm trong phần này."
                                        : "Trùng chủ đề và độ khó trong phần này."}
                                    </p>
                                  )}
                                </td>

                                {isMixed && (
                                  <>
                                    <td className="p-2 align-top">
                                      <CustomSelect
                                        value={det.questionType || "SingleChoice"}
                                        onValueChange={(val) => updateDetailField(secIdx, detIdx, "questionType", val)}
                                        items={[
                                          { value: "SingleChoice", label: "Trắc nghiệm một lựa chọn" },
                                          { value: "MultipleChoice", label: "Trắc nghiệm nhiều lựa chọn" },
                                          { value: "TrueFalse", label: "Đúng/Sai" },
                                          { value: "ShortAnswer", label: "Tự luận ngắn" },
                                          { value: "Composite", label: "Câu hỏi gồm nhiều mệnh đề" }
                                        ]}
                                      />
                                    </td>
                                    <td className="p-2 align-top">
                                      {isRowComposite ? (
                                        <CustomSelect
                                          value={det.scoringRule || "WeightedParts"}
                                          onValueChange={(val) => updateDetailField(secIdx, detIdx, "scoringRule", val)}
                                          items={[
                                            { value: "WeightedParts", label: "Theo trọng số phần" },
                                            { value: "TieredTrueFalse", label: "Đúng/Sai phân bậc" }
                                          ]}
                                        />
                                      ) : (
                                        <div className="h-10 flex items-center px-3 text-xs text-on-surface-variant bg-surface-container-low rounded-lg border border-whisper-border font-medium">
                                          <span className="truncate">Tất cả hoặc không</span>
                                        </div>
                                      )}
                                    </td>
                                  </>
                                )}

                                <td className="p-2 text-center align-top pt-2.5">
                                  {(() => {
                                    const isComplete = isDetailRowComplete(sec, det);
                                    if (!isComplete) {
                                      return <span className="text-xs text-on-surface-variant font-semibold">—</span>;
                                    }

                                    const rowInfo = availabilityData.rowMap[det.clientRowId];
                                    if (availabilityLoading || rowInfo?.status === "loading") {
                                      return (
                                        <span className="inline-flex items-center justify-center gap-1 text-[11px] text-on-surface-variant animate-pulse font-medium">
                                          <span className="w-2.5 h-2.5 border-2 border-primary border-t-transparent rounded-full animate-spin"></span>
                                          Đang kiểm tra...
                                        </span>
                                      );
                                    }

                                    if (rowInfo?.status === "error" || availabilityError) {
                                      return (
                                        <div className="flex flex-col items-center gap-0.5">
                                          <span className="text-[11px] font-bold text-error">Lỗi kiểm tra</span>
                                          <button
                                            type="button"
                                            onClick={() => runAvailabilityCheck()}
                                            className="text-[10px] text-primary font-bold hover:underline cursor-pointer"
                                          >
                                            Thử lại
                                          </button>
                                        </div>
                                      );
                                    }

                                    if (rowInfo && rowInfo.status === "complete") {
                                      const hasShortage = rowInfo.shortage > 0;
                                      return (
                                        <div className="flex flex-col items-center">
                                          <span className={cn(
                                            "font-bold font-mono text-xs",
                                            hasShortage ? "text-error" : "text-emerald-success"
                                          )}>
                                            {rowInfo.availableCount} câu
                                          </span>
                                          {hasShortage && (
                                            <span className="text-[10px] font-bold text-error bg-error/10 border border-error/20 px-1.5 py-0.5 rounded mt-0.5 whitespace-nowrap">
                                              Thiếu {rowInfo.shortage}
                                            </span>
                                          )}
                                        </div>
                                      );
                                    }

                                    return <span className="text-xs text-on-surface-variant font-semibold">—</span>;
                                  })()}
                                </td>

                                <td className="p-2 align-top">
                                  <input
                                    type="number"
                                    value={det.quantity}
                                    min="1"
                                    onChange={(e) => updateDetailField(secIdx, detIdx, "quantity", e.target.value)}
                                    className="w-full rounded-lg border border-outline-variant p-2 text-xs text-on-surface text-center focus:outline-none focus:border-primary transition-all"
                                  />
                                </td>
                                <td className="p-2 text-center align-top pt-3">
                                  <button
                                    type="button"
                                    disabled={sec.details.length <= 1}
                                    onClick={() => removeDetailRow(secIdx, detIdx)}
                                    aria-label="Xóa dòng phân bổ này"
                                    className="p-1.5 text-on-surface-variant hover:text-error hover:bg-error/5 rounded-lg disabled:opacity-30 transition-colors cursor-pointer"
                                  >
                                    <span className="material-symbols-outlined text-[18px]">delete</span>
                                  </button>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>

                      </table>
                    </div>

                  </div>
                );
              })}
            </div>

            {/* Add Section Dotted Button */}
            <button
              type="button"
              onClick={addSection}
              className="border-2 border-dashed border-outline-variant hover:border-primary bg-pure-surface hover:bg-surface-container-low p-4 rounded-xl text-primary font-bold text-xs flex items-center justify-center gap-2 transition-all cursor-pointer shadow-sm"
            >
              <span className="material-symbols-outlined">add_circle</span>
              THÊM PHẦN THI MỚI
            </button>
          </div>

          {/* Right Column: Live Summary Panel (Spans 4) */}
          <div className="col-span-12 lg:col-span-4 sticky top-6">
            <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm flex flex-col gap-4 max-h-[85vh] overflow-y-auto">
              <h2 className="text-sm font-bold text-on-surface border-b border-whisper-border pb-3 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary font-bold">analytics</span>
                Tóm tắt cấu trúc
              </h2>

              {/* Statistical details */}
              <div className="grid grid-cols-2 gap-3">
                <div className="bg-surface-container-low p-3 rounded-lg border border-whisper-border">
                  <span className="block text-[11px] font-bold text-on-surface-variant mb-1">Mục tiêu tổng</span>
                  <span className="block text-xl font-extrabold text-primary font-mono">{targetTotal} câu</span>
                </div>
                <div className="bg-surface-container-low p-3 rounded-lg border border-whisper-border">
                  <span className="block text-[11px] font-bold text-on-surface-variant mb-1">Tổng các phần</span>
                  <span className={cn(
                    "block text-xl font-extrabold font-mono",
                    targetTotal === sectionsTotalSum ? "text-emerald-success" : "text-amber-warning"
                  )}>
                    {sectionsTotalSum} câu
                  </span>
                </div>
              </div>

              {/* Live Warning Panels */}
              {validation.warnings.length > 0 && (
                <div className="bg-amber-warning/10 border border-amber-warning/20 text-on-surface p-3 rounded-xl flex items-start gap-2 select-text">
                  <span className="material-symbols-outlined text-amber-warning shrink-0 text-[18px]">warning</span>
                  <div>
                    <span className="block text-xs font-bold text-amber-warning">Cảnh báo chưa khớp cấu trúc</span>
                    <ul className="list-disc pl-4 mt-1 text-[11px] text-on-surface-variant leading-relaxed flex flex-col gap-1">
                      {validation.warnings.map((w, idx) => (
                        <li key={idx}>{w}</li>
                      ))}
                    </ul>
                    <span className="block text-[10px] text-on-surface-variant font-medium mt-2 italic">
                      * Cảnh báo trên không chặn việc lưu bản nháp nhưng sẽ cần điều chỉnh chính xác trước khi gửi phê duyệt.
                    </span>
                  </div>
                </div>
              )}

              {/* Live Availability Status Card */}
              <div className="border border-whisper-border rounded-xl p-3.5 bg-surface-container-low flex flex-col gap-2 select-text">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold text-on-surface-variant uppercase tracking-wider flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[16px] text-primary">inventory_2</span>
                    Độ khả dụng ngân hàng
                  </span>
                  {availabilityLoading && (
                    <div className="w-3.5 h-3.5 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                  )}
                </div>

                {availabilityLoading ? (
                  <p className="text-xs text-on-surface-variant animate-pulse font-medium">
                    Đang đối chiếu số lượng câu hỏi khả dụng...
                  </p>
                ) : availabilityError ? (
                  <div className="bg-error/10 border border-error/20 p-2.5 rounded-lg text-error text-xs flex flex-col gap-1.5">
                    <div className="flex items-start gap-1.5">
                      <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">warning</span>
                      <span className="font-semibold">{availabilityError}</span>
                    </div>
                    <button
                      type="button"
                      onClick={() => runAvailabilityCheck()}
                      className="self-start text-[11px] font-bold text-primary underline cursor-pointer"
                    >
                      Thử lại kiểm tra khả dụng
                    </button>
                  </div>
                ) : hasIncompleteAllocationRows(form) ? (
                  <div className="text-[11px] text-on-surface-variant leading-relaxed">
                    <span className="font-semibold text-on-surface">Chưa đủ thông tin:</span> Vui lòng hoàn tất chủ đề, độ khó và số lượng ở tất cả các dòng phân bổ để đánh giá toàn bộ cấu trúc đề.
                  </div>
                ) : availabilityData.wholeBlueprintFeasible === true ? (
                  <div className="bg-emerald-success/10 border border-emerald-success/20 p-2.5 rounded-lg text-emerald-success text-xs flex items-start gap-2">
                    <span className="material-symbols-outlined text-[18px] shrink-0">check_circle</span>
                    <div>
                      <span className="font-bold block">Ngân hàng câu hỏi đáp ứng đủ</span>
                      <span className="text-[11px] opacity-90 leading-tight block mt-0.5">
                        Tất cả các dòng phân bổ đều có đủ câu hỏi hợp lệ trong ngân hàng.
                      </span>
                    </div>
                  </div>
                ) : availabilityData.wholeBlueprintFeasible === false ? (
                  <div className="bg-error/10 border border-error/20 p-2.5 rounded-lg text-error text-xs flex flex-col gap-1">
                    <div className="flex items-start gap-1.5">
                      <span className="material-symbols-outlined text-[18px] shrink-0">cancel</span>
                      <div>
                        <span className="font-bold block">
                          {availabilityData.availabilityCode === "BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT"
                            ? "Xung đột trùng lặp câu hỏi"
                            : "Ngân hàng chưa đủ câu hỏi"}
                        </span>
                        <p className="text-[11px] leading-relaxed mt-0.5">
                          {availabilityData.availabilityCode === "BLUEPRINT_AVAILABILITY_OVERLAP_CONFLICT"
                            ? "Các dòng phân bổ đủ câu hỏi riêng lẻ nhưng bị trùng lặp tập câu hỏi, không đủ câu hỏi riêng biệt cho toàn bộ đề thi."
                            : "Số lượng câu hỏi hợp lệ trong ngân hàng không đủ để đáp ứng toàn bộ cấu trúc đề thi này."}
                        </p>
                      </div>
                    </div>
                    <span className="text-[10px] text-on-surface-variant italic mt-1">
                      * Vẫn có thể lưu bản nháp nhưng cần bổ sung câu hỏi trước khi gửi phê duyệt.
                    </span>
                  </div>
                ) : (
                  <p className="text-xs text-on-surface-variant">
                    Chưa có thông tin kiểm tra khả dụng.
                  </p>
                )}
              </div>

              {/* Sections detail list */}
              <div className="flex flex-col gap-3">
                <h3 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider">Chi tiết theo từng phần</h3>
                {form.sections.map((sec, idx) => {
                  const qtySum = sec.details.reduce(
                    (sum, det) => sum + toFiniteNumberOrZero(det.quantity),
                    0
                  );
                  const targetSec = toFiniteNumberOrZero(sec.totalQuestions);
                  const isSectionMatch = targetSec === qtySum;

                  return (
                    <div key={idx} className="border border-whisper-border rounded-lg p-3 bg-surface-bright">
                      <div className="flex justify-between items-center mb-1">
                        <span className="font-bold text-xs text-on-surface truncate pr-2 max-w-[150px]" title={sec.sectionName || `Phần ${idx + 1}`}>
                          {sec.sectionName || `Phần ${idx + 1}`}
                        </span>
                        <span className={cn(
                          "text-[10px] px-2 py-0.5 rounded font-bold uppercase",
                          isSectionMatch ? "bg-emerald-success/10 text-emerald-success" : "bg-amber-warning/10 text-amber-warning"
                        )}>
                          {qtySum} / {targetSec} câu
                        </span>
                      </div>
                      <div className="text-[11px] text-on-surface-variant">
                         Mã: <span className="font-semibold text-on-surface">{sec.sectionCode || "Không"}</span> · Loại: <span className="font-semibold text-on-surface">{getQuestionTypeLabel(sec.questionType)}</span>
                      </div>
                    </div>
                  );
                })}
              </div>

            </div>
          </div>

        </div>

      </div>

      {/* Grade Change Warn Confirmation Dialog */}
      <Dialog isOpen={isGradeConfirmOpen} onClose={() => setIsGradeConfirmOpen(false)}>
        <DialogHeader>
          <DialogTitle>Cảnh báo đổi khối lớp</DialogTitle>
          <DialogDescription>
            Thay đổi khối lớp sẽ thiết lập lại toàn bộ chủ đề đã chọn trước đó.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Danh mục chủ đề (topics) phụ thuộc chặt chẽ vào khối lớp được chọn. Khi đổi khối lớp, tất cả
            chủ đề hiện có trong bảng phân bổ sẽ bị xóa để đảm bảo bạn chọn các chủ đề tương thích với khối lớp mới.
            <br />
            <br />
            Bạn có chắc chắn muốn thay đổi sang <span className="font-bold text-on-surface">Lớp {pendingGrade}</span>?
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => setIsGradeConfirmOpen(false)}
          >
            Hủy bỏ
          </Button>
          <Button
            variant="primary"
            onClick={confirmGradeChange}
          >
            Xác nhận thay đổi
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Section Mode Change Confirmation Dialog */}
      <Dialog isOpen={modeConfirmState.isOpen} onClose={cancelSectionTypeChange}>
        <DialogHeader>
          <DialogTitle>Xác nhận đổi loại phần thi</DialogTitle>
          <DialogDescription>
            Chuyển đổi từ phần thi Hỗn hợp sang phần thi đồng nhất.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Phần thi hiện tại đang ở chế độ <span className="font-bold text-on-surface">Hỗn hợp</span> với các dòng phân bổ có cấu hình loại câu hỏi hoặc quy tắc chấm riêng biệt.
            <br />
            <br />
            Nếu chuyển sang <span className="font-bold text-primary">{getQuestionTypeLabel(modeConfirmState.targetType)}</span>, tất cả các dòng phân bổ sẽ được đồng nhất theo loại câu hỏi này và cấu hình phân bổ riêng từng dòng trước đó sẽ bị thay thế.
            <br />
            <br />
            Bạn có chắc chắn muốn chuyển đổi không?
          </p>
        </DialogContent>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={cancelSectionTypeChange}
          >
            Giữ lại Hỗn hợp
          </Button>
          <Button
            variant="primary"
            onClick={confirmSectionTypeChange}
          >
            Xác nhận chuyển đổi
          </Button>
        </DialogFooter>
      </Dialog>

    </ExpertLayout>
  );
}

