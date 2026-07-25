import * as React from "react";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { CustomSelect } from "../../components/ui/custom-select";
import { questionBankApi } from "../../services/questionBankApi";
import { cn } from "../../utils/cn";

function flattenBackendTopics(nodes, parentId = null) {
  if (!nodes || !Array.isArray(nodes)) return [];
  let result = [];
  for (const node of nodes) {
    const currentId = node.tagId || node.id;
    result.push({
      tagId: currentId,
      id: currentId,
      tagName: node.tagName || node.name || "",
      name: node.tagName || node.name || "",
      description: node.description || "",
      grade: node.grade || 12,
      parentTagId: node.parentTagId || node.parentId || parentId,
      displayOrder: node.displayOrder || 1,
      isActive: node.isActive !== undefined ? node.isActive : true
    });
    if (node.children && node.children.length > 0) {
      result.push(...flattenBackendTopics(node.children, currentId));
    }
  }
  return result;
}

export default function TagManagementPage() {
  const [activeTab, setActiveTab] = React.useState("topic"); // "topic" or "difficulty"
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");
  const [successMessage, setSuccessMessage] = React.useState("");

  // Data states
  const [topics, setTopics] = React.useState([]);
  const [difficulties, setDifficulties] = React.useState([]);

  // Expanded tree states for topics
  const [expandedTopics, setExpandedTopics] = React.useState(new Set());

  // Search & Filter states
  const [topicSearch, setTopicSearch] = React.useState("");
  const [topicGrade, setTopicGrade] = React.useState("ALL"); // "ALL", "10", "11", "12"
  const [topicStatus, setTopicStatus] = React.useState("ALL"); // "ALL", "ACTIVE", "INACTIVE"

  const [diffSearch, setDiffSearch] = React.useState("");
  const [diffStatus, setDiffStatus] = React.useState("ALL"); // "ALL", "ACTIVE", "INACTIVE"

  // Dialog / Modal states
  const [isTopicDialogOpen, setIsTopicDialogOpen] = React.useState(false);
  const [topicDialogMode, setTopicDialogMode] = React.useState("create"); // "create" or "edit"
  const [currentTopic, setCurrentTopic] = React.useState(null);

  const [isDiffDialogOpen, setIsDiffDialogOpen] = React.useState(false);
  const [diffDialogMode, setDiffDialogMode] = React.useState("create"); // "create" or "edit"
  const [currentDiff, setCurrentDiff] = React.useState(null);

  const [isConfirmDisableOpen, setIsConfirmDisableOpen] = React.useState(false);
  const [disableTarget, setDisableTarget] = React.useState(null); // { type: "topic"|"difficulty", item }

  // Form states - Topic
  const [formTopicName, setFormTopicName] = React.useState("");
  const [formTopicDescription, setFormTopicDescription] = React.useState("");
  const [formTopicGrade, setFormTopicGrade] = React.useState("12");
  const [formTopicParentId, setFormTopicParentId] = React.useState("");
  const [formTopicOrder, setFormTopicOrder] = React.useState("1");
  const [formTopicValidation, setFormTopicValidation] = React.useState("");

  // Form states - Difficulty
  const [formDiffName, setFormDiffName] = React.useState("");
  const [formDiffDescription, setFormDiffDescription] = React.useState("");
  const [formDiffLevel, setFormDiffLevel] = React.useState("1");
  const [formDiffOrder, setFormDiffOrder] = React.useState("1");
  const [formDiffValidation, setFormDiffValidation] = React.useState("");

  // Fetch Topic and Difficulty lists
  const loadData = async () => {
    setLoading(true);
    setError("");
    try {
      if (activeTab === "topic") {
        const res = await questionBankApi.getTopicTags({ includeInactive: true });
        const flattened = flattenBackendTopics(res.data || []);
        setTopics(flattened);
      } else {
        const res = await questionBankApi.getDifficulties({ includeInactive: true });
        setDifficulties(res.data || []);
      }
    } catch (err) {
      console.error(err);
      setError("Không thể tải danh sách dữ liệu từ máy chủ.");
    } finally {
      setLoading(false);
    }
  };

  React.useEffect(() => {
    loadData();
  }, [activeTab]);

  // Expand / Collapse chevrons
  const toggleExpand = (id) => {
    setExpandedTopics((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  // Helper: check active descendants before disabling
  const hasActiveDescendants = (topicId, list) => {
    const children = list.filter(t => t.parentTagId === topicId || t.parentId === topicId);
    for (const child of children) {
      const isChildActive = child.isActive !== undefined ? child.isActive : true;
      if (isChildActive) return true;
      if (hasActiveDescendants(child.tagId || child.id, list)) return true;
    }
    return false;
  };

  // Switch triggers
  const handleToggleActive = (item, type) => {
    const currentActive = item.isActive !== undefined ? item.isActive : true;

    if (currentActive) {
      // Toggling off requires check and confirm modal
      if (type === "topic") {
        const topicId = item.tagId || item.id;
        if (hasActiveDescendants(topicId, topics)) {
          setError("Không thể ngừng sử dụng chủ đề này vì vẫn còn các chủ đề con đang hoạt động.");
          window.scrollTo({ top: 0, behavior: "smooth" });
          return;
        }
      }
      setDisableTarget({ type, item });
      setIsConfirmDisableOpen(true);
    } else {
      // Toggling on proceeds directly
      proceedToggleActive(item, type, true);
    }
  };

  const proceedToggleActive = async (item, type, nextActiveState) => {
    setError("");
    setSuccessMessage("");
    try {
      if (type === "topic") {
        const id = item.tagId || item.id;
        await questionBankApi.updateTopic(id, {
          tagName: item.tagName || item.name,
          description: item.description || "",
          grade: parseInt(item.grade) || 12,
          parentTagId: item.parentTagId || null,
          displayOrder: parseInt(item.displayOrder) || 1,
          isActive: nextActiveState
        });
        setSuccessMessage(`Đã cập nhật trạng thái hoạt động của chủ đề.`);
      } else {
        const id = item.difficultyId || item.id;
        await questionBankApi.updateDifficulty(id, {
          difficultyName: item.difficultyName || item.name,
          description: item.description || "",
          levelValue: parseInt(item.levelValue) || 1,
          displayOrder: parseInt(item.displayOrder) || 1,
          isActive: nextActiveState
        });
        setSuccessMessage(`Đã cập nhật trạng thái hoạt động của độ khó.`);
      }
      loadData();
    } catch (err) {
      console.error(err);
      if (err.response?.status === 409) {
        setError(err.response?.data?.message || "Không thể ngừng sử dụng vì tag này đang được sử dụng.");
      } else {
        setError("Lỗi cập nhật trạng thái hoạt động của tag.");
      }
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  // Open creation dialogs
  const handleOpenCreateDialog = () => {
    setError("");
    setSuccessMessage("");
    if (activeTab === "topic") {
      setTopicDialogMode("create");
      setFormTopicName("");
      setFormTopicDescription("");
      setFormTopicGrade("12");
      setFormTopicParentId("");
      setFormTopicOrder("1");
      setFormTopicValidation("");
      setIsTopicDialogOpen(true);
    } else {
      setDiffDialogMode("create");
      setFormDiffName("");
      setFormDiffDescription("");
      setFormDiffLevel("1");
      setFormDiffOrder("1");
      setFormDiffValidation("");
      setIsDiffDialogOpen(true);
    }
  };

  // Open edit dialogs
  const handleOpenEditTopic = (topic) => {
    setError("");
    setSuccessMessage("");
    setTopicDialogMode("edit");
    setCurrentTopic(topic);
    setFormTopicName(topic.tagName || topic.name || "");
    setFormTopicDescription(topic.description || "");
    setFormTopicGrade(topic.grade ? topic.grade.toString() : "12");
    setFormTopicParentId(topic.parentTagId || topic.parentId || "");
    setFormTopicOrder(topic.displayOrder ? topic.displayOrder.toString() : "1");
    setFormTopicValidation("");
    setIsTopicDialogOpen(true);
  };

  const handleOpenEditDiff = (diff) => {
    setError("");
    setSuccessMessage("");
    setDiffDialogMode("edit");
    setCurrentDiff(diff);
    setFormDiffName(diff.difficultyName || diff.name || "");
    setFormDiffDescription(diff.description || "");
    setFormDiffLevel(diff.levelValue ? diff.levelValue.toString() : "1");
    setFormDiffOrder(diff.displayOrder ? diff.displayOrder.toString() : "1");
    setFormDiffValidation("");
    setIsDiffDialogOpen(true);
  };

  // Form submit handlers
  const handleSaveTopic = async (e) => {
    e.preventDefault();
    setFormTopicValidation("");
    if (!formTopicName.trim()) {
      setFormTopicValidation("Tên chủ đề là bắt buộc.");
      return;
    }
    const orderNum = parseInt(formTopicOrder);
    if (isNaN(orderNum) || orderNum <= 0) {
      setFormTopicValidation("Thứ tự hiển thị phải là số nguyên dương.");
      return;
    }

    const payload = {
      tagName: formTopicName.trim(),
      description: formTopicDescription.trim(),
      displayOrder: orderNum,
      grade: parseInt(formTopicGrade),
      parentTagId: formTopicParentId ? formTopicParentId : null,
      isActive: topicDialogMode === "create" ? true : (currentTopic.isActive !== undefined ? currentTopic.isActive : true)
    };

    try {
      if (topicDialogMode === "create") {
        await questionBankApi.createTopic(payload);
        setSuccessMessage("Tạo chủ đề mới thành công.");
      } else {
        const id = currentTopic.tagId || currentTopic.id;
        await questionBankApi.updateTopic(id, payload);
        setSuccessMessage("Cập nhật thông tin chủ đề thành công.");
      }
      setIsTopicDialogOpen(false);
      loadData();
    } catch (err) {
      console.error(err);
      setFormTopicValidation(err.response?.data?.message || "Lỗi lưu thông tin chủ đề. Vui lòng kiểm tra lại.");
    }
  };

  const handleSaveDiff = async (e) => {
    e.preventDefault();
    setFormDiffValidation("");
    if (!formDiffName.trim()) {
      setFormDiffValidation("Tên độ khó là bắt buộc.");
      return;
    }
    const orderNum = parseInt(formDiffOrder);
    if (isNaN(orderNum) || orderNum <= 0) {
      setFormDiffValidation("Thứ tự hiển thị phải là số nguyên dương.");
      return;
    }

    const payload = {
      difficultyName: formDiffName.trim(),
      description: formDiffDescription.trim(),
      displayOrder: orderNum,
      levelValue: parseInt(formDiffLevel),
      isActive: diffDialogMode === "create" ? true : (currentDiff.isActive !== undefined ? currentDiff.isActive : true)
    };

    try {
      if (diffDialogMode === "create") {
        await questionBankApi.createDifficulty(payload);
        setSuccessMessage("Tạo độ khó mới thành công.");
      } else {
        const id = currentDiff.difficultyId || currentDiff.id;
        await questionBankApi.updateDifficulty(id, payload);
        setSuccessMessage("Cập nhật thông tin độ khó thành công.");
      }
      setIsDiffDialogOpen(false);
      loadData();
    } catch (err) {
      console.error(err);
      setFormDiffValidation(err.response?.data?.message || "Lỗi lưu thông tin độ khó. Vui lòng kiểm tra lại.");
    }
  };

  // Hierarchical parser utility for rendering
  const buildTopicTree = (flatList) => {
    const map = {};
    const roots = [];

    // Convert all items to standardized format
    const normalized = flatList.map(item => ({
      tagId: item.tagId || item.id,
      id: item.tagId || item.id,
      tagName: item.tagName || item.name || "",
      name: item.tagName || item.name || "",
      description: item.description || "",
      grade: item.grade || 12,
      parentTagId: item.parentTagId || item.parentId || null,
      displayOrder: item.displayOrder || 1,
      isActive: item.isActive !== undefined ? item.isActive : true
    }));

    normalized.forEach(item => {
      map[item.id] = { ...item, children: [] };
    });

    normalized.forEach(item => {
      const parentId = item.parentTagId;
      if (parentId && map[parentId]) {
        map[parentId].children.push(map[item.id]);
      } else if (!parentId) {
        roots.push(map[item.id]);
      }
    });

    // Sort by DisplayOrder
    roots.sort((a, b) => a.displayOrder - b.displayOrder);
    const sortChildren = (node) => {
      if (node.children) {
        node.children.sort((a, b) => a.displayOrder - b.displayOrder);
        node.children.forEach(sortChildren);
      }
    };
    roots.forEach(sortChildren);

    return roots;
  };

  const flattenParsedTree = (nodes, depth = 0) => {
    let result = [];
    for (const node of nodes) {
      result.push({
        ...node,
        depth
      });
      if (node.children && node.children.length > 0) {
        result.push(...flattenParsedTree(node.children, depth + 1));
      }
    }
    return result;
  };

  // Filter topics lists
  const getFilteredTopics = () => {
    let matchedIds = new Set();

    // 1. Find all items matching search, grade, status
    topics.forEach(t => {
      let matchesSearch = true;
      if (topicSearch.trim()) {
        const query = topicSearch.toLowerCase();
        matchesSearch = (t.tagName || t.name || "").toLowerCase().includes(query);
      }

      let matchesGrade = true;
      if (topicGrade !== "ALL") {
        matchesGrade = t.grade === parseInt(topicGrade);
      }

      let matchesStatus = true;
      if (topicStatus !== "ALL") {
        const activeOnly = topicStatus === "ACTIVE";
        matchesStatus = t.isActive === activeOnly;
      }

      if (matchesSearch && matchesGrade && matchesStatus) {
        matchedIds.add(t.tagId || t.id);
      }
    });

    // 2. Add all ancestors of matched items to avoid visual truncation
    let finalSet = new Set(matchedIds);
    matchedIds.forEach(id => {
      let current = topics.find(t => (t.tagId || t.id) === id);
      while (current && current.parentTagId) {
        finalSet.add(current.parentTagId);
        current = topics.find(t => (t.tagId || t.id) === current.parentTagId);
      }
    });

    // 3. Build list of matched items from the master normalized topics list
    const filteredFlat = topics.filter(t => finalSet.has(t.tagId || t.id));

    // 4. Build tree hierarchy & flatten it
    const tree = buildTopicTree(filteredFlat);
    return flattenParsedTree(tree);
  };

  // Filter difficulties lists
  const getFilteredDifficulties = () => {
    let list = difficulties.map(item => ({
      difficultyId: item.difficultyId || item.id,
      id: item.difficultyId || item.id,
      difficultyName: item.difficultyName || item.name || "",
      name: item.difficultyName || item.name || "",
      description: item.description || "",
      displayOrder: item.displayOrder || 1,
      levelValue: item.levelValue || 1,
      isActive: item.isActive !== undefined ? item.isActive : true
    }));

    if (diffSearch.trim()) {
      const query = diffSearch.toLowerCase();
      list = list.filter(d => d.difficultyName.toLowerCase().includes(query));
    }

    if (diffStatus !== "ALL") {
      const activeOnly = diffStatus === "ACTIVE";
      list = list.filter(d => d.isActive === activeOnly);
    }

    // Sort by display order
    return list.sort((a, b) => a.displayOrder - b.displayOrder);
  };

  const filteredTopicsList = getFilteredTopics();
  const filteredDifficultiesList = getFilteredDifficulties();

  // Helper check visible nodes on Expand/Collapse
  const isTopicVisible = (topic) => {
    if (topicSearch.trim() || topicGrade !== "ALL" || topicStatus !== "ALL") return true; // Show all when any filter is active
    let current = topic;
    while (current.parentTagId) {
      const parent = topics.find(t => (t.tagId || t.id) === current.parentTagId);
      if (!parent) break;
      const parentId = parent.tagId || parent.id;
      if (!expandedTopics.has(parentId)) {
        return false;
      }
      current = {
        ...parent,
        parentTagId: parent.parentTagId || parent.parentId
      };
    }
    return true;
  };

  // Get active parent options filtered to selected grade
  const getTopicParentOptions = () => {
    const gradeVal = parseInt(formTopicGrade);
    return topics.filter(t => {
      const isAct = t.isActive !== undefined ? t.isActive : true;
      const sameGrade = t.grade === gradeVal;
      const isNotSelf = topicDialogMode === "create" || (t.tagId || t.id) !== (currentTopic?.tagId || currentTopic?.id);
      return isAct && sameGrade && isNotSelf;
    });
  };

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">

        {/* Page Header */}
        <DashboardPageHeader
          title="Quản lý Tag"
          subtitle="Quản lý hệ thống chủ đề và độ khó dùng để phân loại, sinh đề và gợi ý bài luyện."
        >
          <Button onClick={handleOpenCreateDialog}>
            <span className="material-symbols-outlined text-[18px] mr-1.5">add</span>
            {activeTab === "topic" ? "Tạo chủ đề" : "Tạo độ khó"}
          </Button>
        </DashboardPageHeader>

        {/* Global Error Banner */}
        {error && (
          <div className="p-4 bg-error/10 border border-error/20 text-error rounded-xl flex items-center gap-3 text-sm font-semibold shadow-sm animate-in fade-in duration-200" role="alert">
            <span className="material-symbols-outlined text-[20px] shrink-0">error</span>
            <div className="flex-1">{error}</div>
            <button onClick={() => setError("")} className="text-error/70 hover:text-error">
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {/* Global Success Banner */}
        {successMessage && (
          <div className="p-4 bg-emerald-success/10 border border-emerald-success/20 text-emerald-success rounded-xl flex items-center gap-3 text-sm font-semibold shadow-sm animate-in fade-in duration-200" role="alert">
            <span className="material-symbols-outlined text-[20px] shrink-0">check_circle</span>
            <div className="flex-1">{successMessage}</div>
            <button onClick={() => setSuccessMessage("")} className="text-emerald-success/70 hover:text-emerald-success">
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {/* Tab Selection Navigation */}
        <div className="flex border-b border-whisper-border">
          <button
            onClick={() => setActiveTab("topic")}
            className={cn(
              "px-6 py-3 font-bold text-sm -mb-[1px] transition-colors border-b-2 outline-none",
              activeTab === "topic" ? "text-primary border-primary" : "text-on-surface-variant border-transparent hover:text-primary"
            )}
          >
            Chủ đề
          </button>
          <button
            onClick={() => setActiveTab("difficulty")}
            className={cn(
              "px-6 py-3 font-bold text-sm -mb-[1px] transition-colors border-b-2 outline-none",
              activeTab === "difficulty" ? "text-primary border-primary" : "text-on-surface-variant border-transparent hover:text-primary"
            )}
          >
            Độ khó
          </button>
        </div>

        {/* TOPIC TAB VIEW */}
        {activeTab === "topic" && (
          <div className="flex flex-col gap-4">

            {/* Toolbar Filters */}
            <div className="flex flex-wrap gap-4 items-center justify-between bg-pure-surface p-4 rounded-xl border border-whisper-border shadow-sm">
              <div className="relative w-full md:w-80 shrink-0">
                <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant pointer-events-none">search</span>
                <input
                  type="text"
                  placeholder="Tìm chủ đề..."
                  value={topicSearch}
                  onChange={(e) => setTopicSearch(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 bg-canvas-white border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                />
              </div>

              <div className="flex flex-wrap items-center gap-6">
                <div className="flex items-center gap-3">
                  <span className="text-xs font-bold text-on-surface-variant">Khối lớp:</span>
                  <div className="flex gap-1">
                    {[
                      { key: "ALL", label: "Tất cả" },
                      { key: "10", label: "Lớp 10" },
                      { key: "11", label: "Lớp 11" },
                      { key: "12", label: "Lớp 12" }
                    ].map((g) => (
                      <button
                        key={g.key}
                        onClick={() => setTopicGrade(g.key)}
                        className={cn(
                          "px-3 py-1 rounded-full text-xs font-bold transition-all border",
                          topicGrade === g.key
                            ? "bg-primary-container text-on-primary-container border-transparent"
                            : "bg-canvas-white text-on-surface-variant border-outline-variant hover:bg-surface-container"
                        )}
                      >
                        {g.label}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-on-surface-variant whitespace-nowrap">Trạng thái:</span>
                  <CustomSelect
                    value={topicStatus}
                    onValueChange={setTopicStatus}
                    items={[
                      { value: "ALL", label: "Tất cả trạng thái" },
                      { value: "ACTIVE", label: "Đang sử dụng" },
                      { value: "INACTIVE", label: "Ngừng sử dụng" }
                    ]}
                    className="w-48 text-xs font-semibold"
                  />
                </div>
              </div>
            </div>

            {/* Tree Data Table */}
            <div className="bg-pure-surface rounded-xl border border-whisper-border shadow-sm overflow-hidden">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-container text-on-surface font-bold text-xs border-b border-whisper-border">
                    <th className="p-4 font-bold">Chủ đề</th>
                    <th className="p-4 font-bold w-36">Khối</th>
                    <th className="p-4 font-bold w-24">Thứ tự</th>
                    <th className="p-4 font-bold w-36">Trạng thái</th>
                    <th className="p-4 font-bold w-24 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="text-xs">
                  {loading ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-on-surface-variant font-medium">
                        <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mr-2"></div>
                        Đang tải danh sách chủ đề...
                      </td>
                    </tr>
                  ) : error ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-error font-semibold">
                        <div className="flex flex-col items-center gap-2">
                          <span>{error}</span>
                          <Button variant="outline" size="sm" onClick={loadData}>Thử lại</Button>
                        </div>
                      </td>
                    </tr>
                  ) : filteredTopicsList.length === 0 ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-on-surface-variant font-semibold">
                        Không tìm thấy chủ đề nào phù hợp.
                      </td>
                    </tr>
                  ) : (
                    filteredTopicsList.map((t) => {
                      if (!isTopicVisible(t)) return null;
                      const hasChildren = t.children && t.children.length > 0;
                      const isExpanded = expandedTopics.has(t.id);
                      const isAct = t.isActive !== undefined ? t.isActive : true;

                      return (
                        <tr
                          key={t.id}
                          className={cn(
                            "border-b border-whisper-border hover:bg-canvas-white/60 transition-colors group",
                            t.depth > 0 ? "bg-canvas-white/20" : "",
                            !isAct ? "opacity-60" : ""
                          )}
                        >
                          <td
                            className="p-4"
                            style={{ paddingLeft: `${t.depth > 0 ? (t.depth * 28 + 16) : 16}px` }}
                          >
                            <div className={cn("flex items-center gap-2", t.depth > 0 && "relative before:content-[''] before:absolute before:-left-5 before:top-1/2 before:-translate-y-1/2 before:w-3.5 before:border-t-2 before:border-outline-variant before:border-dotted")}>
                              {hasChildren ? (
                                <button
                                  type="button"
                                  onClick={() => toggleExpand(t.id)}
                                  className="text-on-surface-variant p-1 rounded hover:bg-surface-container transition-colors cursor-pointer outline-none"
                                >
                                  <span className={cn("material-symbols-outlined text-[20px] transition-transform inline-block", isExpanded ? "rotate-90" : "")}>
                                    chevron_right
                                  </span>
                                </button>
                              ) : (
                                t.depth > 0 && <span className="w-7 shrink-0" />
                              )}
                              <span className={cn("text-on-surface", t.depth === 0 ? "font-bold text-sm" : "font-medium")}>
                                {t.tagName}
                              </span>
                              {hasChildren && (
                                <span className="px-2 py-0.5 rounded-full bg-surface-container text-on-surface-variant text-[10px] font-bold ml-1 font-mono">
                                  {t.children.length} con
                                </span>
                              )}
                            </div>
                          </td>
                          <td className="p-4">
                            {t.depth === 0 ? (
                              <span className="px-2 py-0.5 rounded-md bg-secondary-container text-on-secondary-container font-bold text-[10px] uppercase">
                                Lớp {t.grade}
                              </span>
                            ) : (
                              <span className="text-on-surface-variant/50 font-medium font-mono">-</span>
                            )}
                          </td>
                          <td className="p-4 font-mono font-bold text-on-surface-variant">{t.displayOrder}</td>
                          <td className="p-4">
                            <div className="flex items-center gap-2">
                              <button
                                role="switch"
                                aria-checked={isAct}
                                onClick={() => handleToggleActive(t, "topic")}
                                className={cn(
                                  "relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out outline-none focus:ring-2 focus:ring-primary/20",
                                  isAct ? "bg-emerald-success" : "bg-outline-variant"
                                )}
                              >
                                <span
                                  className={cn(
                                    "pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out",
                                    isAct ? "translate-x-4" : "translate-x-0"
                                  )}
                                />
                              </button>
                              <span className={cn("text-[10px] font-bold uppercase tracking-wider", isAct ? "text-emerald-success" : "text-on-surface-variant/65")}>
                                {isAct ? "Hoạt động" : "Ngừng dùng"}
                              </span>
                            </div>
                          </td>
                          <td className="p-4 text-right">
                            <button
                              onClick={() => handleOpenEditTopic(t)}
                              className="p-1.5 rounded text-primary hover:bg-primary/5 transition-colors cursor-pointer"
                              title="Chỉnh sửa chủ đề"
                            >
                              <span className="material-symbols-outlined text-[18px]">edit</span>
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* DIFFICULTY TAB VIEW */}
        {activeTab === "difficulty" && (
          <div className="flex flex-col gap-4">

            {/* Toolbar Filters */}
            <div className="flex flex-wrap gap-4 items-center justify-between bg-pure-surface p-4 rounded-xl border border-whisper-border shadow-sm">
              <div className="relative w-full md:w-80 shrink-0">
                <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant pointer-events-none">search</span>
                <input
                  type="text"
                  placeholder="Tìm độ khó..."
                  value={diffSearch}
                  onChange={(e) => setDiffSearch(e.target.value)}
                  className="w-full pl-10 pr-4 py-2 bg-canvas-white border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                />
              </div>

              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-on-surface-variant whitespace-nowrap">Trạng thái:</span>
                <CustomSelect
                  value={diffStatus}
                  onValueChange={setDiffStatus}
                  items={[
                    { value: "ALL", label: "Tất cả trạng thái" },
                    { value: "ACTIVE", label: "Đang sử dụng" },
                    { value: "INACTIVE", label: "Ngừng sử dụng" }
                  ]}
                  className="w-48 text-xs font-semibold"
                />
              </div>
            </div>

            {/* Difficulties Data Table */}
            <div className="bg-pure-surface rounded-xl border border-whisper-border shadow-sm overflow-hidden">
              <table className="w-full text-left border-collapse">
                <thead>
                  <tr className="bg-surface-container text-on-surface font-bold text-xs border-b border-whisper-border">
                    <th className="p-4 font-bold">Độ khó</th>
                    <th className="p-4 font-bold w-48">Cấp độ hệ thống</th>
                    <th className="p-4 font-bold w-36">Thứ tự</th>
                    <th className="p-4 font-bold w-36">Trạng thái</th>
                    <th className="p-4 font-bold w-24 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="text-xs">
                  {loading ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-on-surface-variant font-medium">
                        <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mr-2"></div>
                        Đang tải danh sách độ khó...
                      </td>
                    </tr>
                  ) : error ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-error font-semibold">
                        <div className="flex flex-col items-center gap-2">
                          <span>{error}</span>
                          <Button variant="outline" size="sm" onClick={loadData}>Thử lại</Button>
                        </div>
                      </td>
                    </tr>
                  ) : filteredDifficultiesList.length === 0 ? (
                    <tr>
                      <td colSpan="5" className="p-8 text-center text-on-surface-variant font-semibold">
                        Không tìm thấy độ khó nào phù hợp.
                      </td>
                    </tr>
                  ) : (
                    filteredDifficultiesList.map((d) => {
                      const isAct = d.isActive !== undefined ? d.isActive : true;

                      return (
                        <tr
                          key={d.id}
                          className={cn(
                            "border-b border-whisper-border hover:bg-canvas-white/60 transition-colors group",
                            !isAct ? "opacity-60" : ""
                          )}
                        >
                          <td className="p-4 font-bold text-on-surface text-sm">
                            {d.difficultyName}
                          </td>
                          <td className="p-4">
                            <span className="px-2 py-0.5 rounded bg-surface-container text-on-surface-variant font-bold font-mono">
                              Cấp {d.levelValue}
                            </span>
                          </td>
                          <td className="p-4 font-mono font-bold text-on-surface-variant">{d.displayOrder}</td>
                          <td className="p-4">
                            <div className="flex items-center gap-2">
                              <button
                                role="switch"
                                aria-checked={isAct}
                                onClick={() => handleToggleActive(d, "difficulty")}
                                className={cn(
                                  "relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out outline-none focus:ring-2 focus:ring-primary/20",
                                  isAct ? "bg-emerald-success" : "bg-outline-variant"
                                )}
                              >
                                <span
                                  className={cn(
                                    "pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out",
                                    isAct ? "translate-x-4" : "translate-x-0"
                                  )}
                                />
                              </button>
                              <span className={cn("text-[10px] font-bold uppercase tracking-wider", isAct ? "text-emerald-success" : "text-on-surface-variant/65")}>
                                {isAct ? "Hoạt động" : "Ngừng dùng"}
                              </span>
                            </div>
                          </td>
                          <td className="p-4 text-right">
                            <button
                              onClick={() => handleOpenEditDiff(d)}
                              className="p-1.5 rounded text-primary hover:bg-primary/5 transition-colors cursor-pointer"
                              title="Chỉnh sửa độ khó"
                            >
                              <span className="material-symbols-outlined text-[18px]">edit</span>
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* DIALOG: CREATE / EDIT TOPIC */}
        <Dialog isOpen={isTopicDialogOpen} onClose={() => setIsTopicDialogOpen(false)}>
          <DialogHeader>
            <DialogTitle>
              {topicDialogMode === "create" ? "Tạo chủ đề mới" : "Chỉnh sửa chủ đề"}
            </DialogTitle>
            <DialogDescription>
              {topicDialogMode === "create"
                ? "Nhập các thông tin bắt buộc dưới đây để thêm chủ đề phân loại mới."
                : "Cập nhật các thông tin chỉnh sửa hiển thị cho chủ đề đã chọn."}
            </DialogDescription>
          </DialogHeader>
          <DialogContent>
            {formTopicValidation && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2 mb-4 animate-in fade-in duration-200">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{formTopicValidation}</span>
              </div>
            )}
            <form onSubmit={handleSaveTopic} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Khối lớp</label>
                  {topicDialogMode === "create" ? (
                    <CustomSelect
                      value={formTopicGrade}
                      onValueChange={setFormTopicGrade}
                      items={[
                        { value: "10", label: "Lớp 10" },
                        { value: "11", label: "Lớp 11" },
                        { value: "12", label: "Lớp 12" }
                      ]}
                      className="w-full text-xs font-semibold"
                    />
                  ) : (
                    <div>
                      <input
                        type="text"
                        value={`Lớp ${formTopicGrade}`}
                        disabled
                        className="w-full px-3 py-2 bg-surface-container border border-outline-variant/50 rounded-lg text-xs text-on-surface-variant cursor-not-allowed font-semibold"
                      />
                      <p className="text-[10px] text-on-surface-variant/70 mt-1 italic">Không thể thay đổi khối lớp sau khi tạo.</p>
                    </div>
                  )}
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Chủ đề cha</label>
                  {topicDialogMode === "create" ? (
                    <CustomSelect
                      value={formTopicParentId}
                      onValueChange={setFormTopicParentId}
                      items={[
                        { value: "", label: "Không có chủ đề cha" },
                        ...getTopicParentOptions().map(t => ({
                          value: (t.tagId || t.id).toString(),
                          label: t.tagName || t.name
                        }))
                      ]}
                      className="w-full text-xs font-semibold"
                    />
                  ) : (
                    <div>
                      <input
                        type="text"
                        value={
                          topics.find(t => (t.tagId || t.id) === currentTopic.parentTagId)?.tagName ||
                          topics.find(t => (t.tagId || t.id) === currentTopic.parentId)?.tagName ||
                          "Không có chủ đề cha"
                        }
                        disabled
                        className="w-full px-3 py-2 bg-surface-container border border-outline-variant/50 rounded-lg text-xs text-on-surface-variant cursor-not-allowed font-semibold"
                      />
                      <p className="text-[10px] text-on-surface-variant/70 mt-1 italic">Chủ đề cha là cố định sau khi tạo.</p>
                    </div>
                  )}
                </div>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Tên chủ đề <span className="text-error">*</span></label>
                <input
                  type="text"
                  value={formTopicName}
                  onChange={(e) => setFormTopicName(e.target.value)}
                  placeholder="Ví dụ: Khảo sát hàm số"
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  required
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Mô tả chủ đề</label>
                <textarea
                  value={formTopicDescription}
                  onChange={(e) => setFormTopicDescription(e.target.value)}
                  placeholder="Nhập mô tả tóm tắt nếu có..."
                  rows="3"
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Thứ tự hiển thị</label>
                <input
                  type="number"
                  min="1"
                  value={formTopicOrder}
                  onChange={(e) => setFormTopicOrder(e.target.value)}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-bold font-mono"
                  required
                />
              </div>

              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setIsTopicDialogOpen(false)}>
                  Hủy
                </Button>
                <Button type="submit">
                  {topicDialogMode === "create" ? "Tạo chủ đề" : "Lưu thay đổi"}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        {/* DIALOG: CREATE / EDIT DIFFICULTY */}
        <Dialog isOpen={isDiffDialogOpen} onClose={() => setIsDiffDialogOpen(false)}>
          <DialogHeader>
            <DialogTitle>
              {diffDialogMode === "create" ? "Tạo độ khó mới" : "Chỉnh sửa độ khó"}
            </DialogTitle>
            <DialogDescription>
              {diffDialogMode === "create"
                ? "Thêm độ khó hệ thống mới để hỗ trợ thuật toán gợi ý."
                : "Cập nhật tên hoặc thứ tự hiển thị của độ khó."}
            </DialogDescription>
          </DialogHeader>
          <DialogContent>
            {formDiffValidation && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2 mb-4 animate-in fade-in duration-200">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{formDiffValidation}</span>
              </div>
            )}
            <form onSubmit={handleSaveDiff} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Cấp độ hệ thống</label>
                  {diffDialogMode === "create" ? (
                    <CustomSelect
                      value={formDiffLevel}
                      onValueChange={setFormDiffLevel}
                      items={[
                        { value: "1", label: "Cấp 1 (Nhận biết / Dễ)" },
                        { value: "2", label: "Cấp 2 (Thông hiểu / Trung bình)" },
                        { value: "3", label: "Cấp 3 (Vận dụng / Khó)" },
                        { value: "4", label: "Cấp 4 (Vận dụng cao / Rất khó)" }
                      ]}
                      className="w-full text-xs font-semibold"
                    />
                  ) : (
                    <div>
                      <input
                        type="text"
                        value={`Cấp độ hệ thống ${formDiffLevel}`}
                        disabled
                        className="w-full px-3 py-2 bg-surface-container border border-outline-variant/50 rounded-lg text-xs text-on-surface-variant cursor-not-allowed font-semibold"
                      />
                      <p className="text-[10px] text-on-surface-variant/70 mt-1 italic leading-relaxed">
                        Cấp độ hệ thống cố định để đảm bảo tính ổn định của thuật toán gợi ý và sinh đề.
                      </p>
                    </div>
                  )}
                </div>
                <div>
                  <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Thứ tự hiển thị</label>
                  <input
                    type="number"
                    min="1"
                    value={formDiffOrder}
                    onChange={(e) => setFormDiffOrder(e.target.value)}
                    className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-bold font-mono"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Tên độ khó <span className="text-error">*</span></label>
                <input
                  type="text"
                  value={formDiffName}
                  onChange={(e) => setFormDiffName(e.target.value)}
                  placeholder="Ví dụ: Nhận biết"
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  required
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Mô tả độ khó</label>
                <textarea
                  value={formDiffDescription}
                  onChange={(e) => setFormDiffDescription(e.target.value)}
                  placeholder="Nhập mô tả phân loại của độ khó..."
                  rows="3"
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                />
              </div>

              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => setIsDiffDialogOpen(false)}>
                  Hủy
                </Button>
                <Button type="submit">
                  {diffDialogMode === "create" ? "Tạo độ khó" : "Lưu thay đổi"}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        {/* DIALOG: CONFIRM DISABLE TAG */}
        <Dialog isOpen={isConfirmDisableOpen} onClose={() => setIsConfirmDisableOpen(false)}>
          <DialogContent className="p-0">
            {disableTarget && (
              <div className="flex gap-4 items-start p-6">
                <div className="w-12 h-12 rounded-full bg-error/10 flex items-center justify-center shrink-0">
                  <span className="material-symbols-outlined text-error text-[28px]">warning</span>
                </div>
                <div className="flex-1">
                  <h3 className="text-lg font-bold text-on-surface mb-2">Ngừng sử dụng tag?</h3>
                  <p className="text-sm text-on-surface-variant leading-relaxed mb-6">
                    Bạn đang chuẩn bị ngừng sử dụng tag{" "}
                    <span className="font-bold text-on-surface">
                      &ldquo;
                      {disableTarget.type === "topic"
                        ? disableTarget.item.tagName || disableTarget.item.name
                        : disableTarget.item.difficultyName || disableTarget.item.name}
                      &rdquo;
                    </span>
                    . Các câu hỏi hiện tại đang gắn tag này vẫn được giữ nguyên, nhưng hệ thống sẽ không cho phép chọn tag này cho các câu hỏi mới.
                  </p>
                  <div className="flex justify-end gap-3">
                    <Button variant="outline" onClick={() => setIsConfirmDisableOpen(false)}>
                      Hủy thao tác
                    </Button>
                    <Button
                      className="bg-error hover:bg-deep-rose text-white"
                      onClick={() => {
                        setIsConfirmDisableOpen(false);
                        proceedToggleActive(disableTarget.item, disableTarget.type, false);
                      }}
                    >
                      Ngừng sử dụng
                    </Button>
                  </div>
                </div>
              </div>
            )}
          </DialogContent>
        </Dialog>

      </div>
    </ExpertLayout>
  );
}
