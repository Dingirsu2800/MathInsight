import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import { getTargets } from '../../../services/gamificationApi';

export default function WeeklyTargetsCard() {
  const [targets, setTargets] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      try {
        const data = await getTargets();
        if (isMounted) setTargets(data || []);
      } catch {
        if (isMounted) setTargets([]);
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
        <MaterialIcon name="flag" className="text-primary" />
        Mục tiêu điểm số
      </h3>

      {loading ? (
        <p className="text-sm text-on-surface-variant">Đang tải...</p>
      ) : targets.length === 0 ? (
        <p className="text-sm text-on-surface-variant">Bạn chưa đặt mục tiêu nào.</p>
      ) : (
        <div className="space-y-5">
          {targets.slice(0, 3).map((target) => (
            <div key={target.targetId} className="flex items-center gap-3">
              <div className="w-9 h-9 rounded-lg bg-surface-container-low flex items-center justify-center flex-shrink-0">
                <MaterialIcon name={target.isAchieved ? 'check_circle' : 'flag'} size={20} className="text-primary" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex justify-between text-sm mb-1.5">
                  <span className="text-on-surface font-medium truncate">{target.tagName}</span>
                  <span className="text-primary font-bold">
                    {target.currentPoint}/{target.targetPoint}
                  </span>
                </div>
                <ProgressBar
                  value={target.currentPoint}
                  max={target.targetPoint}
                  height="h-1.5"
                  colorClass="bg-primary"
                />
              </div>
            </div>
          ))}
        </div>
      )}

      <Link
        to="/student/targets"
        className="mt-6 w-full py-2.5 border border-whisper-border text-on-surface-variant text-sm font-medium rounded-lg hover:bg-surface-container-low transition-colors flex items-center justify-center gap-2"
      >
        <MaterialIcon name="add" size={16} />
        Quản lý mục tiêu
      </Link>
    </div>
  );
}
