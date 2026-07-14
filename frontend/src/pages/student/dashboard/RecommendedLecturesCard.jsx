import MaterialIcon from '../../../components/ui/MaterialIcon';

// TODO: Replace with API data from /recommender/practice-suggestions
const MOCK_LECTURES = [
  {
    id: 1,
    title: 'Giải nhanh cực trị hàm số với...',
    teacher: 'GV: Trần Thanh Tâm',
    views: '1.2k lượt xem',
    duration: '18:24',
    chipLabel: 'Tổng quan học tập',
    chipColor: 'bg-primary',
  },
  {
    id: 2,
    title: 'Toàn tập hình nón, trụ, cầu – Ôn t...',
    teacher: 'GV: Lê Mạnh Hùng',
    views: '850 lượt xem',
    duration: '22:05',
    chipLabel: 'Làm chủ hình học không gian',
    chipColor: 'bg-tertiary',
  },
];

export default function RecommendedLecturesCard() {
  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2 mb-6">
        <MaterialIcon name="auto_awesome" className="text-primary" />
        Bài giảng đề xuất riêng cho bạn
      </h3>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {MOCK_LECTURES.map((lecture) => (
          <div
            key={lecture.id}
            className="group cursor-pointer rounded-xl overflow-hidden border border-whisper-border hover:border-primary/30 transition-all"
          >
            {/* Thumbnail placeholder */}
            <div className="relative w-full h-[180px] bg-surface-container overflow-hidden">
              <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-primary-container/30">
                <MaterialIcon name="play_circle" size={48} className="text-primary/40" />
              </div>
              <div className={`absolute top-3 left-3 ${lecture.chipColor} text-white text-[10px] font-bold px-2.5 py-1 rounded`}>
                {lecture.chipLabel}
              </div>
              <div className="absolute bottom-3 right-3 bg-black/70 text-white text-xs font-mono px-2 py-0.5 rounded">
                {lecture.duration}
              </div>
            </div>
            <div className="p-3">
              <h4 className="text-sm font-bold text-on-surface truncate group-hover:text-primary transition-colors">
                {lecture.title}
              </h4>
              <p className="text-xs text-outline mt-1">
                {lecture.teacher} • {lecture.views}
              </p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
