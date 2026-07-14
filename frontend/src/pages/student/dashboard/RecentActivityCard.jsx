import MaterialIcon from '../../../components/ui/MaterialIcon';

// TODO: Replace with API data from recent activity endpoint
const MOCK_ACTIVITIES = [
  {
    id: 1,
    icon: 'edit_square',
    iconClass: 'text-primary bg-primary-fixed',
    title: 'Hoàn thành bài luyện tập:',
    highlight: 'Khối đa diện',
    meta: ['8/10', '12:45'],
    metaIcons: ['data_usage', 'schedule'],
    time: '15 phút trước',
  },
  {
    id: 2,
    icon: 'smart_display',
    iconClass: 'text-tertiary bg-tertiary-fixed',
    title: 'Đã xem bài giảng:',
    highlight: 'Phương pháp tọa độ trong không gian',
    meta: ['Đã hoàn thành 100% thời lượng video.'],
    metaIcons: [],
    time: '2 giờ trước',
  },
  {
    id: 3,
    icon: 'emoji_events',
    iconClass: 'text-amber-warning bg-amber-warning/20',
    title: 'Mở khóa huy hiệu:',
    highlight: '"Cú đêm Toán học"',
    meta: [],
    metaIcons: [],
    time: 'Hôm qua',
  },
];

export default function RecentActivityCard() {
  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2">
          <MaterialIcon name="schedule" className="text-outline" />
          Hoạt động gần đây
        </h3>
        <button className="p-1 rounded-lg hover:bg-surface-container transition-colors">
          <MaterialIcon name="more_horiz" className="text-outline" />
        </button>
      </div>

      <div className="space-y-4">
        {MOCK_ACTIVITIES.map((activity) => (
          <div
            key={activity.id}
            className="flex items-start gap-3 group hover:bg-surface-container-low/50 p-2 rounded-lg -mx-2 transition-colors"
          >
            <div
              className={`w-10 h-10 rounded-xl flex-shrink-0 flex items-center justify-center ${activity.iconClass}`}
            >
              <MaterialIcon name={activity.icon} size={20} />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm text-on-surface">
                {activity.title}{' '}
                <span className="font-bold text-on-surface">{activity.highlight}</span>
              </p>
              {activity.meta.length > 0 && (
                <div className="flex items-center gap-4 mt-1 text-xs text-outline">
                  {activity.meta.map((m, i) => (
                    <span key={i} className="flex items-center gap-1">
                      {activity.metaIcons[i] && (
                        <MaterialIcon name={activity.metaIcons[i]} size={14} className="text-outline" />
                      )}
                      {m}
                    </span>
                  ))}
                </div>
              )}
            </div>
            <span className="text-[11px] text-outline whitespace-nowrap flex-shrink-0 mt-1">
              {activity.time}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
