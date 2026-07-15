function isPositiveInteger(val) {
  if (val === undefined || val === null || String(val).trim() === "") return false;
  const text = String(val).trim();
  if (!/^\d+$/.test(text)) return false;

  const num = Number(text);
  return Number.isSafeInteger(num) && num > 0;
}

function isNonNegativeInteger(val) {
  if (val === undefined || val === null || String(val).trim() === "") return false;
  const text = String(val).trim();
  if (!/^\d+$/.test(text)) return false;

  const num = Number(text);
  return Number.isSafeInteger(num) && num >= 0;
}

function isValidPoint(val) {
  if (val === undefined || val === null || String(val).trim() === "") return false;
  const str = String(val).trim();
  if (!/^\d+(?:\.\d{1,2})?$/.test(str)) return false;

  const num = Number(str);
  return (
    Number.isFinite(num) &&
    num >= 0 &&
    num <= 10 &&
    Math.round(num * 100) === num * 100
  );
}

function toIntegerOrNull(val) {
  const text = String(val ?? "").trim();
  if (!/^\d+$/.test(text)) return null;

  const num = Number(text);
  return Number.isSafeInteger(num) ? num : null;
}

export function validateBlueprint(state, isSubmit = false) {
  const errors = [];
  const warnings = [];

  // 1. Validate Blueprint Name
  const blueprintName = (state.blueprintName || "").trim();
  if (!blueprintName) {
    errors.push("Tên cấu trúc đề không được để trống.");
  } else if (blueprintName.length > 100) {
    errors.push("Tên cấu trúc đề không được vượt quá 100 ký tự.");
  }

  // 2. Validate Grade
  const grade = parseInt(state.grade, 10);
  if (isNaN(grade) || ![10, 11, 12].includes(grade)) {
    errors.push("Khối lớp phải là 10, 11 hoặc 12.");
  }

  // 3. Validate Blueprint Total Questions & Duration
  if (isSubmit) {
    if (!isPositiveInteger(state.totalQuestions)) {
      errors.push("Tổng số câu đề thi phải là số nguyên dương lớn hơn 0.");
    }
    if (!isPositiveInteger(state.durationMinutes)) {
      errors.push("Thời gian làm bài phải là số nguyên dương lớn hơn 0.");
    }
  } else {
    if (!isNonNegativeInteger(state.totalQuestions)) {
      errors.push("Tổng số câu đề thi phải là số nguyên không âm.");
    }
    if (!isNonNegativeInteger(state.durationMinutes)) {
      errors.push("Thời gian làm bài phải là số nguyên không âm.");
    }
  }

  // 4. Validate Sections
  const sections = state.sections || [];
  if (sections.length === 0) {
    errors.push("Cấu trúc đề phải có ít nhất một phần thi.");
  }

  let totalSectionsQuestions = 0;

  sections.forEach((sec, idx) => {
    const secIdxLabel = `Phần ${idx + 1}`;
    const secName = (sec.sectionName || "").trim();

    if (!secName) {
      errors.push(`${secIdxLabel}: Tên phần thi không được để trống.`);
    } else if (secName.length > 100) {
      errors.push(`${secIdxLabel}: Tên phần thi không được vượt quá 100 ký tự.`);
    }

    if (sec.sectionCode && sec.sectionCode.trim().length > 20) {
      errors.push(`${secIdxLabel}: Mã phần thi không được vượt quá 20 ký tự.`);
    }

    // Question Type validation
    const allowedTypes = ["SingleChoice", "MultipleChoice", "TrueFalse", "ShortAnswer", "Composite"];
    if (!sec.questionType || !allowedTypes.includes(sec.questionType)) {
      errors.push(`${secIdxLabel}: Loại câu hỏi không hợp lệ.`);
    }

    // Section Total Questions & Point validation
    if (isSubmit) {
      if (!isPositiveInteger(sec.totalQuestions)) {
        errors.push(`${secIdxLabel}: Số câu phải là số nguyên dương lớn hơn 0.`);
      }
    } else {
      if (!isNonNegativeInteger(sec.totalQuestions)) {
        errors.push(`${secIdxLabel}: Số câu phải là số nguyên không âm.`);
      }
    }

    if (!isValidPoint(sec.defaultPointPerQuestion)) {
      errors.push(`${secIdxLabel}: Điểm mỗi câu phải là số từ 0 đến 10, tối đa 2 chữ số thập phân.`);
    }

    // Composite metadata validation
    if (sec.questionType === "Composite") {
      if (!isPositiveInteger(sec.partCountPerQuestion)) {
        errors.push(`${secIdxLabel}: Số phần mỗi câu của câu hỏi Composite phải là số nguyên dương.`);
      }
      if (!isValidPoint(sec.defaultPointPerPart)) {
        errors.push(`${secIdxLabel}: Điểm mỗi phần của câu hỏi Composite phải là số từ 0 đến 10, tối đa 2 chữ số thập phân.`);
      }
    }

    // Validate details (allocations)
    const details = sec.details || [];
    if (details.length === 0) {
      errors.push(`${secIdxLabel}: Phải có ít nhất một dòng phân bổ nội dung.`);
    }

    let sectionDetailsQuantitySum = 0;
    const seenSlots = new Set();

    details.forEach((det, detIdx) => {
      const detIdxLabel = `${secIdxLabel} - Dòng phân bổ ${detIdx + 1}`;

      if (!det.tagId) {
        errors.push(`${detIdxLabel}: Chưa chọn chủ đề.`);
      }
      if (!det.difficultyId) {
        errors.push(`${detIdxLabel}: Chưa chọn độ khó.`);
      }

      const qty = Number(det.quantity);
      if (!isPositiveInteger(det.quantity)) {
        errors.push(`${detIdxLabel}: Số lượng phải là số nguyên dương lớn hơn hoặc bằng 1.`);
      } else {
        sectionDetailsQuantitySum += qty;
      }

      // Check duplicate tagId + difficultyId in same section
      if (det.tagId && det.difficultyId) {
        const slotKey = `${det.tagId}_${det.difficultyId}`;
        if (seenSlots.has(slotKey)) {
          errors.push(`${secIdxLabel}: Trùng lặp cặp chủ đề và độ khó.`);
        } else {
          seenSlots.add(slotKey);
        }
      }
    });

    const parsedSecQuestions = toIntegerOrNull(sec.totalQuestions);
    if (parsedSecQuestions !== null) {
      totalSectionsQuestions += parsedSecQuestions;

      // Quantity mismatch check
      if (parsedSecQuestions !== sectionDetailsQuantitySum) {
        const mismatchMsg = `${secIdxLabel}: Tổng số lượng phân bổ (${sectionDetailsQuantitySum} câu) không khớp với số câu đã thiết lập (${parsedSecQuestions} câu).`;
        if (isSubmit) {
          errors.push(mismatchMsg);
        } else {
          warnings.push(mismatchMsg);
        }
      }
    }
  });

  const parsedTotalQuestions = toIntegerOrNull(state.totalQuestions);
  if (parsedTotalQuestions !== null) {
    // Total questions mismatch check
    if (parsedTotalQuestions !== totalSectionsQuestions) {
      const totalMismatchMsg = `Tổng số câu của các phần thi (${totalSectionsQuestions} câu) không khớp với Tổng số câu mục tiêu của cấu trúc (${parsedTotalQuestions} câu).`;
      if (isSubmit) {
        errors.push(totalMismatchMsg);
      } else {
        warnings.push(totalMismatchMsg);
      }
    }
  }

  return {
    isValid: errors.length === 0,
    errors,
    warnings
  };
}

export function validateBlueprintForDraft(state) {
  return validateBlueprint(state, false);
}

export function validateBlueprintForSubmit(state) {
  return validateBlueprint(state, true);
}
