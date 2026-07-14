import MaterialIcon from '../../../components/ui/MaterialIcon';

/**
 * Improvement call-to-action + review suggestions footer from Stitch competency design.
 */
export default function ImprovementCTACard() {
  return (
    <footer className="flex flex-col md:flex-row gap-6">
      {/* CTA card */}
      <div className="flex-1 bg-primary text-white rounded-xl p-6 flex items-center justify-between overflow-hidden relative">
        <div className="relative z-10">
          <h4 className="text-xl font-semibold mb-2">Cải thiện ngay kết quả</h4>
          <p className="text-sm opacity-90 max-w-md">
            Chúng tôi đã thiết kế một lộ trình học tập cá nhân hóa dựa trên 4 chuyên đề bạn cần
            bổ sung kiến thức.
          </p>
          <button className="mt-4 bg-white text-primary px-6 py-2.5 rounded-lg font-bold hover:bg-primary-fixed transition-colors active:scale-95">
            Bắt đầu lộ trình
          </button>
        </div>
        <div className="absolute right-[-20px] bottom-[-20px] opacity-10">
          <MaterialIcon name="auto_awesome" size={160} />
        </div>
      </div>

      {/* Suggestions card */}
      <div className="w-full md:w-[350px] bg-pure-surface border border-whisper-border rounded-xl p-6">
        <h4 className="text-lg font-semibold text-on-surface mb-4">Gợi ý ôn tập</h4>
        <div className="space-y-4">
          <div className="flex items-center gap-3">
            <div className="w-2 h-2 rounded-full bg-deep-rose" />
            <p className="text-sm text-on-surface">Trắc nghiệm Hình học không gian (Dễ)</p>
          </div>
          <div className="flex items-center gap-3">
            <div className="w-2 h-2 rounded-full bg-amber-warning" />
            <p className="text-sm text-on-surface">Video: Công thức Lượng giác</p>
          </div>
          <div className="flex items-center gap-3">
            <div className="w-2 h-2 rounded-full bg-emerald-success" />
            <p className="text-sm text-on-surface">Thử thách Giải tích nâng cao</p>
          </div>
        </div>
        <button className="w-full mt-6 text-primary font-bold text-center text-sm hover:underline">
          Xem tất cả gợi ý
        </button>
      </div>
    </footer>
  );
}
