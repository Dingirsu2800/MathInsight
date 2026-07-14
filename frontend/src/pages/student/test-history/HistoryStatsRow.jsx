import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import { getStudentHistoryStats } from '../../../services/gradingApi';

export default function HistoryStatsRow() {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    getStudentHistoryStats()
      .then((data) => { if (!cancelled) setStats(data); })
      .catch(() => { /* silently keep null — zeros will render */ })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const totalSessions = stats?.totalSessions ?? 0;
  const sessionsLast30Days = stats?.sessionsLast30Days ?? 0;
  const averageScore = stats?.averageScore ?? 0;
  const accuracyPercent = stats?.accuracyPercent ?? 0;

  const cards = [
    {
      label: 'Tổng số bài làm',
      value: loading ? '—' : String(totalSessions),
      trend: loading ? null : `+${sessionsLast30Days}`,
      trendUp: true,
      sub: 'Trong 30 ngày qua',
      icon: 'history',
      iconBg: 'bg-primary-fixed',
      iconColor: 'text-primary',
    },
    {
      label: 'Điểm trung bình',
      value: loading ? '—' : averageScore.toFixed(1),
      valueSuffix: '/ 10',
      trend: null,
      sub: null,
      icon: 'grade',
      iconBg: 'bg-emerald-success/20',
      iconColor: 'text-emerald-success',
      progressValue: loading ? 0 : Math.round(averageScore * 10),
    },
    {
      label: 'Tỉ lệ chính xác',
      value: loading ? '—' : `${accuracyPercent.toFixed(1)}%`,
      trend: null,
      sub: null,
      icon: 'check_circle',
      iconBg: 'bg-tertiary-fixed',
      iconColor: 'text-tertiary',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {cards.map((stat) => (
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
            <span className={`text-[32px] font-bold text-on-surface ${loading ? 'animate-pulse' : ''}`}>
              {stat.value}
            </span>
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
          {stat.progressValue != null && (
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


