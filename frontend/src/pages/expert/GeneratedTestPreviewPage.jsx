import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import GeneratedTestSection from "../../components/expert/GeneratedTestSection";
import { testGeneratorApi } from "../../services/testGeneratorApi";
import { getTestGenErrorMessage } from "../../utils/testGenerationErrorLocalizer";
import { cn } from "../../utils/cn";

export default function GeneratedTestPreviewPage() {
  const { testId } = useParams();
  const navigate = useNavigate();

  const [testData, setTestData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Archive modal & request states
  const [isArchiveOpen, setIsArchiveOpen] = useState(false);
  const [archiveLoading, setArchiveLoading] = useState(false);
  const [archiveError, setArchiveError] = useState("");

  // Copy feedback state
  const [copiedCode, setCopiedCode] = useState(false);

  const archiveSubmittingRef = useRef(false);

  const fetchPreview = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await testGeneratorApi.getExpertTestPreview(testId);
      setTestData(res.data);
    } catch (err) {
      setError(getTestGenErrorMessage(err, "Không thể tải dữ liệu kiểm duyệt đề thi. Vui lòng thử lại."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPreview();
  }, [testId]);

  const handleCopyTestCode = () => {
    if (!testData?.testCode) return;
    navigator.clipboard.writeText(testData.testCode);
    setCopiedCode(true);
    setTimeout(() => setCopiedCode(false), 2500);
  };

  const handleArchiveConfirm = async () => {
    if (archiveSubmittingRef.current) return;
    archiveSubmittingRef.current = true;
    setArchiveLoading(true);
    setArchiveError("");

    try {
      const res = await testGeneratorApi.archiveSharedBlueprintExam(testId);
      setIsArchiveOpen(false);
      // Update local state status to Archived
      setTestData((prev) => (prev ? { ...prev, testStatus: res.data.testStatus || "Archived" } : prev));
    } catch (err) {
      setArchiveError(getTestGenErrorMessage(err, "Không thể lưu trữ đề thi. Vui lòng thử lại."));
    } finally {
      archiveSubmittingRef.current = false;
      setArchiveLoading(false);
    }
  };

  if (loading) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col items-center justify-center min-h-[350px]">
          <div className="w-10 h-10 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
          <p className="mt-4 text-sm text-on-surface-variant font-semibold">Đang tải bản kiểm duyệt đề thi...</p>
        </div>
      </ExpertLayout>
    );
  }

  if (error || !testData) {
    return (
      <ExpertLayout>
        <div className="p-gutter flex flex-col gap-4 max-w-xl mx-auto text-center mt-12 select-text">
          <span className="material-symbols-outlined text-[48px] text-error">error</span>
          <h2 className="text-xl font-bold text-on-background">Không thể mở bản kiểm duyệt đề</h2>
          <p className="text-sm text-on-surface-variant">{error || "Không tìm thấy thông tin đề thi đã sinh."}</p>
          <div className="flex justify-center gap-3 mt-4">
            <Button variant="outline" onClick={() => navigate("/expert/blueprints")}>Danh sách cấu trúc đề</Button>
            <Button variant="primary" onClick={fetchPreview}>Thử lại</Button>
          </div>
        </div>
      </ExpertLayout>
    );
  }

  const isArchived = testData.testStatus === "Archived";
  const sections = testData.sections || [];

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto select-none">
        {/* Page Header */}
        <DashboardPageHeader
          title={testData.testName}
          subtitle="Kiểm duyệt nội dung câu hỏi, mã nguồn đáp án, lời giải chi tiết và các chỉ tiêu ma trận."
        >
          <div className="flex flex-wrap items-center gap-2.5">
            <Button
              variant="outline"
              onClick={() => navigate(`/expert/blueprints/${testData.blueprintId}`)}
            >
              Quay lại cấu trúc đề
            </Button>

            {!isArchived ? (
              <Button
                variant="destructive"
                disabled={archiveLoading}
                onClick={() => {
                  setArchiveError("");
                  setIsArchiveOpen(true);
                }}
              >
                <span className="material-symbols-outlined text-[18px] mr-1">archive</span>
                Lưu trữ đề
              </Button>
            ) : (
              <span className="px-3.5 py-1.5 rounded-lg border border-whisper-border bg-surface-container-low text-on-surface-variant font-bold text-xs flex items-center gap-1.5">
                <span className="material-symbols-outlined text-[18px]">inventory_2</span>
                Đã lưu trữ
              </span>
            )}
          </div>
        </DashboardPageHeader>

        {/* Notice Banners */}
        <div className="flex flex-col gap-3">
          {/* Mode Indicator Banner */}
          <div className="p-3.5 bg-primary/10 border border-primary/20 rounded-xl text-primary flex items-center justify-between gap-3 text-xs font-bold">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined text-[20px]">admin_panel_settings</span>
              <span>Chế độ xem dành cho chuyên gia (Xem toàn bộ đáp án, lời giải & trọng số)</span>
            </div>
            <span className="bg-primary text-on-primary text-[10px] font-extrabold px-2.5 py-0.5 rounded uppercase tracking-wider">
              Dành cho chuyên gia
            </span>
          </div>

          {/* Archived Notice Banner */}
          {isArchived && (
            <div className="p-4 bg-amber-warning/10 border border-amber-warning/20 rounded-xl text-on-surface flex items-start gap-3 select-text text-xs">
              <span className="material-symbols-outlined text-amber-warning text-[22px] shrink-0 mt-0.5">inventory_2</span>
              <div>
                <strong className="block text-sm font-bold text-amber-warning mb-0.5">Đề thi đã được lưu trữ</strong>
                <p className="text-on-surface-variant leading-relaxed">
                  Đề đã được lưu trữ và không thể bắt đầu phiên làm bài mới. Các phiên làm bài đã tạo trước đó vẫn có thể tiếp tục bình thường.
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Test Summary Metadata Panel */}
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 select-text">
          {/* Test Code */}
          <div className="flex flex-col gap-1 border-r border-whisper-border/50 pr-3">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Mã đề thi</span>
            <div className="flex items-center gap-2">
              <span className="text-base font-black text-primary font-mono tracking-wider">
                {testData.testCode}
              </span>
              <button
                type="button"
                onClick={handleCopyTestCode}
                title="Sao chép mã đề"
                aria-label="Sao chép mã đề thi"
                className="p-1 hover:bg-surface-container rounded text-on-surface-variant hover:text-primary transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">
                  {copiedCode ? "check" : "content_copy"}
                </span>
              </button>
            </div>
            {copiedCode && <span className="text-[10px] text-emerald-success font-bold">Đã sao chép!</span>}
          </div>

          {/* Status */}
          <div className="flex flex-col gap-1 border-r border-whisper-border/50 pr-3">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Trạng thái</span>
            <div className="flex flex-col gap-1 items-start">
              <Badge variant={isArchived ? "secondary" : "success"}>
                {isArchived ? "Đã lưu trữ" : "Đang hoạt động"}
              </Badge>
              {testData.generationType && (
                <span className="text-[10px] font-bold text-on-surface-variant">
                  Loại: {testData.generationType === "Fixed" ? "Cố định" : "Ngẫu nhiên"}
                </span>
              )}
            </div>
          </div>

          {/* Duration */}
          <div className="flex flex-col gap-1 border-r border-whisper-border/50 pr-3">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Thời gian</span>
            <span className="text-sm font-bold text-on-surface">
              {testData.durationMinutes} phút
            </span>
          </div>

          {/* Question Count */}
          <div className="flex flex-col gap-1 border-r border-whisper-border/50 pr-3">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Số câu hỏi</span>
            <span className="text-sm font-bold text-on-surface font-mono">
              {testData.totalQuestions} câu
            </span>
          </div>

          {/* Max Score */}
          <div className="flex flex-col gap-1 border-r border-whisper-border/50 pr-3">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Tổng điểm</span>
            <span className="text-sm font-bold text-primary font-mono">
              {testData.maxScore} điểm
            </span>
          </div>

          {/* Created Time */}
          <div className="flex flex-col gap-1">
            <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Ngày tạo đề</span>
            <span className="text-xs font-semibold text-on-surface">
              {testData.createdTime ? new Date(testData.createdTime).toLocaleString("vi-VN") : "N/A"}
            </span>
          </div>
        </div>


        {/* Section Quick Navigator */}
        {sections.length > 1 && (
          <div className="bg-pure-surface border border-whisper-border rounded-xl p-3.5 shadow-sm flex items-center gap-3 overflow-x-auto">
            <span className="text-xs font-bold text-on-surface-variant uppercase shrink-0">Chuyển nhanh:</span>
            <div className="flex gap-2">
              {sections.map((sec, idx) => {
                const secNum = sec.sectionOrder || idx + 1;
                return (
                  <a
                    key={sec.blueprintSectionId || idx}
                    href={`#section-${secNum}`}
                    className="px-3 py-1 rounded-lg bg-surface-container-low hover:bg-primary/10 border border-whisper-border text-xs font-bold text-on-surface hover:text-primary transition-all shrink-0"
                  >
                    Phần {secNum}: {sec.sectionName}
                  </a>
                );
              })}
            </div>
          </div>
        )}

        {/* Sections and Questions */}
        <div className="flex flex-col gap-8">
          {sections.map((sec, idx) => (
            <GeneratedTestSection
              key={sec.blueprintSectionId || idx}
              section={sec}
              sectionIndex={idx}
            />
          ))}
        </div>
      </div>

      {/* Archive Confirmation Dialog */}
      <Dialog isOpen={isArchiveOpen} onClose={() => !archiveLoading && setIsArchiveOpen(false)}>
        <DialogHeader>
          <DialogTitle className="text-error flex items-center gap-2">
            <span className="material-symbols-outlined text-[22px]">archive</span>
            Xác nhận lưu trữ đề thi
          </DialogTitle>
          <DialogDescription>
            Lưu trữ biến thể đề thi <span className="font-bold text-on-surface">"{testData.testName}"</span> (Mã: {testData.testCode}).
          </DialogDescription>
        </DialogHeader>

        <DialogContent>
          <p className="text-xs text-on-surface-variant leading-relaxed select-text">
            Mã đề sẽ không thể dùng để bắt đầu phiên mới; các phiên đang làm vẫn có thể tiếp tục. Phiên bản này không thể kích hoạt lại.
          </p>

          {archiveError && (
            <div role="alert" className="mt-3 p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold">
              {archiveError}
            </div>
          )}
        </DialogContent>

        <DialogFooter>
          <Button
            variant="outline"
            disabled={archiveLoading}
            onClick={() => setIsArchiveOpen(false)}
          >
            Hủy
          </Button>
          <Button
            variant="destructive"
            disabled={archiveLoading}
            onClick={handleArchiveConfirm}
          >
            {archiveLoading ? "Đang lưu trữ..." : "Xác nhận Lưu trữ"}
          </Button>
        </DialogFooter>
      </Dialog>
    </ExpertLayout>
  );
}
