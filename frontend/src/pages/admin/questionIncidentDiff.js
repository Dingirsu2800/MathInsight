function tokenize(value) {
  return String(value ?? "").match(/\s+|[^\s]+/g) || [];
}

export function diffText(original, edited) {
  if (original == null || edited == null) {
    return { status: "missing", segments: [] };
  }

  const originalTokens = tokenize(original);
  const editedTokens = tokenize(edited);
  const rows = Array.from({ length: originalTokens.length + 1 }, () =>
    Array(editedTokens.length + 1).fill(0)
  );

  for (let originalIndex = originalTokens.length - 1; originalIndex >= 0; originalIndex -= 1) {
    for (let editedIndex = editedTokens.length - 1; editedIndex >= 0; editedIndex -= 1) {
      rows[originalIndex][editedIndex] = originalTokens[originalIndex] === editedTokens[editedIndex]
        ? rows[originalIndex + 1][editedIndex + 1] + 1
        : Math.max(rows[originalIndex + 1][editedIndex], rows[originalIndex][editedIndex + 1]);
    }
  }

  const segments = [];
  const append = (type, value) => {
    if (!value) return;
    const previous = segments[segments.length - 1];
    if (previous?.type === type) previous.value += value;
    else segments.push({ type, value });
  };

  let originalIndex = 0;
  let editedIndex = 0;
  while (originalIndex < originalTokens.length && editedIndex < editedTokens.length) {
    if (originalTokens[originalIndex] === editedTokens[editedIndex]) {
      append("unchanged", originalTokens[originalIndex]);
      originalIndex += 1;
      editedIndex += 1;
    } else if (rows[originalIndex + 1][editedIndex] >= rows[originalIndex][editedIndex + 1]) {
      append("removed", originalTokens[originalIndex]);
      originalIndex += 1;
    } else {
      append("added", editedTokens[editedIndex]);
      editedIndex += 1;
    }
  }
  while (originalIndex < originalTokens.length) append("removed", originalTokens[originalIndex++]);
  while (editedIndex < editedTokens.length) append("added", editedTokens[editedIndex++]);

  return {
    status: original === edited ? "unchanged" : "changed",
    segments
  };
}

function parseSnapshot(version) {
  if (!version || typeof version.answersSnapshot !== "string" || !version.answersSnapshot.trim()) return null;
  try {
    return JSON.parse(version.answersSnapshot);
  } catch {
    return null;
  }
}

function toComparable(value) {
  return value == null ? null : JSON.stringify(value);
}

function getSnapshotValue(version, snapshot, key) {
  if (key === "questionContent") return version?.questionContent ?? null;
  if (key === "solutionContent") return getProperty(snapshot, key) ?? version?.questionAnswer ?? null;
  return getProperty(snapshot, key);
}

function getProperty(value, key) {
  if (!value) return null;
  return value[key] ?? value[key[0].toUpperCase() + key.slice(1)] ?? null;
}

export function compareVersions(original, edited) {
  const originalSnapshot = parseSnapshot(original);
  const editedSnapshot = parseSnapshot(edited);
  const textFields = [
    ["questionContent", "Nội dung câu hỏi"],
    ["solutionContent", "Lời giải / đáp án đúng"]
  ];
  const metadataFields = [
    ["questionType", "Dạng câu hỏi"],
    ["difficultyId", "Mức độ khó"],
    ["grade", "Khối lớp"],
    ["topics", "Chủ đề"],
    ["pictureUrl", "Hình ảnh"]
  ];

  return {
    textFields: textFields.map(([key, label]) => ({
      key,
      label,
      ...diffText(getSnapshotValue(original, originalSnapshot, key), getSnapshotValue(edited, editedSnapshot, key))
    })),
    metadataFields: metadataFields.map(([key, label]) => {
      const originalValue = getSnapshotValue(original, originalSnapshot, key);
      const editedValue = getSnapshotValue(edited, editedSnapshot, key);
      return {
        key,
        label,
        status: originalValue == null || editedValue == null ? "missing" : originalValue === editedValue || toComparable(originalValue) === toComparable(editedValue) ? "unchanged" : "changed",
        original: originalValue,
        edited: editedValue
      };
    }),
    options: compareItems(getProperty(originalSnapshot, "answers"), getProperty(editedSnapshot, "answers"), "answerId", "answerContent", "isCorrect"),
    parts: compareItems(getProperty(originalSnapshot, "parts"), getProperty(editedSnapshot, "parts"), "partId", "partContent", "partType")
  };
}

function compareItems(originalItems, editedItems, idKey, contentKey, secondaryKey) {
  if (!Array.isArray(originalItems) || !Array.isArray(editedItems)) return { status: "missing", items: [] };

  const normalizeItem = (item) => ({
    ...item,
    [idKey]: getProperty(item, idKey),
    [contentKey]: getProperty(item, contentKey),
    [secondaryKey]: getProperty(item, secondaryKey),
    partLabel: getProperty(item, "partLabel")
  });
  const normalizedOriginal = originalItems.map(normalizeItem);
  const normalizedEdited = editedItems.map(normalizeItem);
  const originalById = new Map(normalizedOriginal.map((item, index) => [item[idKey] ?? `original-${index}`, item]));
  const editedById = new Map(normalizedEdited.map((item, index) => [item[idKey] ?? `edited-${index}`, item]));
  const ids = [...new Set([...originalById.keys(), ...editedById.keys()])];
  const items = ids.map((id) => {
    const original = originalById.get(id) ?? null;
    const edited = editedById.get(id) ?? null;
    if (!original) return { id, status: "added", original, edited };
    if (!edited) return { id, status: "removed", original, edited };
    const content = diffText(getProperty(original, contentKey), getProperty(edited, contentKey));
    const comparableFields = (item) => Object.keys(item || {})
      .filter((key) => key !== idKey && key !== contentKey)
      .sort()
      .reduce((result, key) => ({ ...result, [key]: item[key] }), {});
    const secondaryChanged = toComparable(comparableFields(original)) !== toComparable(comparableFields(edited));
    return { id, status: content.status === "unchanged" && !secondaryChanged ? "unchanged" : "changed", original, edited, content, secondaryChanged };
  });

  return {
    status: items.some((item) => item.status !== "unchanged") ? "changed" : "unchanged",
    items
  };
}

export function displayComparableValue(value) {
  if (value == null) return "Không có dữ liệu để so sánh";
  if (Array.isArray(value)) return value.map((item) => item.tagId || item.partLabel || item).join(", ") || "Không có dữ liệu";
  if (typeof value === "boolean") return value ? "Có" : "Không";
  return String(value);
}
