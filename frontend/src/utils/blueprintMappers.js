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
        sectionCode: section.sectionCode || "",
        sectionName: section.sectionName || "",
        questionType: section.questionType || "SingleChoice",
        instructionText: section.instructionText || "",
        totalQuestions: section.totalQuestions ?? "",
        scoreBudget: section.scoreBudget ?? "",
        scoringRule: isMixed ? null : (section.scoringRule || defaultScoringRule(section.questionType)),
        partCountPerQuestion: section.partCountPerQuestion ?? "",
        details: (section.details || []).map((detailSlot) => ({
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

