import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

// TODO: Replace with API data from /recommender/weak-tags
const MOCK_WEAK_TOPICS = [
  {
    name: 'Số phức: Tập hợp điểm',
    score: 3.5,
    statusLabel: 'Cần cải thiện',
    statusClass: 'text-error bg-error-container',
    barColor: 'bg-error',
    hint: 'Gợi ý: Làm lại 5 bài tập mức độ "Trung bình" để tăng độ hiểu.',
  },
  {
    name: 'Tích phân: Ứng dụng diện tích',
    score: 6.2,
    statusLabel: 'Sắp hoàn thành',
    statusClass: 'text-tertiary bg-tertiary-fixed',
    barColor: 'bg-tertiary-fixed-dim',
    hint: 'Học thêm bài giảng "Công thức Newton-Leibniz".',
  },
  {
    name: 'Hình Oxyz: Phương trình mặt phẳng',
    score: 8.8,
    statusLabel: 'Đã ổn định',
    statusClass: 'text-emerald-success bg-emerald-success/20',
    barColor: 'bg-emerald-success',
    hint: null,
  },
];

export default function WeakTopicsCard() {
  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2">
          <MaterialIcon name="trending_down" className="text-deep-rose" />
          Kiến thức cần củng cố
        </h3>
        <a className="text-primary text-xs font-bold hover:underline" href="/student/competency">
          Chi tiết
        </a>
      </div>

      <div className="space-y-4">
        {MOCK_WEAK_TOPICS.map((topic) => (
          <div
            key={topic.name}
            className={`p-4 rounded-xl border ${
              topic.score < 5
                ? 'bg-error-container/20 border-error-container/50'
                : 'bg-surface-container-low border-whisper-border'
            }`}
          >
            <div className="flex justify-between items-center mb-2">
              <span className="text-sm text-on-surface font-bold">{topic.name}</span>
              <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${topic.statusClass}`}>
                {topic.statusLabel}
              </span>
            </div>
            <ProgressBar
              value={topic.score}
              max={10}
              colorClass={topic.barColor}
              trackClass="bg-surface-container-high"
            />
            {topic.hint && (
              <p className="text-[11px] text-outline mt-2 italic">{topic.hint}</p>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
