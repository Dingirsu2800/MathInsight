import React, { useState, useEffect, useRef } from "react";
import StudentLayout from "../../components/layout/StudentLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Button } from "../../components/ui/button";
import StartTestDialog from "../../components/student/StartTestDialog";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { cn } from "../../utils/cn";

export default function SharedBlueprintExamDiscoveryPage() {
  // TestCode Resolution State
  const [testCodeInput, setTestCodeInput] = useState("");
  const [resolvingCode, setResolvingCode] = useState(false);
  const [resolveError, setResolveError] = useState("");

  // Shared Exams List State
  const [exams, setExams] = useState([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState("");

  // Pagination State
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(12);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  // Dialog State
  const [selectedTest, setSelectedTest] = useState(null);
  const [isStartDialogOpen, setIsStartDialogOpen] = useState(false);

  const resolveInFlightRef = useRef(false);

  const fetchExams = async () => {
    setLoading(true);
    setListError("");
    try {
      const response = await testGeneratorApi.getSharedBlueprintExams({ pageIndex, pageSize });
      const data = response.data || {};
      setExams(data.items || []);
      setTotalCount(data.totalCount || 0);
      const calculatedPages = Math.ceil((data.totalCount || 0) / pageSize) || 1;
      setTotalPages(data.totalPages || calculatedPages);
    } catch (err) {
      setListError(getTestGenErrorMessage(err, "Không thể tải danh sách bài thi. Vui lòng thử lại sau."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchExams();
  }, [pageIndex, pageSize]);

  const handleResolveCodeSubmit = async (e) => {
    e.preventDefault();
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
          title="Đề thi luyện tập"
          subtitle="Chọn đề thi dùng chung phù hợp khối lớp hoặc nhập mã đề thi từ giáo viên để bắt đầu làm bài."
        />

        {/* Enter TestCode Card */}
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 md:p-6 shadow-sm flex flex-col gap-3">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[24px]">vpn_key</span>
            <div>
              <h2 className="text-sm font-bold text-on-surface">Nhập mã đề thi trực tiếp</h2>
              <p className="text-xs text-on-surface-variant">Nhập mã đề thi (TestCode) do giáo viên cấp để tìm bài thi tương ứng.</p>
            </div>
          </div>

          <form onSubmit={handleResolveCodeSubmit} className="flex flex-col sm:flex-row gap-3 mt-1 select-text">
            <div className="flex-1 relative">
              <label htmlFor="student-test-code-input" className="sr-only">Nhập mã đề thi</label>
              <input
                id="student-test-code-input"
                type="text"
                value={testCodeInput}
                disabled={resolvingCode}
                onChange={(e) => {
                  setTestCodeInput(e.target.value);
                  if (resolveError) setResolveError("");
                }}
                placeholder="Ví dụ: MATH7K2P..."
                className="w-full h-11 pl-3.5 pr-3 bg-surface-container-low border border-whisper-border rounded-xl text-xs text-on-surface font-mono uppercase tracking-wider focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all disabled:opacity-60"
              />
            </div>
            <Button
              type="submit"
              variant="primary"
              disabled={resolvingCode}
              className="h-11 min-h-[44px] px-6 font-bold shrink-0"
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
          </form>

          {resolveError && (
            <div role="alert" className="p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center gap-2 select-text">
              <span className="material-symbols-outlined text-[18px] shrink-0">error</span>
              <span>{resolveError}</span>
            </div>
          )}
        </div>

        {/* List Section Title */}
        <div className="flex items-center justify-between border-b border-whisper-border pb-3">
          <h2 className="text-sm font-bold text-on-surface uppercase tracking-wider flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[20px]">quiz</span>
            Danh sách đề thi dùng chung phù hợp
          </h2>
          {totalCount > 0 && (
            <span className="text-xs text-on-surface-variant font-bold font-mono">
              Tổng số: {totalCount} bài thi
            </span>
          )}
        </div>

        {/* Error Banner */}
        {listError && (
          <div role="alert" className="p-4 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold flex items-center justify-between gap-3 select-text">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-[20px] shrink-0">error</span>
              <span>{listError}</span>
            </div>
            <Button variant="outline" size="sm" onClick={fetchExams} className="h-8 text-xs font-bold">Thử lại</Button>
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
            <p className="text-sm font-bold text-on-surface">Chưa có đề thi dùng chung nào phù hợp với khối lớp của bạn.</p>
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
      </div>

      {/* Start Test Dialog */}
      <StartTestDialog
        isOpen={isStartDialogOpen}
        onClose={() => setIsStartDialogOpen(false)}
        test={selectedTest}
      />
    </StudentLayout>
  );
}
