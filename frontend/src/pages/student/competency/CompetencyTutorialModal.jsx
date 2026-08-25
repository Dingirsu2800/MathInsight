import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { Button } from '../../../components/ui/button';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogFooter,
} from '../../../components/ui/dialog';

const TUTORIAL_STEPS = [
  {
    step: 1,
    title: 'Điểm Năng Lực Tổng Quát (Overall Competency Score)',
    icon: 'analytics',
    badge: 'Bước 1 / 4',
    content: (
      <div className="space-y-4">
        <p className="text-sm text-on-surface leading-relaxed">
          <strong>Điểm năng lực tổng quát</strong> (thang điểm từ <strong>0.0 đến 10.0</strong>) thể hiện trình độ Toán học chung của bạn ở Dashboard và trang Năng lực.
        </p>

        <div className="bg-surface-container-low border border-whisper-border rounded-xl p-4 space-y-3">
          <div className="flex items-start gap-3">
            <span className="material-symbols-outlined text-primary text-[20px] mt-0.5">functions</span>
            <div className="text-xs text-on-surface-variant leading-relaxed">
              <strong className="text-on-surface">Công thức Điểm tổng quát:</strong> Là trung bình cộng điểm năng lực chính thức của tất cả các chủ đề bạn <em>đã từng thực hành</em> (có ít nhất 1 bài làm).
            </div>
          </div>

          <div className="flex items-start gap-3">
            <span className="material-symbols-outlined text-primary text-[20px] mt-0.5">do_not_disturb_on</span>
            <div className="text-xs text-on-surface-variant leading-relaxed">
              <strong className="text-on-surface">Bỏ qua chủ đề chưa học:</strong> Các chủ đề chưa làm bài lần nào sẽ không bị tính là 0 điểm, giúp bảo vệ điểm tổng quát của bạn không bị kéo xuống vô lý.
            </div>
          </div>
        </div>

        {/* Example Box */}
        <div className="bg-primary/5 border border-primary/20 rounded-xl p-3.5 space-y-2 text-xs">
          <div className="font-bold text-primary flex items-center gap-1.5">
            <span className="material-symbols-outlined text-[18px]">lightbulb</span>
            <span>Ví dụ tính Điểm năng lực tổng quát:</span>
          </div>
          <p className="text-on-surface-variant leading-relaxed">
            Giả sử bạn đã thực hành <strong>3 chủ đề</strong>:
            <br />
            • Đại số: <strong className="text-on-surface">8.0</strong> | Hình học: <strong className="text-on-surface">6.0</strong> | Lượng giác: <strong className="text-on-surface">7.0</strong>
            <br />
            ➔ <span className="font-semibold text-primary">Điểm tổng quát = (8.0 + 6.0 + 7.0) / 3 = 7.0 / 10</span> (Trung bình - Khá)
          </p>
        </div>
      </div>
    ),
  },
  {
    step: 2,
    title: 'Điểm Năng Lực Cho Mỗi Chủ Đề (Official Point)',
    icon: 'tune',
    badge: 'Bước 2 / 4',
    content: (
      <div className="space-y-4">
        <p className="text-sm text-on-surface leading-relaxed">
          Sau khi hiểu về Điểm tổng quát, hãy cùng tìm hiểu cách từng <strong>Điểm năng lực chủ đề</strong> được tạo ra.
        </p>

        <div className="bg-surface-container-low border border-whisper-border rounded-xl p-4 space-y-3">
          <div className="flex items-start gap-3">
            <span className="material-symbols-outlined text-primary text-[20px] mt-0.5">adjust</span>
            <div className="text-xs text-on-surface-variant leading-relaxed">
              <strong className="text-on-surface">Điểm số độc lập:</strong> Mỗi chủ đề học tập (như <em>Hàm số</em>, <em>Phương trình</em>, <em>Hình không gian</em>...) giữ một điểm số riêng từ <strong>0.0 đến 10.0</strong>.
            </div>
          </div>
          <div className="flex items-start gap-3">
            <span className="material-symbols-outlined text-primary text-[20px] mt-0.5">update</span>
            <div className="text-xs text-on-surface-variant leading-relaxed">
              <strong className="text-on-surface">Tự động cập nhật:</strong> Điểm của từng chủ đề sẽ tự động tính toán lại ngay sau mỗi khi bạn nộp bài luyện tập hoặc đề thi liên quan đến chủ đề đó.
            </div>
          </div>
        </div>
      </div>
    ),
  },
  {
    step: 3,
    title: 'Tỷ lệ trọng số (55% Đề thi + 45% Luyện tập)',
    icon: 'calculate',
    badge: 'Bước 3 / 4',
    content: (
      <div className="space-y-4">
        <p className="text-sm text-on-surface leading-relaxed">
          Điểm chính thức của một chủ đề kết hợp từ <strong>2 nguồn làm bài</strong> theo tỷ lệ trọng số cố định:
        </p>

        {/* Comparison grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {/* Card Đề thi (55%) */}
          <div className="bg-tertiary-container/10 border border-tertiary/30 rounded-xl p-3.5 flex flex-col gap-1.5">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 text-tertiary font-bold text-xs uppercase tracking-wider">
                <span className="material-symbols-outlined text-[18px]">assignment_turned_in</span>
                <span>Bài Đề Thi</span>
              </span>
              <span className="px-2 py-0.5 text-[11px] font-extrabold rounded-full bg-tertiary text-white">
                55%
              </span>
            </div>
            <p className="text-xs text-on-surface-variant leading-relaxed">
              Đánh giá áp lực phòng thi. Hệ thống tự động ưu tiên các bài thi gần nhất bằng thuật toán suy giảm lũy thừa.
            </p>
          </div>

          {/* Card Luyện tập (45%) */}
          <div className="bg-primary-container/10 border border-primary/30 rounded-xl p-3.5 flex flex-col gap-1.5">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1.5 text-primary font-bold text-xs uppercase tracking-wider">
                <span className="material-symbols-outlined text-[18px]">fitness_center</span>
                <span>Bài Luyện Tập</span>
              </span>
              <span className="px-2 py-0.5 text-[11px] font-extrabold rounded-full bg-primary text-white">
                45%
              </span>
            </div>
            <p className="text-xs text-on-surface-variant leading-relaxed">
              Tích lũy theo từng câu hỏi. Tăng khi làm đúng câu khó và trừ nhẹ khi làm sai câu dễ.
            </p>
          </div>
        </div>

        {/* Dynamic calculation example box */}
        <div className="bg-surface-container-low border border-whisper-border rounded-xl p-3.5 space-y-2">
          <div className="text-xs font-bold text-on-surface flex items-center gap-1.5">
            <span className="material-symbols-outlined text-primary text-[18px]">functions</span>
            <span>Công thức & Ví dụ tính toán nhanh:</span>
          </div>
          <div className="bg-pure-surface border border-whisper-border rounded-lg p-2.5 text-xs font-mono text-center text-primary font-semibold">
            Điểm chủ đề = (55% × Điểm Đề Thi) + (45% × Điểm Luyện Tập)
          </div>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            👉 Nếu bạn đạt <strong>6.0 / 10</strong> ở bài Đề thi và <strong>8.0 / 10</strong> ở bài Luyện tập:
            <br />
            <span className="font-semibold text-on-surface">
              Điểm chủ đề = (55% × 6.0) + (45% × 8.0) = 3.3 + 3.6 = <strong className="text-primary font-bold">6.9 / 10</strong>
            </span>
          </p>
        </div>
      </div>
    ),
  },
  {
    step: 4,
    title: 'Ví dụ hành trình thực tế chi tiết',
    icon: 'route',
    badge: 'Bước 4 / 4',
    content: (
      <div className="space-y-3">
        <p className="text-xs text-on-surface-variant leading-relaxed">
          Hãy cùng theo dõi hành trình học tập của bạn ở chủ đề <strong>Hàm số bậc hai</strong>:
        </p>

        <div className="space-y-3">
          {/* Phase 1 */}
          <div className="p-3 rounded-xl bg-surface-container-low border border-whisper-border space-y-1">
            <div className="flex items-center justify-between text-xs font-bold text-on-surface">
              <span className="flex items-center gap-1.5">
                <span className="w-5 h-5 rounded-full bg-primary/10 text-primary text-[11px] flex items-center justify-center font-bold">1</span>
                <span>Giai đoạn 1: Chỉ làm Luyện tập (Chưa thi)</span>
              </span>
              <span className="text-primary font-mono font-bold text-xs">3.2 / 10</span>
            </div>
            <p className="text-[11px] text-on-surface-variant leading-relaxed pl-6">
              Bạn luyện tập 10 câu nâng Điểm Luyện tập lên <strong>7.0</strong>. Vì chưa làm bài thi nào (Điểm Đề thi = 0.0):
              <br />
              <span className="font-mono text-[10px] text-outline">➔ Điểm chủ đề = (55% × 0.0) + (45% × 7.0) = 3.15 ➔ làm tròn <strong>3.2</strong></span>
            </p>
          </div>

          {/* Phase 2 */}
          <div className="p-3 rounded-xl bg-emerald-success/10 border border-emerald-success/30 space-y-1">
            <div className="flex items-center justify-between text-xs font-bold text-on-surface">
              <span className="flex items-center gap-1.5 text-emerald-success">
                <span className="w-5 h-5 rounded-full bg-emerald-success text-white text-[11px] flex items-center justify-center font-bold">2</span>
                <span>Giai đoạn 2: Hoàn thành 1 bài Đề thi (8.0 điểm)</span>
              </span>
              <span className="text-emerald-success font-mono font-bold text-xs">7.6 / 10 🎉</span>
            </div>
            <p className="text-[11px] text-on-surface-variant leading-relaxed pl-6">
              Bạn làm bài thi 45 phút đạt <strong>8.0 / 10</strong> phần Hàm số. Điểm Đề thi 8.0 được kích hoạt:
              <br />
              <span className="font-mono text-[10px] text-emerald-success/90">➔ Điểm chủ đề = (55% × 8.0) + (45% × 7.0) = 4.4 + 3.15 = <strong>7.55 / 10</strong> ("Thành thạo")</span>
            </p>
          </div>

          {/* Phase 3 */}
          <div className="p-3 rounded-xl bg-primary/10 border border-primary/30 space-y-1">
            <div className="flex items-center justify-between text-xs font-bold text-on-surface">
              <span className="flex items-center gap-1.5 text-primary">
                <span className="w-5 h-5 rounded-full bg-primary text-white text-[11px] flex items-center justify-center font-bold">3</span>
                <span>Giai đoạn 3: Ôn luyện nâng cao & Thi tiếp</span>
              </span>
              <span className="text-primary font-mono font-bold text-xs">8.8 / 10 ⭐</span>
            </div>
            <p className="text-[11px] text-on-surface-variant leading-relaxed pl-6">
              Bạn nâng Điểm Luyện tập lên <strong>8.5</strong> và bài thi tiếp theo đạt <strong>9.0</strong>:
              <br />
              <span className="font-mono text-[10px] text-primary">➔ Điểm chủ đề = (55% × 9.0) + (45% × 8.5) = 4.95 + 3.825 = <strong>8.8 / 10</strong> ("Giỏi")</span>
            </p>
          </div>
        </div>
      </div>
    ),
  },
];

export default function CompetencyTutorialModal({ isOpen, onClose }) {
  const [currentStep, setCurrentStep] = useState(0);

  if (!isOpen) return null;

  const current = TUTORIAL_STEPS[currentStep];
  const isFirst = currentStep === 0;
  const isLast = currentStep === TUTORIAL_STEPS.length - 1;

  const handleNext = () => {
    if (!isLast) {
      setCurrentStep((prev) => prev + 1);
    } else {
      handleClose();
    }
  };

  const handlePrev = () => {
    if (!isFirst) {
      setCurrentStep((prev) => prev - 1);
    }
  };

  const handleClose = () => {
    setCurrentStep(0);
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={handleClose}
      className="max-w-lg p-6 rounded-2xl border border-whisper-border bg-pure-surface shadow-2xl animate-in fade-in zoom-in-95 duration-200"
    >
      <div className="flex flex-col select-none py-1">
        {/* Header */}
        <div className="flex items-center gap-3 mb-4 pr-6">
          <div className="w-12 h-12 rounded-xl bg-primary/10 flex items-center justify-center text-primary flex-shrink-0">
            <MaterialIcon name={current.icon} className="text-[28px]" />
          </div>
          <div>
            <span className="text-[11px] font-bold text-primary uppercase tracking-wider">
              {current.badge}
            </span>
            <DialogTitle className="text-lg font-bold text-on-background leading-snug">
              {current.title}
            </DialogTitle>
          </div>
        </div>

        {/* Content */}
        <DialogContent className="py-2">
          {current.content}
        </DialogContent>

        {/* Step dots indicator & Footer buttons */}
        <DialogFooter className="mt-6 pt-4 border-t border-whisper-border flex flex-row items-center justify-between">
          {/* Step dots */}
          <div className="flex items-center gap-1.5" aria-label={`Trang ${currentStep + 1} / ${TUTORIAL_STEPS.length}`}>
            {TUTORIAL_STEPS.map((s, idx) => (
              <button
                key={s.step}
                type="button"
                aria-label={`Chuyển đến trang ${s.step}`}
                onClick={() => setCurrentStep(idx)}
                className={`h-2 rounded-full transition-all ${
                  idx === currentStep
                    ? 'w-6 bg-primary'
                    : 'w-2 bg-whisper-border hover:bg-outline/50'
                }`}
              />
            ))}
          </div>

          {/* Navigation Buttons */}
          <div className="flex items-center gap-2">
            {!isFirst && (
              <Button
                type="button"
                variant="outline"
                onClick={handlePrev}
                className="min-h-[38px] px-4 text-xs font-semibold"
              >
                Quay lại
              </Button>
            )}
            <Button
              type="button"
              variant="primary"
              onClick={handleNext}
              className="min-h-[38px] px-5 text-xs font-bold flex items-center gap-1 shadow-md shadow-primary/20 hover:shadow-lg transition-all"
            >
              <span>{isLast ? 'Đã hiểu' : 'Tiếp theo'}</span>
              {!isLast && <MaterialIcon name="arrow_forward" className="text-[16px]" />}
            </Button>
          </div>
        </DialogFooter>
      </div>
    </Dialog>
  );
}
