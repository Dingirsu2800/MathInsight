import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

// TODO: Replace with API data
const MOCK_TOPICS = [
  {
    name: 'Hàm số & Đồ thị',
    subtitle: 'Chương 1 - Đại số lớp 12',
    score: 8.5,
    icon: 'function',
    iconBg: 'bg-primary-fixed',
    iconColor: 'text-primary',
    status: 'Thành thạo',
    statusClass: 'bg-emerald-success/20 text-emerald-success',
    barColor: 'bg-emerald-success',
    badgeBorder: 'border-emerald-success',
    badgeText: 'text-emerald-success',
    flagged: false,
  },
  {
    name: 'Hình học không gian',
    subtitle: 'Chương 2 - Hình học lớp 12',
    score: 4.2,
    icon: 'change_history',
    iconBg: 'bg-error-container',
    iconColor: 'text-error',
    status: 'Đang học',
    statusClass: 'bg-surface-container-high text-on-surface-variant',
    barColor: 'bg-error',
    badgeBorder: 'border-error',
    badgeText: 'text-error',
    flagged: true,
  },
  {
    name: 'Lượng giác',
    subtitle: 'Toán nâng cao 11',
    score: 6.5,
    icon: 'rebase_edit',
    iconBg: 'bg-surface-container-high',
    iconColor: 'text-on-surface-variant',
    status: 'Đang học',
    statusClass: 'bg-surface-container-high text-on-surface-variant',
    barColor: 'bg-amber-warning',
    badgeBorder: 'border-amber-warning',
    badgeText: 'text-amber-warning',
    flagged: false,
  },
  {
    name: 'Xác suất thống kê',
    subtitle: 'Toán ứng dụng',
    score: 7.8,
    icon: 'data_exploration',
    iconBg: 'bg-primary-fixed',
    iconColor: 'text-primary',
    status: 'Thành thạo',
    statusClass: 'bg-emerald-success/20 text-emerald-success',
    barColor: 'bg-emerald-success',
    badgeBorder: 'border-emerald-success',
    badgeText: 'text-emerald-success',
    flagged: false,
  },
];

export default function TopicMasteryGrid() {
  return (
    <section>
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-on-surface">Chi tiết từng chuyên đề</h3>
        <div className="flex gap-2">
          <button className="px-4 py-2 bg-pure-surface border border-whisper-border rounded-lg font-mono text-xs hover:bg-surface-container transition-colors">
            Theo tiến độ
          </button>
          <button className="px-4 py-2 bg-pure-surface border border-whisper-border rounded-lg font-mono text-xs hover:bg-surface-container transition-colors">
            Theo điểm số
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
        {MOCK_TOPICS.map((topic) => (
          <div
            key={topic.name}
            className={`bg-pure-surface border rounded-xl p-5 flex flex-col relative overflow-hidden transition-transform hover:-translate-y-1 ${
              topic.flagged
                ? 'border-2 border-deep-rose/30'
                : 'border-whisper-border'
            }`}
          >
            {/* Flag badge */}
            {topic.flagged && (
              <div className="absolute top-0 right-0">
                <div className="bg-deep-rose text-white text-[10px] font-bold px-3 py-1 rounded-bl-xl shadow-sm flex items-center gap-1">
                  <MaterialIcon name="priority_high" size={12} filled />
                  CẦN PHỤ ĐẠO
                </div>
              </div>
            )}

            <div className="flex justify-between items-start mb-4">
              <div className={`p-2 rounded-lg ${topic.iconBg}`}>
                <MaterialIcon name={topic.icon} className={topic.iconColor} />
              </div>
              <span className={`text-[11px] px-2 py-0.5 rounded-full uppercase font-bold ${topic.statusClass}`}>
                {topic.status}
              </span>
            </div>

            <h4 className="text-base font-bold mb-1 text-on-surface">{topic.name}</h4>
            <p className="text-on-surface-variant text-sm mb-4">{topic.subtitle}</p>

            <div className="flex items-center gap-4 mt-auto">
              <div className="flex-1">
                <div className="flex justify-between font-mono text-xs mb-1">
                  <span>Năng lực</span>
                  <span className={`font-bold ${topic.badgeText}`}>{topic.score}/10</span>
                </div>
                <ProgressBar
                  value={topic.score}
                  max={10}
                  height="h-2"
                  colorClass={topic.barColor}
                  trackClass="bg-surface-container"
                />
              </div>
              <div className={`w-10 h-10 border-2 ${topic.badgeBorder} rounded-full flex items-center justify-center`}>
                <span className={`font-mono text-xs ${topic.badgeText}`}>
                  {Math.round(topic.score * 10)}%
                </span>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
