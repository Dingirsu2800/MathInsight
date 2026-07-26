import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getSessionHistory } from '../../../services/gradingApi';

function formatRelativeTime(isoString) {
  if (!isoString) return '';
  const now = new Date();
  const date = new Date(isoString);
  const diffMs = Math.max(0, now - date);
  const diffMinutes = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMinutes < 1) return 'Vừa xong';
  if (diffMinutes < 60) return `${diffMinutes} phút trước`;
  if (diffHours < 24) return `${diffHours} giờ trước`;
  if (diffDays === 1) return 'Hôm qua';
  if (diffDays < 7) return `${diffDays} ngày trước`;
  return date.toLocaleDateString('vi-VN');
}

export default function RecentActivityCard() {
  const navigate = useNavigate();
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);

    getSessionHistory({ pageIndex: 1, pageSize: 4 })
      .then((data) => {
        if (!cancelled) {
          setActivities(data.items || []);
        }
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2">
          <MaterialIcon name="schedule" className="text-outline" />
          Hoạt động gần đây
        </h3>
        <button
          onClick={() => navigate('/student/test-history')}
          className="text-xs font-bold text-primary hover:underline"
        >
          Xem tất cả
        </button>
      </div>

      {loading && (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="flex items-center gap-3 animate-pulse">
              <div className="w-10 h-10 rounded-xl bg-surface-container flex-shrink-0" />
              <div className="flex-1 space-y-2">
                <div className="h-3 bg-surface-container rounded w-3/4" />
                <div className="h-2 bg-surface-container rounded w-1/2" />
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && error && (
        <p className="text-sm text-outline text-center py-6">
          Không thể tải lịch sử hoạt động. Vui lòng thử lại sau.
        </p>
      )}

      {!loading && !error && activities.length === 0 && (
        <p className="text-sm text-outline text-center py-6">
          Chưa có hoạt động làm bài nào gần đây.
        </p>
      )}

      {!loading && !error && activities.length > 0 && (
        <div className="space-y-3">
          {activities.map((item) => {
            const isExam = item.testFormat === 'Exam';
            const icon = isExam ? 'assignment' : 'edit_square';
            const iconClass = isExam ? 'text-tertiary bg-tertiary-fixed' : 'text-primary bg-primary-fixed';
            const title = isExam ? 'Hoàn thành bài kiểm tra' : 'Hoàn thành bài luyện tập';
            const timeAgo = formatRelativeTime(item.submittedAt);
            const scoreStr = item.score != null ? `${Number(item.score).toFixed(1)}/10` : 'Đã nộp';

            return (
              <div
                key={item.sessionId}
                onClick={() => navigate(`/student/test-result/${item.sessionId}`)}
                className="flex items-start gap-3 group cursor-pointer hover:bg-surface-container-low/60 p-2.5 rounded-xl -mx-2 transition-all"
              >
                <div
                  className={`w-10 h-10 rounded-xl flex-shrink-0 flex items-center justify-center ${iconClass}`}
                >
                  <MaterialIcon name={icon} size={20} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-on-surface font-semibold group-hover:text-primary transition-colors truncate">
                    {title}
                  </p>
                  <div className="flex items-center gap-3 mt-1 text-xs text-outline font-mono">
                    <span className="flex items-center gap-1 font-bold text-on-surface">
                      <MaterialIcon name="grade" size={14} className="text-amber-warning" />
                      {scoreStr}
                    </span>
                    <span>•</span>
                    <span>{item.numCorrect}/{item.totalQuestion} câu đúng</span>
                  </div>
                </div>
                <span className="text-[11px] text-outline whitespace-nowrap flex-shrink-0 mt-0.5">
                  {timeAgo}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
