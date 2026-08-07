import React, { useState, useEffect, useRef, useMemo, useCallback } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { Button } from "../ui/button";
import TopicPracticeConfirmDialog from "./TopicPracticeConfirmDialog";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { startSession } from "../../services/testingApi";
import { getTopicPracticeErrorMessage } from "../../utils/topicPracticeErrorLocalizer";
import { cn } from "../../utils/cn";

function normalizeTopicPracticeOption(topic) {
  return {
    ...topic,
    isWeakRecommended: topic?.isWeakRecommended === true,
    officialPoint: Number.isFinite(Number(topic?.officialPoint))
      ? Number(topic.officialPoint)
      : null,
    evidenceCount: Number.isInteger(Number(topic?.evidenceCount))
      ? Number(topic.evidenceCount)
      : null,
    recommendedDifficultyLevel: Number.isInteger(Number(topic?.recommendedDifficultyLevel))
      ? Number(topic.recommendedDifficultyLevel)
      : null,
  };
}

function compareTopicPracticeSiblings(a, b) {
  if (a.isWeakRecommended !== b.isWeakRecommended) {
    return a.isWeakRecommended ? -1 : 1;
  }
  if (a.displayOrder !== b.displayOrder) return a.displayOrder - b.displayOrder;
  return (a.tagName || "").localeCompare(b.tagName || "", "vi");
}

// Helper 1: Build cycle-safe tree hierarchy from flat topic list
function buildCycleSafeTopicTree(rawTopics) {
  if (!Array.isArray(rawTopics)) return [];

  const topicMap = new Map();
  rawTopics.forEach((t) => {
    if (t && t.tagId) {
      topicMap.set(t.tagId, { ...t, children: [] });
    }
  });

  const roots = [];
  const parentChildEdges = [];

  topicMap.forEach((node) => {
    const parentId = node.parentTagId;
    if (parentId && parentId !== node.tagId && topicMap.has(parentId)) {
      parentChildEdges.push({ parentId, childNode: node });
    } else {
      roots.push(node);
    }
  });

  // Attach children with cycle detection
  parentChildEdges.forEach(({ parentId, childNode }) => {
    let curr = topicMap.get(parentId);
    let isCycle = false;
    const pathVisited = new Set([childNode.tagId]);

    while (curr) {
      if (pathVisited.has(curr.tagId)) {
        isCycle = true;
        break;
      }
      pathVisited.add(curr.tagId);
      curr = curr.parentTagId ? topicMap.get(curr.parentTagId) : null;
    }

    if (!isCycle) {
      topicMap.get(parentId).children.push(childNode);
    } else if (!roots.includes(childNode)) {
      roots.push(childNode);
    }
  });

  // Cycle-safe recursive sorting
  const sortVisited = new Set();
  const sortNodes = (nodeList) => {
    nodeList.sort(compareTopicPracticeSiblings);

    nodeList.forEach((n) => {
      if (!sortVisited.has(n.tagId)) {
        sortVisited.add(n.tagId);
        sortNodes(n.children);
      } else {
        n.children = [];
      }
    });
  };

  sortNodes(roots);
  return roots;
}

// Helper 2: Cycle-safe ancestor path tracing for search matching
function traceCycleSafeAncestors(topics, matchingTagIds) {
  const topicMap = new Map();
  topics.forEach((t) => {
    if (t && t.tagId) topicMap.set(t.tagId, t);
  });

  const ancestors = new Set();

  matchingTagIds.forEach((startTagId) => {
    const pathVisited = new Set([startTagId]);
    let curr = topicMap.get(startTagId);

    while (curr && curr.parentTagId) {
      const parentId = curr.parentTagId;
      if (pathVisited.has(parentId)) break;
      pathVisited.add(parentId);

      if (topicMap.has(parentId)) {
        ancestors.add(parentId);
        curr = topicMap.get(parentId);
      } else {
        break;
      }
    }
  });

  return ancestors;
}

export default function PracticeSetupPanel() {
  const navigate = useNavigate();
  const location = useLocation();
  // tagId được truyền từ WeakTopicsCard qua Link state
  const preselectedTagId = location.state?.preselectedTagId ?? null;

  // Data states
  const [grade, setGrade] = useState(null);
  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  // Interaction states
  const [search, setSearch] = useState("");
  const [expandedTagIds, setExpandedTagIds] = useState(new Set());
  const [selectedTag, setSelectedTag] = useState(null);

  // Dialog & Generation states
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [generationError, setGenerationError] = useState("");

  // Submit lock & Retain generated TestId ref
  const generatingRef = useRef(false);
  const generatedTestRef = useRef({ tagId: null, testId: null });

  // Fetch topic practice options
  const fetchOptions = useCallback(async () => {
    setLoading(true);
    setLoadError("");
    try {
      const res = await testGeneratorApi.getTopicPracticeOptions();
      const data = res.data || {};
      setGrade(data.grade || null);
      const rawTopics = Array.isArray(data.topics)
        ? data.topics.map(normalizeTopicPracticeOption)
        : [];
      setTopics(rawTopics);

      // Auto-expand top-level root nodes
      const parentIds = new Set(rawTopics.filter((t) => !t.parentTagId).map((t) => t.tagId));
      setExpandedTagIds(parentIds);

      // Nếu điều hướng từ WeakTopicsCard với preselectedTagId → auto-open confirm dialog
      if (preselectedTagId) {
        const preselected = rawTopics.find((t) => String(t.tagId) === String(preselectedTagId));
        if (preselected && preselected.canGenerate) {
          setSelectedTag(preselected);
          setGenerationError("");
          setIsConfirmOpen(true);
        }
      }
    } catch (err) {
      setLoadError(getTopicPracticeErrorMessage(err, "Không thể tải danh sách chủ đề luyện tập. Vui lòng thử lại sau."));
    } finally {
      setLoading(false);
    }
  }, [preselectedTagId]);

  useEffect(() => {
    fetchOptions();
  }, [fetchOptions]);

  // Build tree & search ancestor path
  const { tree, matchingTagIds, ancestorTagIds } = useMemo(() => {
    const rootNodes = buildCycleSafeTopicTree(topics);

    const query = search.trim().toLowerCase();
    const matching = new Set();

    if (query) {
      topics.forEach((t) => {
        if (t && t.tagName && t.tagName.toLowerCase().includes(query)) {
          matching.add(t.tagId);
        }
      });
    }

    const ancestors = traceCycleSafeAncestors(topics, matching);
    return { tree: rootNodes, matchingTagIds: matching, ancestorTagIds: ancestors };
  }, [topics, search]);

  // Auto expand matching ancestor nodes when searching
  useEffect(() => {
    if (search.trim() && ancestorTagIds.size > 0) {
      setExpandedTagIds((prev) => new Set([...prev, ...ancestorTagIds]));
    }
  }, [search, ancestorTagIds]);

  const toggleExpand = (tagId, e) => {
    if (e) e.stopPropagation();
    setExpandedTagIds((prev) => {
      const next = new Set(prev);
      if (next.has(tagId)) {
        next.delete(tagId);
      } else {
        next.add(tagId);
      }
      return next;
    });
  };

  const handleSelectTopic = (node) => {
    if (!node || !node.canGenerate) return;
    // Reset generated test cache if user selects a different topic
    if (generatedTestRef.current.tagId !== node.tagId) {
      generatedTestRef.current = { tagId: null, testId: null };
    }
    setSelectedTag(node);
    setGenerationError("");
    setIsConfirmOpen(true);
  };

  // Execute Topic Practice Generation & Start Session with Submit Lock & TestId reuse
  const handleConfirmGenerate = async () => {
    if (!selectedTag || generatingRef.current) return;
    generatingRef.current = true;
    setGenerating(true);
    setGenerationError("");

    try {
      let testId = null;

      // Check if we already have a generated TestId for this topic (from a previous failed startSession)
      if (
        generatedTestRef.current.tagId === selectedTag.tagId &&
        generatedTestRef.current.testId
      ) {
        testId = generatedTestRef.current.testId;
      } else {
        // 1. Generate personal TopicPractice Test
        const genRes = await testGeneratorApi.generateTopicPractice(selectedTag.tagId);
        testId = genRes.data?.testId;

        if (!testId) {
          throw new Error("Không nhận được mã đề thi từ hệ thống.");
        }

        // Cache generated testId for retry
        generatedTestRef.current = { tagId: selectedTag.tagId, testId };
      }

      // 2. Start Testing Session via testingApi (boundary requirement)
      const startData = await startSession(testId);
      const sessionId = startData?.sessionId || startData?.id;

      if (sessionId) {
        // Reset cache on successful start & navigate
        generatedTestRef.current = { tagId: null, testId: null };
        setIsConfirmOpen(false);
        navigate(`/student/test/${sessionId}`);
      } else {
        throw new Error("Không thể khởi tạo phiên làm bài.");
      }
    } catch (err) {
      const errCode = err.response?.data?.code;

      if (errCode === "TESTING_SESSION_ALREADY_IN_PROGRESS") {
        const existingSessionId = err.response?.data?.existingSessionId;
        if (existingSessionId && typeof existingSessionId === "string") {
          generatedTestRef.current = { tagId: null, testId: null };
          setIsConfirmOpen(false);
          navigate(`/student/test/${existingSessionId}`);
          return;
        }
      }

      setGenerationError(getTopicPracticeErrorMessage(err, "Không thể tạo bài luyện tập. Vui lòng thử lại sau."));
    } finally {
      generatingRef.current = false;
      setGenerating(false);
    }
  };

  // Render tree node with 44px+ touch targets and non-nested interactive elements (Finding 3 & 6)
  const renderTreeNode = (node, depth = 0, visitedSet = new Set()) => {
    if (!node || !node.tagId || visitedSet.has(node.tagId)) return null;
    visitedSet.add(node.tagId);

    const isExpanded = expandedTagIds.has(node.tagId);
    const hasChildren = Array.isArray(node.children) && node.children.length > 0;
    const isSearchMode = !!search.trim();
    const hasWeakRecommendation = node.isWeakRecommended &&
      node.weakTagName &&
      node.recommendedDifficultyLevel !== null &&
      node.officialPoint !== null;

    if (isSearchMode && !matchingTagIds.has(node.tagId) && !ancestorTagIds.has(node.tagId)) {
      return null;
    }

    const isMatchHighlight = isSearchMode && matchingTagIds.has(node.tagId);

    return (
      <div key={node.tagId} className="flex flex-col">
        {/* NON-INTERACTIVE Row Container */}
        <div
          style={{ paddingLeft: `${Math.max(12, depth * 24 + 12)}px` }}
          className={cn(
            "min-h-[52px] py-1.5 pr-3.5 rounded-xl border flex items-center justify-between gap-3 transition-all select-none my-0.5",
            !node.canGenerate
              ? "bg-surface-container-low/40 border-whisper-border/60 opacity-70"
              : isMatchHighlight
              ? "bg-primary/10 border-primary ring-1 ring-primary/30"
              : "bg-pure-surface border-whisper-border hover:border-primary/30"
          )}
        >
          {/* Left Side: Separate Expand Button (44px min touch target) + Separate Topic Title Button/Label */}
          <div className="flex items-center gap-2 min-w-0 flex-1">
            {hasChildren ? (
              <button
                type="button"
                aria-expanded={isExpanded}
                aria-label={isExpanded ? `Thu gọn ${node.tagName}` : `Mở rộng ${node.tagName}`}
                onClick={(e) => toggleExpand(node.tagId, e)}
                className="min-w-[44px] min-h-[44px] rounded-xl flex items-center justify-center text-on-surface-variant hover:text-on-surface hover:bg-surface-container-high transition-colors shrink-0 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              >
                <span className="material-symbols-outlined text-[20px]">
                  {isExpanded ? "expand_more" : "chevron_right"}
                </span>
              </button>
            ) : (
              <div className="min-w-[44px] min-h-[44px] shrink-0 flex items-center justify-center">
                <span className="material-symbols-outlined text-[14px] text-on-surface-variant/40">circle</span>
              </div>
            )}

            {node.canGenerate ? (
              <button
                type="button"
                onClick={() => handleSelectTopic(node)}
                aria-label={`Chọn chủ đề ${node.tagName}`}
                className="flex flex-col justify-center text-left min-w-0 flex-1 min-h-[44px] py-1 px-2 rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-primary transition-colors group"
              >
                <span className="flex items-center gap-2 min-w-0">
                  <span className="text-xs font-bold truncate text-on-surface group-hover:text-primary transition-colors">
                    {node.tagName}
                  </span>
                  {node.isWeakRecommended && (
                    <span className="shrink-0 rounded-md border border-amber-500/30 bg-amber-500/10 px-1.5 py-0.5 text-[10px] font-bold text-amber-700">
                      Cần củng cố
                    </span>
                  )}
                </span>
                {hasWeakRecommendation && (
                  <span className="mt-0.5 truncate text-[10px] text-on-surface-variant">
                    Trọng tâm: {node.weakTagName} · Mức {node.recommendedDifficultyLevel} · {node.officialPoint.toFixed(2)}/10
                  </span>
                )}
              </button>
            ) : (
              <div className="flex flex-col justify-center text-left min-w-0 flex-1 min-h-[44px] py-1 px-2">
                <span className="text-xs font-bold truncate text-on-surface-variant">
                  {node.tagName}
                </span>
                <span className="text-[10px] text-error font-medium">
                  Chỉ có {node.availableQuestionCount}/10 câu hợp lệ
                </span>
              </div>
            )}
          </div>

          {/* Right Side: Question Count Badge + Separate Action Button (44px min touch target) */}
          <div className="flex items-center gap-2 shrink-0">
            <span className={cn(
              "px-2.5 py-1 rounded-lg text-[11px] font-bold font-mono border",
              node.canGenerate
                ? "bg-emerald-success/10 text-emerald-success border-emerald-success/20"
                : "bg-surface-container-high text-on-surface-variant border-whisper-border"
            )}>
              {node.availableQuestionCount}/10 câu
            </span>

            {node.canGenerate ? (
              <Button
                type="button"
                variant="primary"
                size="sm"
                onClick={() => handleSelectTopic(node)}
                aria-label={`Luyện tập chủ đề ${node.tagName}`}
                className="min-h-[44px] h-[44px] text-xs font-bold px-4 shrink-0 focus-visible:ring-2 focus-visible:ring-primary"
              >
                <span className="material-symbols-outlined text-[16px] mr-1.5">fitness_center</span>
                Luyện tập
              </Button>
            ) : (
              <span className="text-[11px] text-on-surface-variant italic font-medium hidden sm:inline px-2">
                Chưa đủ câu
              </span>
            )}
          </div>
        </div>

        {/* Render Children */}
        {hasChildren && isExpanded && (
          <div className="flex flex-col">
            {node.children.map((child) => renderTreeNode(child, depth + 1, visitedSet))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-6 select-none">
      {/* Header & Search Bar */}
      <div className="bg-pure-surface border border-whisper-border rounded-xl p-4 md:p-5 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary/10 border border-primary/20 flex items-center justify-center shrink-0">
            <span className="material-symbols-outlined text-primary text-[22px]">auto_stories</span>
          </div>
          <div>
            <h2 className="text-sm font-bold text-on-surface flex items-center gap-2">
              Danh mục chủ đề bài học
              {grade && (
                <span className="bg-primary/15 text-primary border border-primary/25 text-[10px] font-extrabold px-2 py-0.5 rounded">
                  Khối {grade}
                </span>
              )}
            </h2>
            <p className="text-xs text-on-surface-variant">Chọn một chủ đề bất kỳ để tạo bài luyện tập 10 câu hỏi cố định.</p>
          </div>
        </div>

        {/* Search Input */}
        <div className="relative w-full md:w-72 select-text">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-[18px] pointer-events-none">
            search
          </span>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm kiếm chủ đề..."
            aria-label="Tìm kiếm chủ đề"
            className="w-full h-10 pl-9 pr-8 bg-surface-container-low border border-whisper-border rounded-xl text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
          />
          {search && (
            <button
              type="button"
              onClick={() => setSearch("")}
              aria-label="Xóa từ khóa tìm kiếm"
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-surface focus:outline-none focus-visible:ring-1 focus-visible:ring-primary rounded"
            >
              <span className="material-symbols-outlined text-[16px]">close</span>
            </button>
          )}
        </div>
      </div>

      {/* Error State */}
      {loadError && (
        <div role="alert" className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center justify-between gap-3 select-text">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-[20px] shrink-0">error</span>
            <span>{loadError}</span>
          </div>
          <Button variant="outline" size="sm" onClick={fetchOptions} className="h-8 text-xs font-bold min-h-[44px]">
            Thử lại
          </Button>
        </div>
      )}

      {/* Loading State */}
      {loading ? (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-6 shadow-sm flex flex-col gap-3">
          {Array.from({ length: 5 }).map((_, idx) => (
            <div key={idx} className="h-12 bg-surface-container-low rounded-xl animate-pulse" />
          ))}
        </div>
      ) : loadError ? null : tree.length === 0 ? (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant flex flex-col items-center justify-center gap-3">
          <span className="material-symbols-outlined text-[48px] text-outline-variant">topic</span>
          <p className="text-sm font-bold text-on-surface">Không tìm thấy chủ đề học tập nào trong danh mục.</p>
        </div>
      ) : search.trim() && matchingTagIds.size === 0 ? (
        /* Search Empty State (Finding 5) */
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant flex flex-col items-center justify-center gap-2.5">
          <span className="material-symbols-outlined text-[48px] text-outline-variant">search_off</span>
          <p className="text-sm font-bold text-on-surface">Không tìm thấy chủ đề nào khớp với từ khóa "{search.trim()}".</p>
          <p className="text-xs text-on-surface-variant">Vui lòng kiểm tra lại từ khóa hoặc xóa ô tìm kiếm để xem toàn bộ danh mục.</p>
        </div>
      ) : (
        /* Topic Tree List */
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-3 md:p-4 shadow-sm flex flex-col gap-1">
          {tree.map((rootNode) => renderTreeNode(rootNode, 0, new Set()))}
        </div>
      )}

      {/* Topic Practice Confirmation Dialog */}
      <TopicPracticeConfirmDialog
        isOpen={isConfirmOpen}
        onClose={() => {
          if (!generating) {
            setIsConfirmOpen(false);
            setGenerationError("");
          }
        }}
        topic={selectedTag}
        onConfirm={handleConfirmGenerate}
        submitting={generating}
        errorMessage={generationError}
      />
    </div>
  );
}
