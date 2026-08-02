import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getBadges } from '../../../services/gamificationApi';

export default function BadgeCarouselCard() {
  const [badges, setBadges] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      try {
        const data = await getBadges();
        const earned = (data || [])
          .filter((badge) => badge.isEarned)
          .sort((a, b) => new Date(b.earnedTime) - new Date(a.earnedTime))
          .slice(0, 4);
        if (isMounted) setBadges(earned);
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
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2 mb-6">
        <MaterialIcon name="workspace_premium" className="text-amber-warning" />
        Huy hiệu mới đạt được
      </h3>

      {loading ? (
        <p className="text-sm text-on-surface-variant">Đang tải...</p>
      ) : badges.length === 0 ? (
        <p className="text-sm text-on-surface-variant">Bạn chưa đạt huy hiệu nào. Hãy tiếp tục luyện tập!</p>
      ) : (
        <div className="flex gap-4 overflow-x-auto pb-2 scrollbar-none">
          {badges.map((badge) => (
            <div
              key={badge.badgeId}
              className="flex flex-col items-center gap-2 flex-shrink-0 group cursor-pointer"
            >
              <div className="w-16 h-16 rounded-2xl bg-primary flex items-center justify-center text-white shadow-md group-hover:scale-110 transition-transform overflow-hidden">
                {badge.iconUrl ? (
                  <img src={badge.iconUrl} alt={badge.badgeName} className="w-full h-full object-cover" />
                ) : (
                  <MaterialIcon name="workspace_premium" size={28} />
                )}
              </div>
              <p className="text-[11px] text-center text-on-surface-variant leading-tight font-medium">
                {badge.badgeName}
              </p>
            </div>
          ))}
        </div>
      )}

      <Link
        to="/student/achievements"
        className="mt-6 block text-center text-sm font-medium text-primary hover:underline"
      >
        Xem tất cả huy hiệu
      </Link>
    </div>
  );
}
