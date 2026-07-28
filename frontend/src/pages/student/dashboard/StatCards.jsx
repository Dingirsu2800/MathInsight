import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getStudentHistoryStats } from '../../../services/gradingApi';
import { getWeakTags } from '../../../services/recommenderApi';

export default function StatCards() {
  const [stats, setStats] = useState(null);
  const [competencyScore, setCompetencyScore] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    Promise.allSettled([
      getStudentHistoryStats(),
      getWeakTags()
    ]).then(([historyRes, weakTagsRes]) => {
      if (cancelled) return;

      if (historyRes.status === 'fulfilled' && historyRes.value) {
        setStats(historyRes.value);
      }

      if (weakTagsRes.status === 'fulfilled' && Array.isArray(weakTagsRes.value) && weakTagsRes.value.length > 0) {
        const tags = weakTagsRes.value;
        const avg = tags.reduce((sum, t) => sum + Number(t.officialPoint || 0), 0) / tags.length;
        setCompetencyScore(Math.round(avg * 10) / 10);
      } else if (historyRes.status === 'fulfilled' && historyRes.value?.averageScore != null) {
        setCompetencyScore(Math.round(Number(historyRes.value.averageScore) * 10) / 10);
      }
    }).finally(() => {
      if (!cancelled) setLoading(false);
    });

    return () => { cancelled = true; };
  }, []);

  const totalSessions = stats?.totalSessions ?? 0;
  const sessionsLast30Days = stats?.sessionsLast30Days ?? 0;
  const accuracyPercent = stats?.accuracyPercent ?? 0;
  const compDisplay = competencyScore != null ? `${competencyScore} / 10` : '—';

  const cards = [
    {
      label: 'Điểm năng lực',
      value: compDisplay,
      icon: 'insights',
      colorClass: 'text-primary',
      hoverBg: 'group-hover:bg-primary'
    },
    {
      label: 'Bài đã làm',
      value: loading ? '—' : String(totalSessions),
      icon: 'history_edu',
      colorClass: 'text-primary',
      hoverBg: 'group-hover:bg-primary'
    },
    {
      label: 'Tỉ lệ chính xác',
      value: loading ? '—' : `${accuracyPercent.toFixed(1)}%`,
      icon: 'check_circle',
      colorClass: 'text-tertiary',
      hoverBg: 'group-hover:bg-tertiary'
    },
    {
      label: 'Bài làm (30 ngày)',
      value: loading ? '—' : `${sessionsLast30Days} bài`,
      icon: 'date_range',
      colorClass: 'text-emerald-success',
      hoverBg: 'group-hover:bg-emerald-success'
    },
  ];

  return (
    <section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {cards.map((stat) => (
        <div
          key={stat.label}
          className="bg-pure-surface border border-whisper-border p-5 rounded-2xl flex items-center gap-4 group hover:border-primary/30 transition-all cursor-default shadow-sm"
        >
          <div
            className={`w-12 h-12 rounded-xl bg-surface-container-low flex items-center justify-center ${stat.colorClass} ${stat.hoverBg} group-hover:text-white transition-colors`}
          >
            <MaterialIcon name={stat.icon} filled />
          </div>
          <div>
            <p className="text-outline text-xs font-medium">{stat.label}</p>
            <h3 className={`text-xl font-semibold text-on-surface ${loading ? 'animate-pulse' : ''}`}>
              {stat.value}
            </h3>
          </div>
        </div>
      ))}
    </section>
  );
}
