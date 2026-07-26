import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getStreak, getBadges } from '../../../services/gamificationApi';
import { getStudentHistoryStats } from '../../../services/gradingApi';

const STAT_META = [
  { key: 'competency', label: 'Điểm năng lực', icon: 'insights', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { key: 'sessions', label: 'Bài đã làm', icon: 'history_edu', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { key: 'streak', label: 'Chuỗi ngày 🔥', icon: 'local_fire_department', colorClass: 'text-deep-rose', hoverBg: 'group-hover:bg-deep-rose' },
  { key: 'badges', label: 'Huy hiệu 🏅', icon: 'workspace_premium', colorClass: 'text-emerald-success', hoverBg: 'group-hover:bg-emerald-success' },
];

export default function StatCards() {
  const [values, setValues] = useState({ competency: '—', sessions: '—', streak: '—', badges: '—' });

  useEffect(() => {
    let isMounted = true;

    async function load() {
      const [streakResult, badgesResult, statsResult] = await Promise.allSettled([
        getStreak(),
        getBadges(),
        getStudentHistoryStats(),
      ]);

      if (!isMounted) return;

      setValues({
        competency:
          statsResult.status === 'fulfilled' ? `${statsResult.value.averageScore ?? 0} / 10` : '—',
        sessions:
          statsResult.status === 'fulfilled' ? String(statsResult.value.totalSessions ?? 0) : '—',
        streak:
          streakResult.status === 'fulfilled' ? `${streakResult.value.currentStreak ?? 0} Ngày` : '—',
        badges:
          badgesResult.status === 'fulfilled'
            ? String(badgesResult.value.filter((badge) => badge.isEarned).length)
            : '—',
      });
    }

    load();
    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {STAT_META.map((stat) => (
        <div
          key={stat.key}
          className="bg-pure-surface border border-whisper-border p-5 rounded-2xl flex items-center gap-4 group hover:border-primary/30 transition-all cursor-default"
        >
          <div
            className={`w-12 h-12 rounded-xl bg-surface-container-low flex items-center justify-center ${stat.colorClass} ${stat.hoverBg} group-hover:text-white transition-colors`}
          >
            <MaterialIcon name={stat.icon} filled />
          </div>
          <div>
            <p className="text-outline text-xs font-medium">{stat.label}</p>
            <h3 className="text-xl font-semibold text-on-surface">{values[stat.key]}</h3>
          </div>
        </div>
      ))}
    </section>
  );
}
