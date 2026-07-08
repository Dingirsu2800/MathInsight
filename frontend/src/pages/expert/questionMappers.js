export function flattenTopicTree(nodes, depth = 0) {
  if (!nodes || !Array.isArray(nodes)) return [];
  let result = [];
  for (const node of nodes) {
    result.push({
      id: node.id || node.tagId,
      name: node.name || node.tagName,
      tagId: node.tagId || node.id,
      displayName: "  ".repeat(depth) + (node.name || node.tagName),
      depth: depth,
      ...node
    });
    if (node.children && node.children.length > 0) {
      result.push(...flattenTopicTree(node.children, depth + 1));
    }
  }
  return result;
}

export function mapBackendTypeToUiType(type) {
  if (!type) return "SINGLE_CHOICE";
  const upper = type.toUpperCase();
  if (upper === "SINGLECHOICE" || upper === "SINGLE_CHOICE") return "SINGLE_CHOICE";
  if (upper === "MULTIPLECHOICE" || upper === "MULTIPLE_CHOICE") return "MULTIPLE_CHOICE";
  if (upper === "TRUEFALSE" || upper === "TRUE_FALSE") return "TRUE_FALSE";
  if (upper === "SHORTANSWER" || upper === "SHORT_ANSWER") return "SHORT_ANSWER";
  if (upper === "COMPOSITE") return "COMPOSITE";
  return upper;
}

export function mapBackendDifficultyLevelToUi(level) {
  const val = parseInt(level);
  if (val === 1) return "easy";
  if (val === 2) return "medium";
  if (val === 3) return "hard";
  if (val === 4) return "very_hard";
  
  // If backend returns difficultyLevel as string, compare it case-insensitively
  if (typeof level === "string") {
    const lower = level.toLowerCase();
    if (lower === "easy") return "easy";
    if (lower === "medium") return "medium";
    if (lower === "hard") return "hard";
    if (lower === "very_hard" || lower === "veryhard") return "very_hard";
  }
  return "medium";
}

export function mapBackendPartTypeToUiType(type) {
  if (!type) return "TRUE_FALSE";
  const upper = type.toUpperCase();
  if (upper === "TRUEFALSE" || upper === "TRUE_FALSE") return "TRUE_FALSE";
  if (upper === "SHORTANSWER" || upper === "SHORT_ANSWER") return "SHORT_ANSWER";
  if (upper === "NUMERICANSWER" || upper === "NUMERIC_ANSWER") return "NUMERIC_ANSWER";
  return upper;
}

export function mapQuestionListItemToViewModel(item) {
  const primaryTopic = item.topics?.find(t => t.isPrimary) || item.topics?.[0];
  return {
    id: item.questionId,
    content: item.questionContent || "",
    pictureUrl: item.pictureUrl || "",
    difficultyId: item.difficultyId || "",
    difficulty: item.difficultyName || "Chưa xác định",
    difficultyLevel: mapBackendDifficultyLevelToUi(item.difficultyLevel),
    grade: item.grade ? item.grade.toString() : "12",
    status: item.status || "APPROVED",
    type: mapBackendTypeToUiType(item.questionType),
    expertId: item.expertId || "Hệ thống",
    points: item.defaultPoint ?? 0.2,
    topics: item.topics || [],
    topic: primaryTopic?.tagName || primaryTopic?.name || "Chưa phân loại"
  };
}

export function mapQuestionDetailToViewModel(detail) {
  if (!detail) return null;
  return {
    id: detail.questionId,
    content: detail.questionContent || "",
    pictureUrl: detail.pictureUrl || "",
    difficultyId: detail.difficultyId || "",
    difficulty: detail.difficultyName || "Chưa xác định",
    difficultyLevel: mapBackendDifficultyLevelToUi(detail.difficultyLevel),
    grade: detail.grade ? detail.grade.toString() : "12",
    status: detail.status || "APPROVED",
    type: mapBackendTypeToUiType(detail.questionType),
    expertId: detail.expertId || "Hệ thống",
    points: detail.defaultPoint ?? 0.2,
    topics: detail.topics || [],
    answers: detail.answers || [],
    solutionContent: detail.solutionContent || "",
    parts: (detail.parts || []).map(p => ({
      ...p,
      partType: mapBackendPartTypeToUiType(p.partType)
    }))
  };
}

export function mapQuestionDetailToEditorState(detail) {
  const uiType = mapBackendTypeToUiType(detail.questionType);
  
  // Extract short answer from answers if type is ShortAnswer
  let shortAnswerText = "";
  if (uiType === "SHORT_ANSWER" && detail.answers) {
    const correctAns = detail.answers.find(a => a.isCorrect);
    shortAnswerText = correctAns ? correctAns.answerContent : (detail.answers[0]?.answerContent || "");
  }

  // Map answers to options
  const uiOptions = (detail.answers || []).map(a => ({
    content: a.answerContent || "",
    isCorrect: !!a.isCorrect
  }));

  // Map composite parts
  const uiParts = (detail.parts || []).map(p => ({
    partOrder: p.partOrder || 1,
    partLabel: p.partLabel || "a",
    partContent: p.partContent || "",
    partType: mapBackendPartTypeToUiType(p.partType),
    correctBoolean: p.correctBoolean !== undefined ? p.correctBoolean : null,
    correctText: p.correctText || null,
    correctNumeric: p.correctNumeric !== undefined ? p.correctNumeric : null,
    numericTolerance: p.numericTolerance !== undefined ? p.numericTolerance : null,
    explanation: p.explanation || "",
    defaultPoint: p.defaultPoint ?? 0.05
  }));

  return {
    questionContent: detail.questionContent || "",
    solutionContent: detail.solutionContent || "",
    pictureUrl: detail.pictureUrl || "",
    grade: detail.grade || 12,
    questionType: uiType,
    difficultyId: detail.difficultyId || "",
    defaultPoint: detail.defaultPoint ?? 0.2,
    topics: detail.topics || [],
    options: uiType === "TRUE_FALSE"
      ? normalizeTrueFalseOptions(uiOptions)
      : (uiOptions.length > 0 ? uiOptions : [
          { content: "", isCorrect: false },
          { content: "", isCorrect: false }
        ]),
    shortAnswer: shortAnswerText,
    parts: uiParts
  };
}

function numberOrDefault(value, fallback) {
  const parsed = parseFloat(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

export function normalizeTrueFalseOptions(options = []) {
  const trueOption = options[0];
  const falseOption = options[1];

  const isFalseCorrect = falseOption?.isCorrect === true;

  return [
    { content: "Đúng", isCorrect: !isFalseCorrect },
    { content: "Sai", isCorrect: isFalseCorrect }
  ];
}

export function mapEditorStateToCreateUpdateRequest(state) {
  const isComposite = state.questionType === "COMPOSITE";

  // Build topics payload
  const topicsPayload = (state.topics || []).map(t => ({
    tagId: t.tagId || t.id,
    isPrimary: !!t.isPrimary
  }));

  const payload = {
    questionContent: state.questionContent,
    solutionContent: state.solutionContent,
    pictureUrl: state.pictureUrl || null,
    difficultyId: state.difficultyId,
    grade: parseInt(state.grade) || 12,
    questionType: state.questionType, // SINGLE_CHOICE, MULTIPLE_CHOICE, TRUE_FALSE, SHORT_ANSWER, COMPOSITE
    defaultPoint: numberOrDefault(state.defaultPoint, 0.2),
    topics: topicsPayload
  };

  if (isComposite) {
    payload.answers = [];
    payload.parts = state.parts.map((p, index) => {
      const parsedNumeric = parseFloat(p.correctNumeric);
      const parsedTolerance = parseFloat(p.numericTolerance);
      
      const mappedPart = {
        partOrder: index + 1,
        partLabel: String.fromCharCode(97 + index), // a, b, c, ...
        partContent: p.partContent,
        partType: p.partType, // TRUE_FALSE, SHORT_ANSWER, NUMERIC_ANSWER
        correctBoolean: p.partType === "TRUE_FALSE" ? (p.correctBoolean === true || p.correctBoolean === "true") : null,
        correctText: p.partType === "SHORT_ANSWER" ? p.correctText : null,
        correctNumeric: p.partType === "NUMERIC_ANSWER" ? (Number.isFinite(parsedNumeric) ? parsedNumeric : null) : null,
        numericTolerance: p.partType === "NUMERIC_ANSWER" ? (Number.isFinite(parsedTolerance) ? parsedTolerance : null) : null,
        explanation: p.explanation || "",
        defaultPoint: numberOrDefault(p.defaultPoint, 0.05)
      };
      return mappedPart;
    });
  } else {
    payload.parts = [];
    if (state.questionType === "SHORT_ANSWER") {
      payload.answers = [
        {
          answerContent: state.shortAnswer,
          isCorrect: true
        }
      ];
    } else {
      const opts = state.questionType === "TRUE_FALSE" ? normalizeTrueFalseOptions(state.options) : state.options;
      payload.answers = opts.map(opt => ({
        answerContent: opt.content,
        isCorrect: !!opt.isCorrect
      }));
    }
  }

  return payload;
}
