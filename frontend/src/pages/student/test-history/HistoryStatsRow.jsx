import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

// TODO: Replace with API data
const MOCK_STATS = [
  {
    label: 'Tổng số bài làm',
    value: '128',
    trend: '+12',
    trendUp: true,
    sub: 'Trong 30 ngày qua',
    icon: 'history',
    iconBg: 'bg-primary-fixed',
    iconColor: 'text-primary',
  },
  {
    label: 'Điểm trung bình',
    value: '8.4',
    valueSuffix: '/ 10',
    trend: null,
    sub: null,
    icon: 'grade',
    iconBg: 'bg-emerald-success/20',
    iconColor: 'text-emerald-success',
    progressValue: 84,
  },
  {
    label: 'Tỉ lệ chính xác',
    value: '76%',
    trend: '-2%',
    trendUp: false,
    sub: 'So với tháng trước',
    icon: 'check_circle',
    iconBg: 'bg-tertiary-fixed',
    iconColor: 'text-tertiary',
  },
];

export default function HistoryStatsRow() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {MOCK_STATS.map((stat) => (
        <div
          key={stat.label}
          className="bg-pure-surface border border-whisper-border p-5 rounded-xl shadow-sm hover:border-primary/30 transition-colors"
        >
          <div className="flex items-center justify-between mb-2">
            <span className="text-on-surface-variant text-sm font-medium">{stat.label}</span>
            <div className={`w-10 h-10 rounded-lg ${stat.iconBg} flex items-center justify-center ${stat.iconColor}`}>
              <MaterialIcon name={stat.icon} />
            </div>
          </div>
          <div className="flex items-baseline gap-2">
            <span className="text-[32px] font-bold text-on-surface">{stat.value}</span>
            {stat.valueSuffix && (
              <span className="text-on-surface-variant text-xs font-bold">{stat.valueSuffix}</span>
            )}
            {stat.trend && (
              <span
                className={`text-xs font-bold flex items-center ${
                  stat.trendUp ? 'text-emerald-success' : 'text-deep-rose'
                }`}
              >
                <MaterialIcon
                  name={stat.trendUp ? 'trending_up' : 'trending_down'}
                  size={16}
                />
                {stat.trend}
              </span>
            )}
          </div>
          {stat.sub && (
            <p className="text-[12px] text-outline mt-1">{stat.sub}</p>
          )}
          {stat.progressValue && (
            <ProgressBar
              value={stat.progressValue}
              max={100}
              height="h-1.5"
              colorClass="bg-emerald-success"
              className="mt-3"
            />
          )}
        </div>
      ))}
    </div>
  );
}
