import MaterialIcon from '../../../components/ui/MaterialIcon';

// TODO: Replace with API data
const MOCK_STATS = [
  { label: 'Điểm năng lực', value: '6.8 / 10', icon: 'insights', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { label: 'Bài đã làm', value: '1,248', icon: 'history_edu', colorClass: 'text-primary', hoverBg: 'group-hover:bg-primary' },
  { label: 'Chuỗi ngày 🔥', value: '12 Ngày', icon: 'local_fire_department', colorClass: 'text-deep-rose', hoverBg: 'group-hover:bg-deep-rose' },
  { label: 'Huy hiệu 🏅', value: '24', icon: 'workspace_premium', colorClass: 'text-emerald-success', hoverBg: 'group-hover:bg-emerald-success' },
];

export default function StatCards() {
  return (
    <section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {MOCK_STATS.map((stat) => (
        <div
          key={stat.label}
          className="bg-pure-surface border border-whisper-border p-5 rounded-2xl flex items-center gap-4 group hover:border-primary/30 transition-all cursor-default"
        >
          <div
            className={`w-12 h-12 rounded-xl bg-surface-container-low flex items-center justify-center ${stat.colorClass} ${stat.hoverBg} group-hover:text-white transition-colors`}
          >
            <MaterialIcon name={stat.icon} filled />
          </div>
          <div>
            <p className="text-outline text-xs font-medium">{stat.label}</p>
            <h3 className="text-xl font-semibold text-on-surface">{stat.value}</h3>
          </div>
        </div>
      ))}
    </section>
  );
}
