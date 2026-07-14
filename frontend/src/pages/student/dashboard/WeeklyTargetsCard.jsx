import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

// TODO: Replace with API data
const MOCK_TARGETS = [
  { label: 'Hoàn thành 5 bài giảng', current: 4, max: 5, icon: 'auto_stories', done: false },
  { label: 'Giải 100 câu trắc nghiệm', current: 65, max: 100, icon: 'task_alt', done: false },
  { label: 'Đạt Streak 7 ngày', current: 5, max: 7, icon: 'bolt', done: false },
];

export default function WeeklyTargetsCard() {
  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2 mb-6">
        <MaterialIcon name="flag" className="text-primary" />
        Mục tiêu tuần này
      </h3>

      <div className="space-y-5">
        {MOCK_TARGETS.map((target) => (
          <div key={target.label} className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-surface-container-low flex items-center justify-center flex-shrink-0">
              <MaterialIcon name={target.icon} size={20} className="text-primary" />
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex justify-between text-sm mb-1.5">
                <span className="text-on-surface font-medium">{target.label}</span>
                <span className="text-primary font-bold">
                  {target.current}/{target.max}
                </span>
              </div>
              <ProgressBar
                value={target.current}
                max={target.max}
                height="h-1.5"
                colorClass="bg-primary"
              />
            </div>
          </div>
        ))}
      </div>

      <button className="mt-6 w-full py-2.5 border border-whisper-border text-on-surface-variant text-sm font-medium rounded-lg hover:bg-surface-container-low transition-colors flex items-center justify-center gap-2">
        <MaterialIcon name="add" size={16} />
        Thêm mục tiêu mới
      </button>
    </div>
  );
}
