function isInteger(value, positive) {
  const text = String(value ?? "").trim();
  if (!/^\d+$/.test(text)) return false;
  const number = Number(text);
  return Number.isSafeInteger(number) && (positive ? number > 0 : number >= 0);
}

function isValidScore(value) {
  const text = String(value ?? "").trim();
  if (!/^\d+(?:\.\d{1,2})?$/.test(text)) return false;
  const number = Number(text);
  return Number.isFinite(number) && number > 0 && number <= 100;
}

export function validateBlueprint(state, isSubmit = false) {
  const errors = [];
  const warnings = [];
  const name = (state.blueprintName || "").trim();

  if (!name || name.length > 100) errors.push("Tên cấu trúc đề phải có từ 1 đến 100 ký tự.");
  if (![10, 11, 12].includes(Number(state.grade))) errors.push("Khối lớp phải là 10, 11 hoặc 12.");
  if (!isInteger(state.totalQuestions, isSubmit)) errors.push("Tổng số câu không hợp lệ.");
  if (!isInteger(state.durationMinutes, isSubmit)) errors.push("Thời gian làm bài không hợp lệ.");
  if (!isValidScore(state.totalScore)) errors.push("Tổng điểm phải lớn hơn 0, không quá 100 và có tối đa 2 chữ số thập phân.");

  const sections = state.sections || [];
  if (sections.length === 0) errors.push("Cấu trúc đề phải có ít nhất một phần thi.");

  let totalQuestions = 0;
  let totalBudget = 0;
  sections.forEach((section, index) => {
    const label = `Phần ${index + 1}`;
    const sectionQuestions = Number(section.totalQuestions);
    const scoreBudget = Number(section.scoreBudget);

    if (!(section.sectionName || "").trim()) errors.push(`${label}: Tên phần không được để trống.`);
    if (!isInteger(section.totalQuestions, isSubmit)) errors.push(`${label}: Số câu không hợp lệ.`);
    if (!isValidScore(section.scoreBudget)) errors.push(`${label}: Quỹ điểm không hợp lệ.`);
    if ((section.details || []).length === 0) errors.push(`${label}: Phải có ít nhất một dòng phân bổ.`);

    totalQuestions += Number.isFinite(sectionQuestions) ? sectionQuestions : 0;
    totalBudget += Number.isFinite(scoreBudget) ? scoreBudget : 0;

    const isComposite = section.questionType === "Composite";
    if (isComposite) {
      if (!isInteger(section.partCountPerQuestion, true)) errors.push(`${label}: Số phần mỗi câu phải lớn hơn 0.`);
      if (!["TieredTrueFalse", "WeightedParts"].includes(section.scoringRule)) {
        errors.push(`${label}: Quy tắc chấm Composite không hợp lệ.`);
      }
      if (section.scoringRule === "TieredTrueFalse" && Number(section.partCountPerQuestion) !== 4) {
        errors.push(`${label}: TieredTrueFalse chỉ áp dụng cho đúng 4 mệnh đề.`);
      }
    } else if (section.scoringRule !== "AllOrNothing") {
      errors.push(`${label}: Câu không phải Composite phải dùng AllOrNothing.`);
    }

    const detailQuantity = (section.details || []).reduce((sum, detail) => {
      if (!detail.tagId || !detail.difficultyId || !isInteger(detail.quantity, true)) {
        errors.push(`${label}: Dòng phân bổ chủ đề, độ khó hoặc số lượng chưa hợp lệ.`);
      }
      return sum + (Number(detail.quantity) || 0);
    }, 0);
    if (detailQuantity !== sectionQuestions) {
      const message = `${label}: Tổng số lượng phân bổ (${detailQuantity}) phải bằng số câu (${sectionQuestions || 0}).`;
      (isSubmit ? errors : warnings).push(message);
    }
  });

  if (totalQuestions !== Number(state.totalQuestions)) {
    const message = `Tổng số câu của các phần (${totalQuestions}) phải bằng tổng số câu của cấu trúc (${Number(state.totalQuestions) || 0}).`;
    (isSubmit ? errors : warnings).push(message);
  }

  if (Math.abs(totalBudget - Number(state.totalScore)) > 0.001) {
    errors.push(`Tổng quỹ điểm các phần (${totalBudget.toFixed(2)}) phải bằng tổng điểm (${Number(state.totalScore || 0).toFixed(2)}).`);
  }

  return { isValid: errors.length === 0, errors, warnings };
}

export const validateBlueprintForDraft = (state) => validateBlueprint(state, false);
export const validateBlueprintForSubmit = (state) => validateBlueprint(state, true);
