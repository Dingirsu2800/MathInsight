import MaterialIcon from '../../../components/ui/MaterialIcon';

// TODO: Replace with API data from /gamification/recent-badges
const MOCK_BADGES = [
  { id: 1, name: 'Thiên tài\nTinh nhẩm', icon: 'calculate', color: 'bg-primary' },
  { id: 2, name: 'Thần tốc\nHoàn thành', icon: 'timer', color: 'bg-emerald-success' },
  { id: 3, name: 'Kiên trì\n7 ngày', icon: 'local_fire_department', color: 'bg-deep-rose' },
  { id: 4, name: 'Đỉnh cao\nGiải tích', icon: 'function', color: 'bg-tertiary' },
];

export default function BadgeCarouselCard() {
  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2 mb-6">
        <MaterialIcon name="workspace_premium" className="text-amber-warning" />
        Huy hiệu mới đạt được
      </h3>

      <div className="flex gap-4 overflow-x-auto pb-2 scrollbar-none">
        {MOCK_BADGES.map((badge) => (
          <div
            key={badge.id}
            className="flex flex-col items-center gap-2 flex-shrink-0 group cursor-pointer"
          >
            <div
              className={`w-16 h-16 rounded-2xl ${badge.color} flex items-center justify-center text-white shadow-md group-hover:scale-110 transition-transform`}
            >
              <MaterialIcon name={badge.icon} size={28} />
            </div>
            <p className="text-[11px] text-center text-on-surface-variant leading-tight font-medium whitespace-pre-line">
              {badge.name}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
