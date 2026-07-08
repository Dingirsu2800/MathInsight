import * as React from "react";
import { useNavigate } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import VersionHistoryDrawer from "./VersionHistoryDrawer";
import { cn } from "../../utils/cn";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { CustomSelect } from "../../components/ui/custom-select";
import { questionBankApi } from "../../services/questionBankApi";
import { mapQuestionListItemToViewModel, mapQuestionDetailToViewModel, flattenTopicTree } from "./questionMappers";
import { getQuestionTypeLabel, getQuestionPartTypeLabel } from "../../utils/questionLabels";
import LatexPreview from "../../components/expert/LatexPreview";

export default function QuestionBankListPage() {
  const navigate = useNavigate();

  // Dialog / Drawer states
  const [selectedQuestion, setSelectedQuestion] = React.useState(null);
  const [isPreviewOpen, setIsPreviewOpen] = React.useState(false);
  const [isHistoryOpen, setIsHistoryOpen] = React.useState(false);
  
  // Real detail states
  const [selectedQuestionDetails, setSelectedQuestionDetails] = React.useState(null);
  const [detailsLoading, setDetailsLoading] = React.useState(false);

  // Filter lists from API
  const [topics, setTopics] = React.useState([]);
  const [difficulties, setDifficulties] = React.useState([]);

  // Filter states
  const [searchTerm, setSearchTerm] = React.useState("");
  const [selectedGrade, setSelectedGrade] = React.useState("");
  const [selectedStatus, setSelectedStatus] = React.useState("");
  const [selectedType, setSelectedType] = React.useState("");
  const [selectedTopic, setSelectedTopic] = React.useState("");
  const [selectedDifficulty, setSelectedDifficulty] = React.useState("");

  // Pagination states
  const [pageIndex, setPageIndex] = React.useState(1);
  const [pageSize] = React.useState(10);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);

  // List states
  const [questions, setQuestions] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(null);
  const [usingMockData, setUsingMockData] = React.useState(false);

  // Static mock questions fallback database (matching backend enum mapping)
  const mockQuestionsFallback = [
    {
      id: 1042,
      content: "Tính đạo hàm của hàm số y = \\sin(2x) tại điểm x = \\frac{\\pi}{4}.",
      topic: "Giải tích 11",
      grade: "11",
      difficulty: "Khó",
      difficultyLevel: "hard",
      type: "MULTIPLE_CHOICE",
      status: "APPROVED",
      points: 10,
      answers: {
        options: [
          { content: "y'(\\frac{\\pi}{4}) = 0", isCorrect: true },
          { content: "y'(\\frac{\\pi}{4}) = 1", isCorrect: false },
          { content: "y'(\\frac{\\pi}{4}) = 2", isCorrect: false },
          { content: "y'(\\frac{\\pi}{4}) = -2", isCorrect: false }
        ],
        explanation: "Ta có y' = 2\\cos(2x). Thay x = \\pi/4 vào ta được y'(\\pi/4) = 2\\cos(\\pi/2) = 0."
      }
    },
    {
      id: 1088,
      content: "Cho hình chóp S.ABCD có đáy là hình vuông cạnh a, SA \\perp (ABCD) và SA = a\\sqrt{3}. Tính thể tích khối chóp S.ABCD.",
      topic: "Hình học không gian",
      grade: "11",
      difficulty: "Rất khó",
      difficultyLevel: "very_hard",
      type: "SINGLE_CHOICE",
      status: "REPORTED",
      points: 20,
      answers: {
        options: [
          { content: "V = \\frac{a^3 \\sqrt{3}}{3}", isCorrect: true },
          { content: "V = a^3 \\sqrt{3}", isCorrect: false },
          { content: "V = \\frac{a^3}{3}", isCorrect: false },
          { content: "V = \\frac{a^3 \\sqrt{2}}{3}", isCorrect: false }
        ],
        explanation: "Diện tích đáy S = a^2. Chiều cao h = SA = a\\sqrt{3}. Thể tích V = 1/3 * S * h = a^3\\sqrt{3}/3."
      }
    },
    {
      id: 1102,
      content: "Khảo sát sự biến thiên và vẽ đồ thị hàm số y = x^3 - 3x + 1.",
      topic: "Khảo sát hàm số",
      grade: "12",
      difficulty: "Trung bình",
      difficultyLevel: "medium",
      type: "COMPOSITE",
      status: "DEACTIVATED",
      points: 15,
      answers: {
        explanation: "1. Tập xác định D = R. 2. Sự biến thiên: y' = 3x^2 - 3. y' = 0 <=> x = +-1.",
        parts: [
          {
            type: "TRUE_FALSE",
            question: "Hàm số đồng biến trên các khoảng (-\\infty; -1) và (1; +\\infty).",
            options: [
              { content: "Đúng", isCorrect: true },
              { content: "Sai", isCorrect: false }
            ]
          },
          {
            type: "SHORT_ANSWER",
            question: "Điểm cực tiểu của đồ thị hàm số là điểm nào? (Nhập tọa độ dạng (x,y))",
            correctAnswer: "(1,-1)"
          }
        ]
      }
    }
  ];

  // Load tag libraries
  React.useEffect(() => {
    questionBankApi.getDifficulties()
      .then((res) => {
        setDifficulties(res.data || []);
      })
      .catch((err) => {
        console.error("Failed to load difficulties tags:", err);
      });
  }, []);

  // Fetch topic tags when selectedGrade changes
  React.useEffect(() => {
    questionBankApi.getTopicTags(selectedGrade)
      .then((res) => {
        setTopics(flattenTopicTree(res.data || []));
      })
      .catch((err) => {
        console.error("Failed to load topic tags:", err);
      });
  }, [selectedGrade]);

  // Fetch questions main logic
  const fetchQuestions = React.useCallback(() => {
    setLoading(true);
    setError(null);

    const queryParams = {
      pageIndex: pageIndex,
      pageSize: pageSize
    };

    if (selectedGrade) queryParams.grade = parseInt(selectedGrade);
    if (selectedStatus) queryParams.status = selectedStatus;
    if (selectedType) queryParams.questionType = selectedType;
    if (selectedTopic) queryParams.tagId = selectedTopic;
    if (selectedDifficulty) queryParams.difficultyId = selectedDifficulty;

    questionBankApi.getQuestions(queryParams)
      .then((res) => {
        const data = res.data;
        let items = [];
        let totalP = 1;
        let totalC = 0;

        if (Array.isArray(data)) {
          items = data;
          totalC = data.length;
        } else if (data) {
          if (data.items && Array.isArray(data.items)) {
            items = data.items;
            totalP = data.totalPages || 1;
            totalC = data.totalCount || data.items.length;
          } else if (data.data && Array.isArray(data.data)) {
            items = data.data;
            totalP = data.totalPages || 1;
            totalC = data.totalCount || data.data.length;
          }
        }

        setQuestions(items.map(mapQuestionListItemToViewModel));
        setTotalPages(totalP);
        setTotalCount(totalC);
        setUsingMockData(false);
        setLoading(false);
      })
      .catch((err) => {
        console.error("Failed to load questions from backend API:", err);
        const enableFallback = import.meta.env.VITE_ENABLE_MOCK_FALLBACK === "true";

        if (enableFallback) {
          setQuestions(mockQuestionsFallback);
          setTotalPages(1);
          setTotalCount(mockQuestionsFallback.length);
          setUsingMockData(true);
          setError("Đang hiển thị dữ liệu mẫu do không kết nối được backend API.");
        } else {
          setError(
            err.response?.data?.message ||
            err.response?.statusText ||
            err.message ||
            "Không thể kết nối tới máy chủ API QuestionBank."
          );
        }
        setLoading(false);
      });
  }, [pageIndex, pageSize, selectedGrade, selectedStatus, selectedType, selectedTopic, selectedDifficulty]);

  // Refetch on dependency changes
  React.useEffect(() => {
    fetchQuestions();
  }, [fetchQuestions]);

  // Reset all filters
  const handleResetFilters = () => {
    setSearchTerm("");
    setSelectedGrade("");
    setSelectedStatus("");
    setSelectedType("");
    setSelectedTopic("");
    setSelectedDifficulty("");
    setPageIndex(1);
  };

  // Client-side search filtering (search in current page)
  const filteredQuestions = React.useMemo(() => {
    return questions.filter((q) => {
      const matchesSearch =
        searchTerm.trim() === "" ||
        q.id.toString().includes(searchTerm) ||
        q.content.toLowerCase().includes(searchTerm.toLowerCase()) ||
        q.topic.toLowerCase().includes(searchTerm.toLowerCase());
      
      // If we are using local fallback mock data, we also apply the selectors client-side:
      if (usingMockData) {
        const matchesGrade = selectedGrade === "" || q.grade === selectedGrade;
        const matchesStatus = selectedStatus === "" || q.status.toLowerCase() === selectedStatus.toLowerCase();
        const matchesType = selectedType === "" || q.type === selectedType;
        const matchesDifficulty = selectedDifficulty === "" || q.difficultyId === selectedDifficulty;
        return matchesSearch && matchesGrade && matchesStatus && matchesType && matchesDifficulty;
      }

      return matchesSearch;
    });
  }, [questions, searchTerm, usingMockData, selectedGrade, selectedStatus, selectedType, selectedDifficulty]);

  // Handle detailed preview loading
  const handleOpenPreview = (q) => {
    setSelectedQuestion(q);
    setIsPreviewOpen(true);
    setDetailsLoading(true);
    setSelectedQuestionDetails(null);

    questionBankApi.getQuestionDetail(q.id)
      .then((res) => {
        setSelectedQuestionDetails(mapQuestionDetailToViewModel(res.data));
        setDetailsLoading(false);
      })
      .catch((err) => {
        console.error("Failed to load question details:", err);
        const enableFallback = import.meta.env.VITE_ENABLE_MOCK_FALLBACK === "true";
        if (enableFallback || usingMockData) {
          // fallback details from list item
          setSelectedQuestionDetails(mapQuestionDetailToViewModel({
            questionId: q.id,
            questionContent: q.content,
            questionType: q.type,
            grade: parseInt(q.grade),
            defaultPoint: q.points,
            status: q.status,
            difficultyName: q.difficulty,
            difficultyLevel: q.difficultyLevel,
            topics: q.topics,
            // construct answers from local list item mock database if exists
            answers: q.answers?.options?.map(o => ({
              answerContent: o.content,
              isCorrect: o.isCorrect
            })) || [
              { answerContent: "Đáp án A", isCorrect: true },
              { answerContent: "Đáp án B", isCorrect: false }
            ],
            parts: q.answers?.parts || []
          }));
        } else {
          alert("Lỗi tải chi tiết: " + (err.response?.data?.message || err.message));
          setIsPreviewOpen(false);
        }
        setDetailsLoading(false);
      });
  };

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        
        {/* Page Header */}
        <div className="flex justify-between items-center">
          <div>
            <h1 className="text-2xl lg:text-3xl font-bold text-on-background">Ngân hàng câu hỏi</h1>
            <p className="text-sm text-on-surface-variant mt-1">Quản lý, tìm kiếm và lọc dữ liệu câu hỏi môn Toán học.</p>
          </div>
          <Button onClick={() => navigate("/expert/questions/new")}>
            <span className="material-symbols-outlined text-[18px] mr-1.5">add</span>
            Tạo câu hỏi mới
          </Button>
        </div>

        {/* Error / Alert banner */}
        {error && (
          <div className={cn(
            "p-4 border rounded-xl flex items-center justify-between text-sm font-semibold shadow-sm",
            usingMockData 
              ? "bg-amber-warning/10 border-amber-warning/30 text-amber-warning" 
              : "bg-error/10 border-error/20 text-error"
          )}>
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined">{usingMockData ? "warning" : "error"}</span>
              <span>{error}</span>
            </div>
            {usingMockData && (
              <button 
                onClick={fetchQuestions}
                className="underline hover:no-underline cursor-pointer"
              >
                Thử lại
              </button>
            )}
          </div>
        )}

        {/* Filters & Search Layout */}
        <div className="grid grid-cols-12 gap-4 bg-pure-surface border border-whisper-border p-4 rounded-xl shadow-sm">
          {/* Search Box */}
          <div className="col-span-12 lg:col-span-4 bg-surface-container-lowest border border-whisper-border rounded-lg flex items-center shadow-inner focus-within:ring-2 focus-within:ring-primary focus-within:border-transparent transition-all">
            <span className="material-symbols-outlined text-on-surface-variant px-3 select-none">search</span>
            <input
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setPageIndex(1);
              }}
              className="w-full bg-transparent border-none focus:ring-0 text-[14px] text-on-surface placeholder-on-surface-variant outline-none py-2 pr-4"
              placeholder="Tìm trong trang hiện tại..."
              type="text"
            />
          </div>

          {/* Filters Select Dropdowns */}
          <div className="col-span-12 lg:col-span-8 flex flex-wrap gap-3 items-center">
            {/* Khối lớp */}
            <div className="w-[110px]">
              <CustomSelect
                value={selectedGrade || "ALL"}
                onValueChange={(val) => { setSelectedGrade(val === "ALL" ? "" : val); setPageIndex(1); }}
                placeholder="Khối lớp"
                items={[
                  { value: "ALL", label: "Tất cả khối" },
                  { value: "10", label: "Lớp 10" },
                  { value: "11", label: "Lớp 11" },
                  { value: "12", label: "Lớp 12" }
                ]}
              />
            </div>

            {/* Độ khó */}
            <div className="w-[125px]">
              <CustomSelect
                value={selectedDifficulty || "ALL"}
                onValueChange={(val) => { setSelectedDifficulty(val === "ALL" ? "" : val); setPageIndex(1); }}
                placeholder="Độ khó"
                items={[
                  { value: "ALL", label: "Tất cả độ khó" },
                  ...difficulties.map((d) => ({ value: d.difficultyId, label: d.difficultyName }))
                ]}
              />
            </div>

            {/* Trạng thái */}
            <div className="w-[135px]">
              <CustomSelect
                value={selectedStatus || "ALL"}
                onValueChange={(val) => { setSelectedStatus(val === "ALL" ? "" : val); setPageIndex(1); }}
                placeholder="Trạng thái"
                items={[
                  { value: "ALL", label: "Tất cả trạng thái" },
                  { value: "APPROVED", label: "Approved" },
                  { value: "REPORTED", label: "Reported" },
                  { value: "REJECTED", label: "Rejected" },
                  { value: "DEACTIVATED", label: "Deactivated" }
                ]}
              />
            </div>

            {/* Loại câu hỏi */}
            <div className="w-[155px]">
              <CustomSelect
                value={selectedType || "ALL"}
                onValueChange={(val) => { setSelectedType(val === "ALL" ? "" : val); setPageIndex(1); }}
                placeholder="Loại câu hỏi"
                items={[
                  { value: "ALL", label: "Tất cả loại" },
                  { value: "SINGLE_CHOICE", label: "Trắc nghiệm một đáp án" },
                  { value: "MULTIPLE_CHOICE", label: "Trắc nghiệm nhiều đáp án" },
                  { value: "TRUE_FALSE", label: "Đúng / Sai" },
                  { value: "SHORT_ANSWER", label: "Trả lời ngắn" },
                  { value: "COMPOSITE", label: "Câu hỏi nhiều mệnh đề" }
                ]}
              />
            </div>

            {/* Chủ đề (Hierarchical) */}
            <div className="w-[155px]">
              <CustomSelect
                value={selectedTopic || "ALL"}
                onValueChange={(val) => { setSelectedTopic(val === "ALL" ? "" : val); setPageIndex(1); }}
                placeholder="Chủ đề"
                items={[
                  { value: "ALL", label: "Tất cả chủ đề" },
                  ...topics.map((t) => ({ value: t.tagId, label: t.displayName }))
                ]}
              />
            </div>

            {/* Reset Filters Button */}
            <button
              onClick={handleResetFilters}
              className="ml-auto text-on-surface-variant hover:text-error text-xs font-bold flex items-center gap-1 transition-colors px-2.5 py-1.5 rounded-lg border border-whisper-border bg-pure-surface hover:bg-surface-container-low cursor-pointer"
            >
              <span className="material-symbols-outlined text-[16px]">filter_alt_off</span>
              Xóa bộ lọc
            </button>
          </div>
        </div>

        {/* Data Table */}
        <div className="w-full bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr className="text-on-surface-variant uppercase text-[11px] font-bold tracking-wider">
                  <th className="py-3 px-4 w-16 text-center">ID</th>
                  <th className="py-3 px-4 max-w-md">Nội dung câu hỏi (Preview)</th>
                  <th className="py-3 px-4 w-48">Thuộc tính</th>
                  <th className="py-3 px-4 w-40">Loại</th>
                  <th className="py-3 px-4 w-36">Trạng thái</th>
                  <th className="py-3 px-4 w-20 text-center">Điểm</th>
                  <th className="py-3 px-4 w-32 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border bg-pure-surface text-[14px]">
                {loading ? (
                  <tr>
                    <td colSpan={7} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải danh sách câu hỏi...</span>
                      </div>
                    </td>
                  </tr>
                ) : filteredQuestions.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-12 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">search_off</span>
                        Không tìm thấy câu hỏi phù hợp.
                      </div>
                    </td>
                  </tr>
                ) : (
                  filteredQuestions.map((q) => (
                    <tr key={q.id} className="hover:bg-surface-bright transition-all group duration-150">
                      <td className="py-4 px-4 font-mono font-bold text-center text-on-surface-variant">
                        #{q.id}
                      </td>
                      <td className="py-4 px-4 max-w-md">
                        <div className="truncate font-semibold text-on-surface" title={q.content}>
                          {q.content}
                        </div>
                      </td>
                      <td className="py-4 px-4">
                        <div className="flex flex-col gap-1.5">
                          <span className="font-bold text-[13px] text-on-surface truncate max-w-[170px]" title={q.topic}>
                            {q.topic}
                          </span>
                          <div className="flex gap-2">
                            <span className="text-[10px] uppercase font-black tracking-wider text-on-surface-variant bg-surface px-2 py-0.5 rounded border border-whisper-border">
                              Lớp {q.grade}
                            </span>
                            <span className={cn(
                              "text-[10px] uppercase font-black tracking-wider px-2 py-0.5 rounded border",
                              {
                                "text-emerald-success bg-emerald-success/10 border-emerald-success/20": q.difficultyLevel === "easy",
                                "text-primary bg-primary/10 border-primary/20": q.difficultyLevel === "medium",
                                "text-amber-warning bg-amber-warning/10 border-amber-warning/20": q.difficultyLevel === "hard",
                                "text-deep-rose bg-deep-rose/10 border-deep-rose/20": q.difficultyLevel === "very_hard"
                              }
                            )}>
                              {q.difficulty}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td className="py-4 px-4">
                        <span className="font-semibold text-xs text-on-secondary-fixed bg-surface-container-high px-2.5 py-1 rounded-md border border-whisper-border">
                          {getQuestionTypeLabel(q.type)}
                        </span>
                      </td>
                      <td className="py-4 px-4">
                        <Badge variant={q.status}>{q.status}</Badge>
                      </td>
                      <td className="py-4 px-4 font-bold text-center text-on-surface">
                        {q.points}
                      </td>
                      <td className="py-4 px-4 text-right">
                        <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-opacity duration-200">
                          <button
                            onClick={() => handleOpenPreview(q)}
                            className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                            title="Xem chi tiết"
                          >
                            <span className="material-symbols-outlined text-[18px]">visibility</span>
                          </button>
                          <button
                            onClick={() => navigate(`/expert/questions/${q.id}/edit`)}
                            className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                            title="Chỉnh sửa"
                          >
                            <span className="material-symbols-outlined text-[18px]">edit</span>
                          </button>
                          <button
                            onClick={() => {
                              setSelectedQuestion(q);
                              setIsHistoryOpen(true);
                            }}
                            className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                            title="Lịch sử"
                          >
                            <span className="material-symbols-outlined text-[18px]">history</span>
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          <div className="bg-surface-container-low border-t border-whisper-border p-4 flex items-center justify-between">
            <span className="text-xs text-on-surface-variant font-bold">
              Hiển thị {filteredQuestions.length} trong số {totalCount} câu hỏi
            </span>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex(p => Math.max(1, p - 1))}
                disabled={pageIndex <= 1 || loading}
              >
                Trước
              </Button>
              <div className="flex items-center justify-center bg-pure-surface border border-whisper-border rounded px-3 text-xs font-bold select-none text-on-surface">
                {pageIndex} / {totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex(p => Math.min(totalPages, p + 1))}
                disabled={pageIndex >= totalPages || loading}
              >
                Tiếp
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* DETAILED PREVIEW DIALOG */}
      <Dialog isOpen={isPreviewOpen} onClose={() => setIsPreviewOpen(false)} variant="modal">
        {selectedQuestion && (
          <>
            <DialogHeader>
              <div className="flex items-center gap-2 mb-1">
                <Badge variant="primary">Q-{selectedQuestion.id}</Badge>
                <Badge variant={selectedQuestion.status}>{selectedQuestion.status}</Badge>
              </div>
              <DialogTitle>Chi tiết Câu hỏi</DialogTitle>
              <DialogDescription>Xem thông tin chi tiết cấu hình câu hỏi môn Toán học.</DialogDescription>
            </DialogHeader>

            <DialogContent className="space-y-4">
              {detailsLoading ? (
                <div className="flex flex-col items-center justify-center py-10 gap-2">
                  <div className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin"></div>
                  <span className="text-xs text-on-surface-variant">Đang tải chi tiết câu hỏi...</span>
                </div>
              ) : selectedQuestionDetails ? (
                <>
                  {/* Question Content */}
                  <div>
                    <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Nội dung câu hỏi:</h4>
                    <div className="p-4 bg-surface-container rounded-xl text-[14px] leading-relaxed border border-whisper-border break-words">
                      <LatexPreview content={selectedQuestionDetails.content} />
                    </div>
                  </div>

                  {/* Image */}
                  {selectedQuestionDetails.pictureUrl && (
                    <div className="rounded-xl overflow-hidden border border-whisper-border">
                      <img src={selectedQuestionDetails.pictureUrl} alt="Hình minh họa" className="max-h-48 mx-auto object-contain" />
                    </div>
                  )}

                  {/* Attributes */}
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Khối lớp:</h4>
                      <p className="font-semibold text-on-surface text-[14px]">
                        Lớp {selectedQuestionDetails.grade} ({selectedQuestionDetails.difficultyName || selectedQuestion.difficulty})
                      </p>
                    </div>
                    <div>
                      <h4 className="text-xs font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Điểm mặc định:</h4>
                      <p className="font-bold text-primary text-[14px]">
                        {selectedQuestionDetails.points} điểm
                      </p>
                    </div>
                  </div>

                  {/* Answers */}
                  <div>
                    <h4 className="text-xs font-bold text-on-surface-variant mb-2 uppercase tracking-wider">Đáp án & Lời giải:</h4>
                    
                    {/* Single / Multiple Choice / True False options */}
                    {(selectedQuestion.type === "SINGLE_CHOICE" || selectedQuestion.type === "MULTIPLE_CHOICE" || selectedQuestion.type === "TRUE_FALSE") && (
                      <div className="space-y-2 mb-4">
                        {(selectedQuestionDetails.answers || []).map((opt, idx) => (
                          <div
                            key={idx}
                            className={cn(
                              "p-3 rounded-lg border flex items-center justify-between text-[13px] transition-all",
                              opt.isCorrect
                                ? "bg-emerald-success/10 border-emerald-success/30 text-emerald-success font-bold"
                                : "bg-pure-surface border-whisper-border text-on-surface-variant"
                            )}
                          >
                            <div className="flex items-center gap-3">
                              <div className={cn(
                                "w-5 h-5 rounded-full flex items-center justify-center border font-bold text-[10px]",
                                opt.isCorrect ? "bg-emerald-success text-on-primary border-transparent" : "border-outline-variant text-on-surface-variant"
                              )}>
                                {String.fromCharCode(65 + idx)}
                              </div>
                              <span className="font-mono">{opt.answerContent || opt.content}</span>
                            </div>
                            {selectedQuestion.type === "TRUE_FALSE" && (
                              <Badge variant={opt.isCorrect ? "approved" : "secondary"}>
                                {opt.isCorrect ? "Đúng" : "Sai"}
                              </Badge>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Short Answer */}
                    {selectedQuestion.type === "SHORT_ANSWER" && (
                      <div className="p-3 bg-surface-container rounded-lg border border-whisper-border font-mono text-[13px] text-primary font-bold mb-4">
                        Đáp án đúng: {selectedQuestionDetails.answers?.find(a => a.isCorrect)?.answerContent || selectedQuestionDetails.answers?.[0]?.answerContent || "Chưa thiết lập"}
                      </div>
                    )}

                    {/* Composite nested parts */}
                    {selectedQuestion.type === "COMPOSITE" && (
                      <div className="space-y-3 mb-4">
                        {(selectedQuestionDetails.parts || []).map((part, idx) => (
                          <div key={idx} className="border border-whisper-border rounded-xl p-3 bg-canvas-white">
                            <div className="flex items-center justify-between mb-1.5">
                              <span className="text-[10px] font-black uppercase text-primary">Phần {part.partOrder || (idx + 1)}: {getQuestionPartTypeLabel(part.partType)}</span>
                              <Badge variant="outline" className="scale-90">{part.defaultPoint} đ</Badge>
                            </div>
                            <div className="mb-2">
                              <LatexPreview content={part.partContent} />
                            </div>
                            
                            {part.partType === "TRUE_FALSE" && (
                              <div className="flex gap-2">
                                <Badge variant={part.correctBoolean ? "approved" : "secondary"}>
                                  Đúng: {part.correctBoolean ? "Có" : "Không"}
                                </Badge>
                              </div>
                            )}

                            {part.partType === "SHORT_ANSWER" && (
                              <p className="text-[12px] text-emerald-success font-mono font-bold">
                                Đáp án đúng: <span className="underline">{part.correctText}</span>
                              </p>
                            )}

                            {part.partType === "NUMERIC_ANSWER" && (
                              <div className="space-y-0.5">
                                <p className="text-[12px] text-emerald-success font-mono font-bold">
                                  Số đúng: <span className="underline">{part.correctNumeric}</span>
                                </p>
                                {part.numericTolerance > 0 && (
                                  <p className="text-[10px] text-on-surface-variant font-mono">
                                    Sai số cho phép: ±{part.numericTolerance}
                                  </p>
                                )}
                              </div>
                            )}

                            {part.explanation && (
                              <p className="text-[11px] text-on-surface-variant italic mt-1.5 border-t border-dashed border-whisper-border pt-1">
                                Lời giải phụ: {part.explanation}
                              </p>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Solution Explanation */}
                    {selectedQuestionDetails.solutionContent && (
                      <div className="mt-3">
                        <h5 className="text-[12px] font-bold text-on-surface-variant mb-1 uppercase tracking-wider">Lời giải chi tiết:</h5>
                        <div className="p-3 bg-surface-container-high/60 rounded-lg text-[13px] text-on-surface font-medium leading-relaxed border border-whisper-border/30">
                          <LatexPreview content={selectedQuestionDetails.solutionContent} />
                        </div>
                      </div>
                    )}
                  </div>
                </>
              ) : (
                <p className="text-sm text-error text-center py-4">Không thể tải thông tin chi tiết.</p>
              )}
            </DialogContent>

            <DialogFooter>
              <Button variant="outline" onClick={() => setIsPreviewOpen(false)} className="normal-case h-9 text-xs">
                Đóng
              </Button>
              <Button 
                onClick={() => {
                  setIsPreviewOpen(false);
                  navigate(`/expert/questions/${selectedQuestion.id}/edit`);
                }} 
                disabled={detailsLoading}
                className="normal-case h-9 text-xs"
              >
                Chỉnh sửa
              </Button>
            </DialogFooter>
          </>
        )}
      </Dialog>
      
      {/* VERSION HISTORY DRAWER */}
      {selectedQuestion && (
        <VersionHistoryDrawer
          isOpen={isHistoryOpen}
          onClose={() => setIsHistoryOpen(false)}
          questionId={selectedQuestion.id}
          questionTitle={selectedQuestion.content}
        />
      )}
    </ExpertLayout>
  );
}
