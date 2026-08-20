import React, { useState, useEffect, useRef } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import StudentLayout from "../../components/layout/StudentLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import StartTestDialog from "../../components/student/StartTestDialog";
import AdaptiveBlueprintExamDialog from "../../components/student/AdaptiveBlueprintExamDialog";
import PracticeSetupPanel from "../../components/student/PracticeSetupPanel";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { useAdaptiveExamFlow } from "../../hooks/useAdaptiveExamFlow";
import { cn } from "../../utils/cn";

export default function SharedBlueprintExamDiscoveryPage() {
  const location = useLocation();
  const navigate = useNavigate();

  // Mode derived from route path: '/student/test/topics' -> practice, else -> exam
  const mode = location.pathname.endsWith('/topics') ? 'practice' : 'exam';

  // TestCode Resolution State
  const [testCodeInput, setTestCodeInput] = useState("");
  const [resolvingCode, setResolvingCode] = useState(false);
  const [resolveError, setResolveError] = useState("");

  // Exam Generation Type Filter State ("Fixed" | "Random")
  const [generationType, setGenerationType] = useState("Fixed");

  // Shared Exams List State
  const [exams, setExams] = useState([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState("");

  // Pagination State
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(12);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  // Featured Adaptive Blueprint State
  const [featuredBlueprint, setFeaturedBlueprint] = useState(null);
  const [loadingFeatured, setLoadingFeatured] = useState(true);
  const [featuredError, setFeaturedError] = useState("");

  // Shared Adaptive Exam Flow
  const adaptiveFlow = useAdaptiveExamFlow();
  const {
    generating,
    starting,
    isBusy: isAdaptiveBusy,
    generatedTestId,
    actionError: adaptiveActionError,
    resumeSessionId,
    handleCreateAndStart,
  } = adaptiveFlow;

  // Dialog State
  const [selectedTest, setSelectedTest] = useState(null);
  const [isStartDialogOpen, setIsStartDialogOpen] = useState(false);
  const [isAdaptiveDialogOpen, setIsAdaptiveDialogOpen] = useState(false);
  const [isTestCodeDialogOpen, setIsTestCodeDialogOpen] = useState(false);

  const resolveInFlightRef = useRef(false);
  const activeRequestIdRef = useRef(0);

  const fetchFeaturedBlueprint = async () => {
    setLoadingFeatured(true);
    setFeaturedError("");
    try {
      const res = await testGeneratorApi.getBlueprintExamOptions({
        pageIndex: 1,
        pageSize: 1,
      });
      const data = res.data || {};
      const items = Array.isArray(data) ? data : (data.items || []);
      if (items.length > 0) {
        setFeaturedBlueprint(items[0]);
      } else {
        setFeaturedBlueprint(null);
      }
    } catch (err) {
      setFeaturedError(getTestGenErrorMessage(err, "Không thể tải cấu trúc đề thi đề xuất."));
    } finally {
      setLoadingFeatured(false);
    }
  };

  const fetchExams = async (targetGenerationType, targetPageIndex) => {
    const currentRequestId = ++activeRequestIdRef.current;
    setLoading(true);
    setListError("");
    try {
      const response = await testGeneratorApi.getSharedBlueprintExams({
        pageIndex: targetPageIndex,
        pageSize,
        generationType: targetGenerationType,
      });

      // Ignore stale responses from earlier tab/page requests
      if (currentRequestId !== activeRequestIdRef.current) return;

      const data = response.data || {};
      const rawItems = data.items || [];
      // Client safety guard against mismatched items
      const filteredItems = rawItems.filter(
        (item) => !item.generationType || item.generationType.toLowerCase() === targetGenerationType.toLowerCase()
      );

      setExams(filteredItems);
      setTotalCount(data.totalCount || 0);
      const calculatedPages = Math.ceil((data.totalCount || 0) / pageSize) || 1;
      setTotalPages(data.totalPages || calculatedPages);
    } catch (err) {
      if (currentRequestId !== activeRequestIdRef.current) return;
      setListError(getTestGenErrorMessage(err, "Không thể tải danh sách bài thi. Vui lòng thử lại sau."));
    } finally {
      if (currentRequestId === activeRequestIdRef.current) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    if (mode === 'exam') {
      fetchFeaturedBlueprint();
    }
  }, [mode]);

  useEffect(() => {
    if (mode === 'exam') {
      fetchExams(generationType, pageIndex);
    }
  }, [mode, generationType, pageIndex, pageSize]);

  const handleGenerationTypeChange = (newType) => {
    if (newType === generationType) return;
    setGenerationType(newType);
    setPageIndex(1);
    setExams([]);
  };

  const handleResolveCodeSubmit = async (e) => {
    if (e) e.preventDefault();
    const rawInput = testCodeInput.trim();
    if (!rawInput) {
      setResolveError("Vui lòng nhập mã đề thi.");
      return;
    }

    if (resolveInFlightRef.current) return;
    resolveInFlightRef.current = true;
    setResolvingCode(true);
    setResolveError("");

    try {
      const res = await testGeneratorApi.resolveTestCode(rawInput);
      const resolvedTest = res.data;
      if (resolvedTest && resolvedTest.testId) {
        setIsTestCodeDialogOpen(false);
        setSelectedTest(resolvedTest);
        setIsStartDialogOpen(true);
      } else {
        throw new Error("Không thể xác thực mã đề thi.");
      }
    } catch (err) {
      setResolveError(getTestGenErrorMessage(err, "Mã đề không khả dụng. Vui lòng kiểm tra lại."));
    } finally {
      resolveInFlightRef.current = false;
      setResolvingCode(false);
    }
  };

  const handleOpenStartDialog = (testItem) => {
    setSelectedTest(testItem);
    setIsStartDialogOpen(true);
  };

  return (
    <StudentLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">
        {/* Page Header */}
        <DashboardPageHeader
          title={mode === 'practice' ? 'Luyện tập theo chủ đề' : 'Đề thi theo cấu trúc'}
          subtitle={
            mode === 'practice'
              ? 'Chọn chủ đề bài học để tạo bài luyện tập 10 câu hỏi không giới hạn thời gian.'
              : 'Đề thi gồm các câu hỏi được tập hợp theo ma trận có sẵn'
          }
        />

        {/* ── Mode Tab Switcher ── */}
        <div role="tablist" aria-label="Chế độ thi và luyện tập" className="flex items-center gap-1 p-1 bg-surface-container-low border border-whisper-border rounded-xl w-fit">
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'exam'}
            onClick={() => navigate('/student/test')}
            className={`flex items-center gap-1.5 px-4 py-2.5 rounded-lg text-sm font-bold transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary min-h-[44px] ${mode === 'exam'
              ? 'bg-pure-surface text-primary shadow-sm border border-whisper-border'
              : 'text-on-surface-variant hover:text-on-surface'
              }`}
          >
            <span className="material-symbols-outlined text-[18px]">quiz</span>
            Đề thi
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'practice'}
            onClick={() => navigate('/student/test/topics')}
            className={`flex items-center gap-1.5 px-4 py-2.5 rounded-lg text-sm font-bold transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary min-h-[44px] ${mode === 'practice'
              ? 'bg-pure-surface text-primary shadow-sm border border-whisper-border'
              : 'text-on-surface-variant hover:text-on-surface'
              }`}
          >
            <span className="material-symbols-outlined text-[18px]">fitness_center</span>
            Luyện theo chủ đề
          </button>
        </div>

        {/* ── Practice Mode ── */}
        {mode === 'practice' && <PracticeSetupPanel />}

        {/* ── Exam Mode content below ── */}
        {mode === 'exam' && (<>

          {/* ── Featured Recommendation Panel: Đề dành cho em ── */}
          {loadingFeatured ? (
            <div data-testid="featured-blueprint-skeleton" className="bg-gradient-to-br from-primary/5 via-surface-container-low to-pure-surface border border-primary/20 rounded-2xl p-6 shadow-sm animate-pulse flex flex-col lg:flex-row lg:items-center justify-between gap-5">
              <div className="space-y-3 flex-1">
                <div className="h-5 bg-surface-container-high rounded-full w-36"></div>
                <div className="h-6 bg-surface-container-high rounded-md w-2/3"></div>
                <div className="h-4 bg-surface-container rounded-md w-1/2"></div>
              </div>
              <div className="flex gap-3 shrink-0">
                <div className="h-11 bg-surface-container-high rounded-xl w-32"></div>
                <div className="h-11 bg-surface-container-high rounded-xl w-36"></div>
              </div>
            </div>
          ) : featuredError ? (
            <div className="bg-surface-container-low border border-whisper-border rounded-2xl p-5 shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs">
              <div className="flex items-center gap-2.5 text-on-surface-variant">
                <span className="material-symbols-outlined text-outline-variant text-[20px]">info</span>
                <span>{featuredError}</span>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={fetchFeaturedBlueprint}
                className="h-8 px-3 font-bold shrink-0 self-start sm:self-auto"
              >
                Thử lại
              </Button>
            </div>
          ) : !featuredBlueprint ? (
            <div className="bg-surface-container-low border border-whisper-border rounded-2xl p-6 text-center text-on-surface-variant flex flex-col items-center justify-center gap-2">
              <span className="material-symbols-outlined text-[36px] text-outline-variant">assignment_late</span>
              <p className="text-sm font-bold text-on-surface">Chưa có cấu trúc đề thi nào phù hợp với khối lớp của bạn.</p>
              <p className="text-xs">Bạn có thể chọn đề thi sẵn có trong kho đề bên dưới hoặc liên hệ giáo viên.</p>
            </div>
          ) : (
            <div data-testid="featured-recommendation-panel" className="relative overflow-hidden bg-gradient-to-br from-primary/10 via-primary/5 to-surface-container-low border border-primary/25 rounded-2xl p-6 shadow-sm flex flex-col gap-4">
              <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-5">
                <div className="space-y-2.5 min-w-0 flex-1 select-text">
                  {/* Badge */}
                  <div className="flex items-center gap-2">
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-primary/15 text-primary border border-primary/25 text-xs font-extrabold tracking-wide">
                      <span className="material-symbols-outlined text-[16px]">auto_awesome</span>
                      Đề dành cho em
                    </span>
                    <span className="bg-surface-container-high/80 text-on-surface-variant text-[11px] font-bold px-2 py-0.5 rounded-md border border-whisper-border">
                      Khối {featuredBlueprint.grade}
                    </span>
                  </div>

                  {/* Blueprint Name */}
                  <h2 className="text-lg md:text-xl font-black text-on-surface tracking-tight truncate">
                    {featuredBlueprint.blueprintName}
                  </h2>

                  {/* Metadata Badges / Info */}
                  <div className="flex items-center gap-2.5 sm:gap-3.5 text-xs text-on-surface-variant font-medium flex-wrap">
                    <span className="flex items-center gap-1">
                      <span className="material-symbols-outlined text-[16px] text-primary">view_list</span>
                      {featuredBlueprint.sectionCount} phần
                    </span>
                    <span className="text-whisper-border">·</span>
                    <span className="flex items-center gap-1 font-mono">
                      <span className="material-symbols-outlined text-[16px] text-primary">format_list_numbered</span>
                      {featuredBlueprint.totalQuestions} câu
                    </span>
                    <span className="text-whisper-border">·</span>
                    <span className="flex items-center gap-1">
                      <span className="material-symbols-outlined text-[16px] text-primary">schedule</span>
                      {featuredBlueprint.durationMinutes === 0 ? "Không giới hạn" : `${featuredBlueprint.durationMinutes} phút`}
                    </span>
                    <span className="text-whisper-border">·</span>
                    <span className="flex items-center gap-1 font-mono font-bold text-primary">
                      <span className="material-symbols-outlined text-[16px]">grade</span>
                      {featuredBlueprint.totalScore} điểm
                    </span>
                  </div>

                  {/* Supporting Copy */}
                  <p className="text-xs text-on-surface-variant flex items-center gap-1.5 pt-0.5">
                    <span className="material-symbols-outlined text-primary text-[16px] shrink-0">psychology</span>
                    <span>Độ khó câu hỏi được điều chỉnh dựa trên kết quả làm bài gần đây của em.</span>
                  </p>
                </div>

                {/* Actions */}
                <div className="flex flex-col sm:flex-row lg:flex-col xl:flex-row items-stretch sm:items-center gap-3 shrink-0">
                  <Button
                    type="button"
                    variant="outline"
                    disabled={isAdaptiveBusy}
                    onClick={() => setIsAdaptiveDialogOpen(true)}
                    className="min-h-[44px] px-4 font-bold border-primary/30 hover:bg-primary/5 text-on-surface"
                  >
                    Chọn cấu trúc khác
                  </Button>

                  <Button
                    type="button"
                    variant="primary"
                    disabled={isAdaptiveBusy}
                    onClick={() => handleCreateAndStart(featuredBlueprint.blueprintId)}
                    className="min-h-[44px] px-6 font-bold shadow-md flex items-center justify-center gap-2"
                  >
                    {generating ? (
                      <div className="flex items-center gap-2">
                        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tạo đề...</span>
                      </div>
                    ) : starting ? (
                      <div className="flex items-center gap-2">
                        <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang bắt đầu...</span>
                      </div>
                    ) : resumeSessionId ? (
                      <div className="flex items-center gap-1.5">
                        <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                        <span>Tiếp tục bài đang làm</span>
                      </div>
                    ) : generatedTestId ? (
                      <div className="flex items-center gap-1.5">
                        <span className="material-symbols-outlined text-[18px]">refresh</span>
                        <span>Thử bắt đầu lại</span>
                      </div>
                    ) : (
                      <div className="flex items-center gap-1.5">
                        <span className="material-symbols-outlined text-[18px]">auto_awesome</span>
                        <span>Tạo đề ngay</span>
                      </div>
                    )}
                  </Button>
                </div>
              </div>

              {/* In-Panel Resume Alert Banner */}
              {resumeSessionId && (
                <div className="p-3 bg-amber-500/10 border border-amber-500/20 rounded-xl text-on-surface flex items-start gap-2.5 text-xs">
                  <span className="material-symbols-outlined text-amber-700 text-[18px] shrink-0 mt-0.5">pending_actions</span>
                  <p className="text-on-surface-variant leading-relaxed">
                    Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này. Hãy chọn "Tiếp tục bài đang làm" để tiếp tục.
                  </p>
                </div>
              )}

              {/* In-Panel Error Alert Banner */}
              {adaptiveActionError && !resumeSessionId && (
                <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2">
                  <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                  <span className="flex-1">{adaptiveActionError}</span>
                </div>
              )}
            </div>
          )}

          {/* ── Shared Catalog Header: Segmented Tabs & Beside TestCode Button ── */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-whisper-border pb-4">
            {/* Catalog Tabs */}
            <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3">
              <span className="text-xs font-bold text-on-surface-variant shrink-0">Kho đề:</span>
              <div role="tablist" aria-label="Kho đề thi" className="flex items-center gap-1 p-1 bg-surface-container-low border border-whisper-border rounded-xl w-fit">
                <button
                  type="button"
                  role="tab"
                  aria-selected={generationType === 'Fixed'}
                  onClick={() => handleGenerationTypeChange('Fixed')}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary min-h-[38px] ${
                    generationType === 'Fixed'
                      ? 'bg-pure-surface text-primary shadow-sm border border-whisper-border'
                      : 'text-on-surface-variant hover:text-on-surface'
                  }`}
                >
                  Đề cố định
                </button>
                <button
                  type="button"
                  role="tab"
                  aria-selected={generationType === 'Random'}
                  onClick={() => handleGenerationTypeChange('Random')}
                  className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary min-h-[38px] ${
                    generationType === 'Random'
                      ? 'bg-pure-surface text-primary shadow-sm border border-whisper-border'
                      : 'text-on-surface-variant hover:text-on-surface'
                  }`}
                >
                  Đề theo cấu trúc
                </button>
              </div>

              {totalCount > 0 && (
                <span className="text-xs text-on-surface-variant font-bold font-mono">
                  ({totalCount} bài thi)
                </span>
              )}
            </div>

            {/* Beside Catalog: TestCode Modal Trigger Button */}
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setTestCodeInput("");
                setResolveError("");
                setIsTestCodeDialogOpen(true);
              }}
              className="h-10 min-h-[44px] px-4 font-bold flex items-center justify-center gap-2 shrink-0 self-start sm:self-auto"
            >
              <span className="material-symbols-outlined text-[18px]">vpn_key</span>
              <span>Nhập mã đề</span>
            </Button>
          </div>

          {/* Error Banner */}
          {listError && (
            <div role="alert" className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center justify-between gap-3 select-text">
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-[20px] shrink-0">error</span>
                <span>{listError}</span>
              </div>
              <Button variant="outline" size="sm" onClick={() => fetchExams(generationType, pageIndex)} className="h-8 text-xs font-bold">Thử lại</Button>
            </div>
          )}

          {/* Grid List Cards */}
          {loading ? (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
              {Array.from({ length: 6 }).map((_, idx) => (
                <div key={idx} className="bg-pure-surface border border-whisper-border rounded-xl p-5 h-48 animate-pulse flex flex-col justify-between">
                  <div className="space-y-3">
                    <div className="h-5 bg-surface-container-high rounded-md w-3/4"></div>
                    <div className="h-3 bg-surface-container rounded-md w-1/2"></div>
                  </div>
                  <div className="h-9 bg-surface-container-high rounded-lg w-full"></div>
                </div>
              ))}
            </div>
          ) : listError ? null : exams.length === 0 ? (
            <div className="bg-pure-surface border border-whisper-border rounded-xl p-12 text-center text-on-surface-variant flex flex-col items-center justify-center gap-3">
              <span className="material-symbols-outlined text-[48px] text-outline-variant">assignment_late</span>
              <p className="text-sm font-bold text-on-surface">
                {generationType === 'Fixed'
                  ? 'Chưa có đề cố định phù hợp với khối lớp của bạn.'
                  : 'Chưa có đề theo cấu trúc phù hợp với khối lớp của bạn.'}
              </p>
              <p className="text-xs">Bạn có thể dùng mã đề thi do giáo viên cung cấp ở ô tìm kiếm trên.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
              {exams.map((exam) => (
                <div
                  key={exam.testId}
                  className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm hover:border-primary/40 hover:shadow-md transition-all flex flex-col justify-between gap-4 group"
                >
                  <div className="space-y-2 select-text">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-bold text-sm text-on-surface group-hover:text-primary transition-colors line-clamp-2">
                        {exam.testName}
                      </h3>
                      <span className="bg-primary/10 text-primary border border-primary/20 text-[10px] font-extrabold px-2 py-0.5 rounded shrink-0">
                        Khối {exam.grade}
                      </span>
                    </div>

                    <div className="flex flex-wrap items-center justify-between gap-1 text-[11px]">
                      {exam.testCode && (
                        <span className="text-on-surface-variant font-mono">
                          Mã đề: <strong className="text-primary">{exam.testCode}</strong>
                        </span>
                      )}
                      {exam.createdTime && (
                        <span className="text-[10px] text-on-surface-variant">
                          {new Date(exam.createdTime).toLocaleDateString("vi-VN")}
                        </span>
                      )}
                    </div>

                    <div className="grid grid-cols-3 gap-2 pt-2 border-t border-whisper-border/50 text-[11px] text-on-surface-variant font-medium">
                      <div>
                        Thời gian: <strong className="block text-on-surface font-bold">{exam.durationMinutes} phút</strong>
                      </div>
                      <div>
                        Số câu: <strong className="block text-on-surface font-bold font-mono">{exam.totalQuestions} câu</strong>
                      </div>
                      <div>
                        Điểm tối đa: <strong className="block text-primary font-bold font-mono">{exam.maxScore} đ</strong>
                      </div>
                    </div>
                  </div>

                  <Button
                    type="button"
                    variant="primary"
                    onClick={() => handleOpenStartDialog(exam)}
                    className="w-full h-10 font-bold justify-center"
                  >
                    <span className="material-symbols-outlined text-[18px] mr-1.5">play_arrow</span>
                    Bắt đầu làm bài
                  </Button>
                </div>
              ))}
            </div>
          )}

          {/* Pagination Bar */}
          {totalPages > 1 && (
            <div className="p-4 bg-pure-surface border border-whisper-border rounded-xl flex items-center justify-between shadow-sm">
              <span className="text-xs text-on-surface-variant font-semibold">
                Trang {pageIndex} / {totalPages}
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pageIndex <= 1 || loading}
                  onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                  className="h-9 px-3 font-bold"
                >
                  <span className="material-symbols-outlined text-[16px] mr-1">chevron_left</span>
                  Trước
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pageIndex >= totalPages || loading}
                  onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                  className="h-9 px-3 font-bold"
                >
                  Sau
                  <span className="material-symbols-outlined text-[16px] ml-1">chevron_right</span>
                </Button>
              </div>
            </div>
          )}
          {/* close exam-mode conditional fragment */}
        </>)}
      </div>

      {/* Start Test Dialog */}
      <StartTestDialog
        isOpen={isStartDialogOpen}
        onClose={() => setIsStartDialogOpen(false)}
        test={selectedTest}
      />

      {/* Adaptive Blueprint Exam Dialog */}
      <AdaptiveBlueprintExamDialog
        isOpen={isAdaptiveDialogOpen}
        onClose={() => setIsAdaptiveDialogOpen(false)}
      />

      {/* Compact TestCode Entry Dialog */}
      <Dialog
        isOpen={isTestCodeDialogOpen}
        onClose={() => {
          if (!resolvingCode) setIsTestCodeDialogOpen(false);
        }}
        isCloseDisabled={resolvingCode}
        className="w-[92vw] max-w-[420px]"
      >
        <div className="flex flex-col h-full select-none">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-base">
              <span className="material-symbols-outlined text-primary text-[22px]">vpn_key</span>
              Nhập mã đề
            </DialogTitle>
            <DialogDescription>
              Nhập mã đề để tìm bài thi
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleResolveCodeSubmit}>
            <DialogContent>
              <div className="flex flex-col gap-3 py-1 select-text">
                <label htmlFor="student-test-code-input" className="sr-only">Nhập mã đề</label>
                <input
                  id="student-test-code-input"
                  type="text"
                  autoFocus
                  value={testCodeInput}
                  disabled={resolvingCode}
                  onChange={(e) => {
                    setTestCodeInput(e.target.value);
                    if (resolveError) setResolveError("");
                  }}
                  placeholder="Ví dụ: MATH7K2P..."
                  className="w-full h-11 px-3.5 bg-surface-container-low border border-whisper-border rounded-xl text-xs text-on-surface font-mono uppercase tracking-wider focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
                />

                {resolveError && (
                  <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2 select-text">
                    <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
                    <span>{resolveError}</span>
                  </div>
                )}
              </div>
            </DialogContent>

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={resolvingCode}
                onClick={() => setIsTestCodeDialogOpen(false)}
                className="min-h-[44px]"
              >
                Hủy
              </Button>
              <Button
                type="submit"
                variant="primary"
                disabled={resolvingCode || !testCodeInput.trim()}
                className="h-11 min-h-[44px] px-5 font-bold shrink-0 min-w-[100px]"
              >
                {resolvingCode ? (
                  <div className="flex items-center gap-2">
                    <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                    <span>Đang tìm...</span>
                  </div>
                ) : (
                  <div className="flex items-center gap-1.5">
                    <span className="material-symbols-outlined text-[18px]">search</span>
                    <span>Tìm đề</span>
                  </div>
                )}
              </Button>
            </DialogFooter>
          </form>
        </div>
      </Dialog>
    </StudentLayout>
  );
}
