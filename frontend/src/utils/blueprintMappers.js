import { ACTUAL_QUESTION_TYPES } from "./blueprintLabels";

export function generateClientId(prefix = "client") {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

export function detailToEditorState(detail) {
  if (!detail) return null;

  return {
    blueprintName: detail.blueprintName || "",
    grade: detail.grade ? String(detail.grade) : "12",
    totalQuestions: detail.totalQuestions ?? "",
    totalScore: detail.totalScore ?? 10,
    durationMinutes: detail.durationMinutes ?? 90,
    sections: (detail.sections || []).map((section) => {
      const isMixed = section.questionType === "Mixed";
      return {
        clientSectionId: section.clientSectionId || section.blueprintSectionId || generateClientId("sec"),
        sectionCode: section.sectionCode || "",
        sectionName: section.sectionName || "",
        questionType: section.questionType || "SingleChoice",
        instructionText: section.instructionText || "",
        totalQuestions: section.totalQuestions ?? "",
        scoreBudget: section.scoreBudget ?? "",
        scoringRule: isMixed ? null : (section.scoringRule || defaultScoringRule(section.questionType)),
        partCountPerQuestion: section.partCountPerQuestion ?? "",
        details: (section.details || []).map((detailSlot) => ({
          clientRowId: detailSlot.clientRowId || detailSlot.blueprintDetailId || generateClientId("row"),
          tagId: detailSlot.tagId || "",
          difficultyId: detailSlot.difficultyId || "",
          quantity: detailSlot.quantity ?? 1,
          questionType: isMixed ? (detailSlot.questionType || "SingleChoice") : null,
          scoringRule: isMixed
            ? (detailSlot.scoringRule || defaultScoringRule(detailSlot.questionType || "SingleChoice"))
            : null
        }))
      };
    })
  };
}

export function editorStateToBlueprintRequest(editorState) {
  if (!editorState) return null;

  const parseInteger = (value, fieldName) => {
    const text = String(value ?? "").trim();
    const parsed = Number(text);
    if (!/^\d+$/.test(text) || !Number.isSafeInteger(parsed)) {
      throw new Error(`Trường '${fieldName}' phải là số nguyên hợp lệ.`);
    }
    return parsed;
  };

  const parseDecimal = (value, fieldName) => {
    const text = String(value ?? "").trim();
    const parsed = Number(text);
    if (!/^\d+(?:\.\d{1,2})?$/.test(text) || !Number.isFinite(parsed)) {
      throw new Error(`Trường '${fieldName}' phải là số có tối đa 2 chữ số thập phân.`);
    }
    return parsed;
  };

  return {
    blueprintName: (editorState.blueprintName || "").trim(),
    grade: parseInteger(editorState.grade, "Khối lớp"),
    totalQuestions: parseInteger(editorState.totalQuestions, "Tổng số câu"),
    totalScore: parseDecimal(editorState.totalScore, "Tổng điểm"),
    durationMinutes: parseInteger(editorState.durationMinutes, "Thời gian làm bài"),
    sections: (editorState.sections || []).map((section, index) => {
      const isMixed = section.questionType === "Mixed";
      const isComposite = section.questionType === "Composite";
      const sectionLabel = `Phần ${index + 1} (${section.sectionName || "Chưa đặt tên"})`;

      let sectionScoringRule = null;
      if (!isMixed) {
        sectionScoringRule = isComposite ? (section.scoringRule || "WeightedParts") : "AllOrNothing";
      }

      return {
        sectionOrder: index + 1,
        sectionCode: section.sectionCode?.trim() || null,
        sectionName: (section.sectionName || "").trim(),
        questionType: section.questionType,
        instructionText: section.instructionText?.trim() || null,
        totalQuestions: parseInteger(section.totalQuestions, `${sectionLabel} - Số câu`),
        scoreBudget: parseDecimal(section.scoreBudget, `${sectionLabel} - Quỹ điểm`),
        scoringRule: sectionScoringRule,
        partCountPerQuestion: null,
        details: (section.details || []).map((detailSlot, detailIndex) => {
          const detailLabel = `${sectionLabel} - Phân bổ dòng ${detailIndex + 1}`;
          if (!detailSlot.tagId) throw new Error(`Chưa chọn chủ đề tại ${detailLabel}.`);
          if (!detailSlot.difficultyId) throw new Error(`Chưa chọn độ khó tại ${detailLabel}.`);

          const baseDetail = {
            tagId: detailSlot.tagId,
            difficultyId: detailSlot.difficultyId,
            quantity: parseInteger(detailSlot.quantity, `${detailLabel} - Số lượng`)
          };

          if (isMixed) {
            const rowType = detailSlot.questionType || "SingleChoice";
            const rowRule = rowType === "Composite"
              ? (detailSlot.scoringRule === "TieredTrueFalse" ? "TieredTrueFalse" : "WeightedParts")
              : "AllOrNothing";
            return {
              ...baseDetail,
              questionType: rowType,
              scoringRule: rowRule
            };
          }

          return {
            ...baseDetail,
            questionType: null,
            scoringRule: null
          };
        })
      };
    })
  };
}

export function defaultScoringRule(questionType) {
  if (questionType === "Mixed") return null;
  return questionType === "Composite" ? "WeightedParts" : "AllOrNothing";
}

export function isDetailRowComplete(section, detail) {
  if (!detail || !detail.tagId || !detail.difficultyId) return false;
  const qty = Number(detail.quantity);
  if (!Number.isInteger(qty) || qty <= 0) return false;

  const isMixed = section?.questionType === "Mixed";
  if (isMixed) {
    if (!detail.questionType || !ACTUAL_QUESTION_TYPES.includes(detail.questionType)) return false;
    if (detail.questionType === "Composite") {
      return ["TieredTrueFalse", "WeightedParts"].includes(detail.scoringRule);
    }
    return detail.scoringRule === "AllOrNothing";
  }

  // Homogeneous section:
  if (!section?.questionType || !ACTUAL_QUESTION_TYPES.includes(section.questionType)) return false;
  if (section.questionType === "Composite") {
    return ["TieredTrueFalse", "WeightedParts"].includes(section.scoringRule);
  }
  return section.scoringRule === "AllOrNothing";
}

export function hasIncompleteAllocationRows(editorState) {
  if (!editorState || !Array.isArray(editorState.sections) || editorState.sections.length === 0) {
    return true;
  }
  for (const sec of editorState.sections) {
    if (!Array.isArray(sec.details) || sec.details.length === 0) return true;
    for (const det of sec.details) {
      if (!isDetailRowComplete(sec, det)) return true;
    }
  }
  return false;
}

export function editorStateToAvailabilityRequest(editorState) {
  if (!editorState) return null;
  const grade = parseInt(editorState.grade, 10);
  if (!Number.isInteger(grade)) return null;

  const validSections = [];
  for (const section of (editorState.sections || [])) {
    const isMixed = section.questionType === "Mixed";
    const isComposite = section.questionType === "Composite";
    const completeRows = [];

    for (const det of (section.details || [])) {
      if (!isDetailRowComplete(section, det)) continue;

      const baseRow = {
        clientRowId: det.clientRowId || generateClientId("row"),
        tagId: det.tagId,
        difficultyId: det.difficultyId,
        quantity: parseInt(det.quantity, 10)
      };

      if (isMixed) {
        completeRows.push({
          ...baseRow,
          questionType: det.questionType,
          scoringRule: det.scoringRule
        });
      } else {
        // Homogeneous: omit questionType and scoringRule
        completeRows.push(baseRow);
      }
    }

    // Only include section if it contains at least one complete row
    if (completeRows.length > 0) {
      validSections.push({
        clientSectionId: section.clientSectionId || generateClientId("sec"),
        questionType: section.questionType,
        scoringRule: isMixed ? null : (isComposite ? (section.scoringRule || "WeightedParts") : "AllOrNothing"),
        rows: completeRows
      });
    }
  }

  if (validSections.length === 0) {
    return null;
  }

  return {
    grade,
    sections: validSections
  };
}

