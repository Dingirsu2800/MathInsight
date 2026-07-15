export function detailToEditorState(detail) {
  if (!detail) return null;
  return {
    blueprintName: detail.blueprintName || "",
    grade: detail.grade ? String(detail.grade) : "12",
    totalQuestions: detail.totalQuestions !== undefined && detail.totalQuestions !== null ? detail.totalQuestions : "",
    durationMinutes: detail.durationMinutes !== undefined && detail.durationMinutes !== null ? detail.durationMinutes : 90,
    sections: (detail.sections || []).map((sec) => ({
      sectionCode: sec.sectionCode || "",
      sectionName: sec.sectionName || "",
      questionType: sec.questionType || "SingleChoice",
      instructionText: sec.instructionText || "",
      totalQuestions: sec.totalQuestions !== undefined && sec.totalQuestions !== null ? sec.totalQuestions : "",
      defaultPointPerQuestion: sec.defaultPointPerQuestion !== undefined && sec.defaultPointPerQuestion !== null ? sec.defaultPointPerQuestion : 0.2,
      partCountPerQuestion: sec.partCountPerQuestion !== undefined && sec.partCountPerQuestion !== null ? sec.partCountPerQuestion : "",
      defaultPointPerPart: sec.defaultPointPerPart !== undefined && sec.defaultPointPerPart !== null ? sec.defaultPointPerPart : "",
      details: (sec.details || []).map((det) => ({
        tagId: det.tagId || "",
        difficultyId: det.difficultyId || "",
        quantity: det.quantity !== undefined && det.quantity !== null ? det.quantity : 1
      }))
    }))
  };
}

export function editorStateToBlueprintRequest(editorState) {
  if (!editorState) return null;

  const parseInteger = (val, fieldName) => {
    if (val === undefined || val === null || String(val).trim() === "") {
      throw new Error(`Trường '${fieldName}' không được bỏ trống.`);
    }
    const text = String(val).trim();
    const parsed = Number(text);
    if (!/^\d+$/.test(text) || !Number.isSafeInteger(parsed)) {
      throw new Error(`Trường '${fieldName}' chứa giá trị không hợp lệ.`);
    }
    return parsed;
  };

  const parseFloatVal = (val, fieldName) => {
    if (val === undefined || val === null || String(val).trim() === "") {
      throw new Error(`Trường '${fieldName}' không được bỏ trống.`);
    }
    const text = String(val).trim();
    const parsed = Number(text);
    if (!/^\d+(?:\.\d{1,2})?$/.test(text) || !Number.isFinite(parsed)) {
      throw new Error(`Trường '${fieldName}' chứa giá trị không hợp lệ.`);
    }
    return parsed;
  };

  return {
    blueprintName: (editorState.blueprintName || "").trim(),
    grade: parseInteger(editorState.grade, "Khối lớp"),
    totalQuestions: parseInteger(editorState.totalQuestions, "Tổng số câu"),
    durationMinutes: parseInteger(editorState.durationMinutes, "Thời gian làm bài"),
    sections: (editorState.sections || []).map((sec, index) => {
      const isComposite = sec.questionType === "Composite";
      const sectionLabel = `Phần ${index + 1} (${sec.sectionName || "Chưa đặt tên"})`;

      const mappedSection = {
        sectionOrder: index + 1, // Re-index starting from 1 sequentially
        sectionCode: sec.sectionCode ? sec.sectionCode.trim() : null,
        sectionName: (sec.sectionName || "").trim(),
        questionType: sec.questionType,
        instructionText: sec.instructionText ? sec.instructionText.trim() : null,
        totalQuestions: parseInteger(sec.totalQuestions, `${sectionLabel} - Số câu`),
        defaultPointPerQuestion: parseFloatVal(sec.defaultPointPerQuestion, `${sectionLabel} - Điểm mỗi câu`),
        details: (sec.details || []).map((det, detIdx) => {
          const detailLabel = `${sectionLabel} - Phân bổ dòng ${detIdx + 1}`;
          if (!det.tagId) {
            throw new Error(`Chưa chọn chủ đề tại ${detailLabel}.`);
          }
          if (!det.difficultyId) {
            throw new Error(`Chưa chọn độ khó tại ${detailLabel}.`);
          }
          return {
            tagId: det.tagId,
            difficultyId: det.difficultyId,
            quantity: parseInteger(det.quantity, `${detailLabel} - Số lượng`)
          };
        })
      };

      if (isComposite) {
        mappedSection.partCountPerQuestion = parseInteger(sec.partCountPerQuestion, `${sectionLabel} - Số phần mỗi câu`);
        mappedSection.defaultPointPerPart = parseFloatVal(sec.defaultPointPerPart, `${sectionLabel} - Điểm mỗi phần`);
      } else {
        mappedSection.partCountPerQuestion = null;
        mappedSection.defaultPointPerPart = null;
      }

      return mappedSection;
    })
  };
}
