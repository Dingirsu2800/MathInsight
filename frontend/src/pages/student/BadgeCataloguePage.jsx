import { useEffect, useState } from 'react';
import StudentLayout from '../../components/layout/StudentLayout';
import MaterialIcon from '../../components/ui/MaterialIcon';
import ProgressBar from '../../components/ui/ProgressBar';
import { getBadges, getBadgeProgress } from '../../services/gamificationApi';

function formatDate(isoDate) {
  if (!isoDate) return '';
  return new Date(isoDate).toLocaleDateString('vi-VN');
}

export default function BadgeCataloguePage() {
  const [badges, setBadges] = useState([]);
  const [progressByBadgeId, setProgressByBadgeId] = useState({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      try {
        const [badgeList, progressList] = await Promise.all([getBadges(), getBadgeProgress()]);
        if (!isMounted) return;

        setBadges(badgeList || []);
        setProgressByBadgeId(
          Object.fromEntries((progressList || []).map((item) => [item.badgeId, item]))
        );
      } catch {
        if (isMounted) setBadges([]);
      } finally {
        if (isMounted) setLoading(false);
      }
    }

    load();
    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <StudentLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-on-surface flex items-center gap-2">
            <MaterialIcon name="workspace_premium" className="text-amber-warning" />
            Bộ sưu tập huy hiệu
          </h1>
          <p className="text-on-surface-variant text-sm mt-1">
            Hoàn thành các mục tiêu học tập để mở khóa huy hiệu mới.
          </p>
        </div>

        {loading ? (
          <p className="text-sm text-on-surface-variant">Đang tải...</p>
        ) : badges.length === 0 ? (
          <p className="text-sm text-on-surface-variant">Chưa có huy hiệu nào trong hệ thống.</p>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
            {badges.map((badge) => {
              const progress = progressByBadgeId[badge.badgeId];

              return (
                <div
                  key={badge.badgeId}
                  className={`bg-pure-surface border border-whisper-border rounded-2xl p-5 flex flex-col items-center text-center gap-3 ${
                    badge.isEarned ? '' : 'opacity-60'
                  }`}
                >
                  <div
                    className={`w-16 h-16 rounded-2xl flex items-center justify-center text-white shadow-md overflow-hidden ${
                      badge.isEarned ? 'bg-primary' : 'bg-surface-container'
                    }`}
                  >
                    {badge.iconUrl ? (
                      <img src={badge.iconUrl} alt={badge.badgeName} className="w-full h-full object-cover" />
                    ) : (
                      <MaterialIcon
                        name={badge.isEarned ? 'workspace_premium' : 'lock'}
                        size={28}
                        className={badge.isEarned ? '' : 'text-on-surface-variant'}
                      />
                    )}
                  </div>

                  <div>
                    <p className="font-semibold text-on-surface">{badge.badgeName}</p>
                    {badge.description && (
                      <p className="text-xs text-on-surface-variant mt-1">{badge.description}</p>
                    )}
                  </div>

                  {badge.isEarned ? (
                    <span className="text-xs text-emerald-success font-medium">
                      Đạt được ngày {formatDate(badge.earnedTime)}
                    </span>
                  ) : progress ? (
                    <div className="w-full">
                      <ProgressBar value={progress.currentValue} max={progress.requiredValue} height="h-1.5" />
                      <p className="text-xs text-on-surface-variant mt-1">
                        {progress.currentValue}/{progress.requiredValue} ({Math.round(progress.progressPercentage)}%)
                      </p>
                    </div>
                  ) : (
                    <span className="text-xs text-on-surface-variant">Chưa mở khóa</span>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </StudentLayout>
  );
}
