import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getStreak, getBadges } from '../../../services/gamificationApi';
import { getStudentHistoryStats } from '../../../services/gradingApi';
import { getWeakTags } from '../../../services/recommenderApi';

const STAT_META = [
  { key: 'competency', label: 'Điểm năng lực', icon: 'insights', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { key: 'sessions', label: 'Bài đã làm', icon: 'history_edu', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { key: 'accuracy', label: 'Tỉ lệ chính xác', icon: 'check_circle', colorClass: 'text-tertiary', hoverBg: 'group-hover:bg-tertiary' },
  { key: 'sessionsLast30Days', label: 'Bài làm (30 ngày)', icon: 'date_range', colorClass: 'text-emerald-success', hoverBg: 'group-hover:bg-emerald-success' },
  { key: 'streak', label: 'Chuỗi ngày 🔥', icon: 'local_fire_department', colorClass: 'text-deep-rose', hoverBg: 'group-hover:bg-deep-rose' },
  { key: 'badges', label: 'Huy hiệu 🏅', icon: 'workspace_premium', colorClass: 'text-emerald-success', hoverBg: 'group-hover:bg-emerald-success' },
];

export default function StatCards() {
  const [values, setValues] = useState({
    competency: '—',
    sessions: '—',
    accuracy: '—',
    sessionsLast30Days: '—',
    streak: '—',
    badges: '—',
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);

    async function load() {
      const [streakResult, badgesResult, statsResult, weakTagsResult] = await Promise.allSettled([
        getStreak(),
        getBadges(),
        getStudentHistoryStats(),
        getWeakTags(),
      ]);

      if (!isMounted) return;

      const stats = statsResult.status === 'fulfilled' ? statsResult.value : null;

      let competency = '—';
      if (weakTagsResult.status === 'fulfilled' && Array.isArray(weakTagsResult.value) && weakTagsResult.value.length > 0) {
        const tags = weakTagsResult.value;
        const avg = tags.reduce((sum, t) => sum + Number(t.officialPoint || 0), 0) / tags.length;
        competency = `${Math.round(avg * 10) / 10} / 10`;
      } else if (stats?.averageScore != null) {
        competency = `${Math.round(Number(stats.averageScore) * 10) / 10} / 10`;
      }

      setValues({
        competency,
        sessions: stats ? String(stats.totalSessions ?? 0) : '—',
        accuracy: stats ? `${Number(stats.accuracyPercent ?? 0).toFixed(1)}%` : '—',
        sessionsLast30Days: stats ? `${stats.sessionsLast30Days ?? 0} bài` : '—',
        streak:
          streakResult.status === 'fulfilled' ? `${streakResult.value.currentStreak ?? 0} Ngày` : '—',
        badges:
          badgesResult.status === 'fulfilled'
            ? String(badgesResult.value.filter((badge) => badge.isEarned).length)
            : '—',
      });
      setLoading(false);
    }

    load();
    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <section className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-6">
      {STAT_META.map((stat) => (
        <div
          key={stat.key}
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
              {values[stat.key]}
            </h3>
          </div>
        </div>
      ))}
    </section>
  );
}
