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
    parentTagName: topic?.parentTagName || topic?.parentName || topic?.parentTag?.tagName || "Nhóm chung",
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

export default function PracticeSetupPanel() {
  const navigate = useNavigate();
  const location = useLocation();
  const preselectedTagId = location.state?.preselectedTagId ?? null;

  // Data states
  const [grade, setGrade] = useState(null);
  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  // Interaction states
  const [search, setSearch] = useState("");
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

      // Auto preselect if coming from recommendation card
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

  // Group topics by Grade -> Parent Group Name
  const groupedTopics = useMemo(() => {
    if (!Array.isArray(topics)) return [];

    const query = search.trim().toLowerCase();

    const filtered = topics.filter((t) => {
      if (!query) return true;
      const matchChild = (t.tagName || "").toLowerCase().includes(query);
      const matchParent = (t.parentTagName || "").toLowerCase().includes(query);
      return matchChild || matchParent;
    });

    const gradeMap = new Map();

    filtered.forEach((t) => {
      const topicGrade = t.grade || grade || 12;
      if (!gradeMap.has(topicGrade)) {
        gradeMap.set(topicGrade, new Map());
      }
      const parentMap = gradeMap.get(topicGrade);
      const parentName = t.parentTagName || "Nhóm chung";
      if (!parentMap.has(parentName)) {
        parentMap.set(parentName, []);
      }
      parentMap.get(parentName).push(t);
    });

    const sortedGrades = Array.from(gradeMap.keys()).sort((a, b) => b - a);

    return sortedGrades.map((g) => {
      const parentMap = gradeMap.get(g);
      const parentGroups = Array.from(parentMap.entries()).map(([parentName, childTopics]) => {
        childTopics.sort(compareTopicPracticeSiblings);
        return { parentName, childTopics };
      });
      parentGroups.sort((a, b) => a.parentName.localeCompare(b.parentName, "vi"));

      return {
        grade: g,
        isCurrentGrade: grade ? g === Number(grade) : true,
        parentGroups,
      };
    });
  }, [topics, search, grade]);

  const handleSelectTopic = (node) => {
    if (!node || !node.canGenerate) return;
    if (generatedTestRef.current.tagId !== node.tagId) {
      generatedTestRef.current = { tagId: null, testId: null };
    }
    setSelectedTag(node);
    setGenerationError("");
    setIsConfirmOpen(true);
  };

  // Execute Topic Practice Generation
  const handleConfirmGenerate = async () => {
    if (!selectedTag || generatingRef.current) return;
    generatingRef.current = true;
    setGenerating(true);
    setGenerationError("");

    try {
      let testId = null;

      if (
        generatedTestRef.current.tagId === selectedTag.tagId &&
        generatedTestRef.current.testId
      ) {
        testId = generatedTestRef.current.testId;
      } else {
        const genRes = await testGeneratorApi.generateTopicPractice(selectedTag.tagId);
        testId = genRes.data?.testId;

        if (!testId) {
          throw new Error("Không nhận được mã đề thi từ hệ thống.");
        }

        generatedTestRef.current = { tagId: selectedTag.tagId, testId };
      }

      const startData = await startSession(testId);
      const sessionId = startData?.sessionId || startData?.id;

      if (sessionId) {
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
              Danh mục chủ đề luyện tập
              {grade && (
                <span className="bg-primary/15 text-primary border border-primary/25 text-[10px] font-extrabold px-2 py-0.5 rounded">
                  Khối {grade}
                </span>
              )}
            </h2>
            <p className="text-xs text-on-surface-variant">Chọn một chủ đề bất kỳ để bắt đầu bài luyện tập gồm 10 câu.</p>
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
      ) : loadError ? null : topics.length === 0 ? (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant flex flex-col items-center justify-center gap-3">
          <span className="material-symbols-outlined text-[48px] text-outline-variant">topic</span>
          <p className="text-sm font-bold text-on-surface">Không tìm thấy chủ đề học tập nào trong danh mục.</p>
        </div>
      ) : groupedTopics.length === 0 ? (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant flex flex-col items-center justify-center gap-2.5">
          <span className="material-symbols-outlined text-[48px] text-outline-variant">search_off</span>
          <p className="text-sm font-bold text-on-surface">Không tìm thấy chủ đề nào khớp với từ khóa "{search.trim()}".</p>
          <p className="text-xs text-on-surface-variant">Vui lòng kiểm tra lại từ khóa hoặc xóa ô tìm kiếm để xem toàn bộ danh mục.</p>
        </div>
      ) : (
        /* Grouped Topics List */
        <div className="flex flex-col gap-6">
          {groupedTopics.map((gradeGroup) => (
            <div key={gradeGroup.grade} className="bg-pure-surface border border-whisper-border rounded-xl p-4 md:p-5 shadow-sm flex flex-col gap-4">

              {/* Grade Header */}
              <div className="flex items-center justify-between border-b border-whisper-border pb-3">
                <div className="flex items-center gap-2">
                  <span className="material-symbols-outlined text-primary text-[20px]">school</span>
                  <h3 className="text-sm font-bold text-on-surface">
                    {gradeGroup.isCurrentGrade
                      ? `Lớp ${gradeGroup.grade} (Khối lớp hiện tại)`
                      : `Ôn tập lớp ${gradeGroup.grade}`}
                  </h3>
                </div>
                {!gradeGroup.isCurrentGrade && (
                  <span className="px-2.5 py-0.5 rounded-full bg-secondary-container text-on-secondary-container text-[10px] font-bold">
                    Ôn tập lớp dưới
                  </span>
                )}
              </div>

              {/* Parent Groups */}
              <div className="flex flex-col gap-4">
                {gradeGroup.parentGroups.map((group) => (
                  <div key={group.parentName} className="flex flex-col gap-2">
                    {/* Non-interactive Parent Header */}
                    <div className="flex items-center gap-2 text-xs font-bold text-on-surface-variant bg-surface-container-low px-3 py-1.5 rounded-lg border border-whisper-border/50">
                      <span className="material-symbols-outlined text-[16px] text-primary">folder</span>
                      <span>{group.parentName}</span>
                    </div>

                    {/* Direct-Child Topics */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5">
                      {group.childTopics.map((node) => {
                        const hasWeakRecommendation = node.isWeakRecommended &&
                          node.weakTagName &&
                          node.recommendedDifficultyLevel !== null &&
                          node.officialPoint !== null;

                        return (
                          <div
                            key={node.tagId}
                            className={cn(
                              "p-3 rounded-xl border flex items-center justify-between gap-3 transition-all",
                              !node.canGenerate
                                ? "bg-surface-container-low/40 border-whisper-border/60 opacity-70"
                                : "bg-pure-surface border-whisper-border hover:border-primary/40 hover:shadow-sm"
                            )}
                          >
                            <div className="flex flex-col justify-center min-w-0 flex-1">
                              <div className="flex items-center gap-2 min-w-0">
                                <span className="text-xs font-bold truncate text-on-surface">
                                  {node.tagName}
                                </span>
                                {node.isWeakRecommended && (
                                  <span className="shrink-0 rounded-md border border-amber-500/30 bg-amber-500/10 px-1.5 py-0.5 text-[10px] font-bold text-amber-700">
                                    Cần củng cố
                                  </span>
                                )}
                              </div>
                              {hasWeakRecommendation ? (
                                <span className="mt-0.5 truncate text-[10px] text-on-surface-variant">
                                  Trọng tâm: {node.weakTagName} · Mức {node.recommendedDifficultyLevel} · {node.officialPoint.toFixed(2)}/10
                                </span>
                              ) : (
                                <span className="mt-0.5 text-[10px] text-on-surface-variant">
                                  {node.canGenerate
                                    ? `${node.availableQuestionCount}/10 câu hỏi hợp lệ`
                                    : `Chỉ có ${node.availableQuestionCount}/10 câu hợp lệ`}
                                </span>
                              )}
                            </div>

                            <div className="flex items-center gap-2 shrink-0">
                              {node.canGenerate ? (
                                <Button
                                  type="button"
                                  variant="primary"
                                  size="sm"
                                  onClick={() => handleSelectTopic(node)}
                                  aria-label={`Luyện tập chủ đề ${node.tagName}`}
                                  className="min-h-[44px] h-[44px] text-xs font-bold px-3.5 shrink-0"
                                >
                                  <span className="material-symbols-outlined text-[16px] mr-1">fitness_center</span>
                                  Luyện tập
                                </Button>
                              ) : (
                                <span className="text-[11px] text-on-surface-variant italic font-medium px-2">
                                  Chưa đủ câu
                                </span>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>

            </div>
          ))}
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
