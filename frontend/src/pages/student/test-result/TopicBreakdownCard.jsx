import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

// TODO: Replace with API data from /grading/submissions/:id/topic-breakdown
const MOCK_TOPICS = [
  { name: 'Hàm số bậc nhất và bậc hai', percent: 90, level: 'good' },
  { name: 'Phương trình lượng giác', percent: 65, level: 'improve' },
  { name: 'Hình học không gian (Oxyz)', percent: 40, level: 'weak' },
  { name: 'Đạo hàm và ứng dụng', percent: 75, level: 'improve' },
];

function getBarColor(level) {
  switch (level) {
    case 'good': return 'bg-primary';
    case 'improve': return 'bg-primary/70';
    case 'weak': return 'bg-deep-rose';
    default: return 'bg-primary';
  }
}

function getLabelColor(level) {
  return level === 'weak' ? 'text-deep-rose' : 'text-primary';
}

export default function TopicBreakdownCard() {
  return (
    <div className="bg-pure-surface rounded-xl p-8 border border-whisper-border">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-xl font-semibold text-on-surface">Phân tích chủ đề</h3>
        <button className="text-primary text-sm font-bold flex items-center gap-1 hover:underline">
          Chi tiết
          <MaterialIcon name="arrow_forward" size={16} />
        </button>
      </div>

      <div className="space-y-6">
        {MOCK_TOPICS.map((topic) => (
          <div key={topic.name}>
            <div className="flex justify-between mb-2">
              <span className="text-sm font-medium text-on-surface">{topic.name}</span>
              <span className={`text-sm font-bold ${getLabelColor(topic.level)}`}>
                {topic.percent}%
              </span>
            </div>
            <ProgressBar
              value={topic.percent}
              max={100}
              height="h-2.5"
              colorClass={getBarColor(topic.level)}
              trackClass="bg-surface-container"
            />
          </div>
        ))}
      </div>

      {/* Legend */}
      <div className="mt-8 pt-6 border-t border-whisper-border flex items-center gap-4 text-sm text-on-surface-variant">
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-primary" /> Tốt
        </span>
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-primary/60" /> Cần cải thiện
        </span>
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-deep-rose" /> Yếu
        </span>
      </div>
    </div>
  );
}
