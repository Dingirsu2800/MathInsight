import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import { getWeakTags } from '../../../services/recommenderApi';

/** Derive display style from officialPoint (0–10 scale). */
function getTopicStyle(score) {
  const s = Number(score);
  if (s < 5) {
    return {
      statusLabel: 'Cần cải thiện',
      statusClass: 'text-error bg-error-container',
      barColor: 'bg-error',
      cardClass: 'bg-error-container/20 border-error-container/50',
      hint: 'Gợi ý: Ôn lại kiến thức nền và làm thêm bài tập mức "Trung bình".',
    };
  }
  if (s < 7.5) {
    return {
      statusLabel: 'Sắp hoàn thành',
      statusClass: 'text-tertiary bg-tertiary-fixed',
      barColor: 'bg-tertiary-fixed-dim',
      cardClass: 'bg-surface-container-low border-whisper-border',
      hint: null,
    };
  }
  return {
    statusLabel: 'Đã ổn định',
    statusClass: 'text-emerald-success bg-emerald-success/20',
    barColor: 'bg-emerald-success',
    cardClass: 'bg-surface-container-low border-whisper-border',
    hint: null,
  };
}

export default function WeakTopicsCard() {
  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);

    getWeakTags()
      .then((data) => {
        if (!cancelled) setTopics(data);
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
          <MaterialIcon name="trending_down" className="text-deep-rose" />
          Kiến thức cần củng cố
        </h3>
        <a className="text-primary text-xs font-bold hover:underline" href="/student/competency">
          Chi tiết
        </a>
      </div>

      {/* Loading skeleton */}
      {loading && (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="p-4 rounded-xl border border-whisper-border bg-surface-container-low animate-pulse">
              <div className="h-3 bg-surface-container-high rounded w-3/4 mb-3" />
              <div className="h-2 bg-surface-container-high rounded w-full" />
            </div>
          ))}
        </div>
      )}

      {/* Error state */}
      {!loading && error && (
        <p className="text-sm text-outline text-center py-6">
          Không thể tải dữ liệu. Vui lòng thử lại sau.
        </p>
      )}

      {/* Empty state */}
      {!loading && !error && topics.length === 0 && (
        <p className="text-sm text-outline text-center py-6">
          Tuyệt vời! Bạn chưa có chủ đề nào cần cải thiện.
        </p>
      )}

      {/* Data */}
      {!loading && !error && topics.length > 0 && (
        <div className="space-y-4">
          {topics.map((topic) => {
            const score = Number(topic.officialPoint);
            const style = getTopicStyle(score);
            return (
              <div
                key={topic.tagId}
                className={`p-4 rounded-xl border ${style.cardClass}`}
              >
                <div className="flex justify-between items-center mb-2">
                  <span className="text-sm text-on-surface font-bold">{topic.tagName}</span>
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded ${style.statusClass}`}>
                    {style.statusLabel}
                  </span>
                </div>
                <ProgressBar
                  value={score}
                  max={10}
                  colorClass={style.barColor}
                  trackClass="bg-surface-container-high"
                />
                {style.hint && (
                  <p className="text-[11px] text-outline mt-2 italic">{style.hint}</p>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
