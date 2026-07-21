/**
 * Topic mastery grid — one card per tag with score, status, and progress bar.
 * Data: recommenderApi.getWeakTags() replaces the old MOCK_TOPICS array.
 */
import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import { getWeakTags } from '../../../services/recommenderApi';

// Icon pool cycled per index (no icon field in DTO)
const ICON_POOL = [
  { name: 'function', bg: 'bg-primary-fixed', color: 'text-primary' },
  { name: 'change_history', bg: 'bg-error-container', color: 'text-error' },
  { name: 'rebase_edit', bg: 'bg-surface-container-high', color: 'text-on-surface-variant' },
  { name: 'data_exploration', bg: 'bg-primary-fixed', color: 'text-primary' },
  { name: 'calculate', bg: 'bg-tertiary-fixed', color: 'text-tertiary' },
  { name: 'schema', bg: 'bg-emerald-success/20', color: 'text-emerald-success' },
];

/** Derive visual style from officialPoint (0–10) */
function getTopicStyle(score) {
  if (score < 5) {
    return {
      status: 'Cần cải thiện',
      statusClass: 'bg-error-container/30 text-error',
      barColor: 'bg-error',
      badgeBorder: 'border-error',
      badgeText: 'text-error',
      flagged: true,
    };
  }
  if (score < 7.5) {
    return {
      status: 'Đang học',
      statusClass: 'bg-surface-container-high text-on-surface-variant',
      barColor: 'bg-amber-warning',
      badgeBorder: 'border-amber-warning',
      badgeText: 'text-amber-warning',
      flagged: false,
    };
  }
  return {
    status: 'Thành thạo',
    statusClass: 'bg-emerald-success/20 text-emerald-success',
    barColor: 'bg-emerald-success',
    badgeBorder: 'border-emerald-success',
    badgeText: 'text-emerald-success',
    flagged: false,
  };
}

export default function TopicMasteryGrid() {
  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [sortBy, setSortBy] = useState('progress'); // 'progress' | 'score'

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    getWeakTags()
      .then((data) => { if (!cancelled) setTopics(data || []); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, []);

  // Sort
  const sorted = [...topics].sort((a, b) => {
    if (sortBy === 'score') return Number(b.officialPoint) - Number(a.officialPoint);
    // progress: flagged first, then ascending score
    const sa = Number(a.officialPoint);
    const sb = Number(b.officialPoint);
    if (sa < 5 && sb >= 5) return -1;
    if (sb < 5 && sa >= 5) return 1;
    return sa - sb;
  });

  return (
    <section>
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-on-surface">Chi tiết từng chuyên đề</h3>
        <div className="flex gap-2">
          <button
            onClick={() => setSortBy('progress')}
            className={`px-4 py-2 rounded-lg font-mono text-xs border transition-colors ${
              sortBy === 'progress'
                ? 'bg-primary text-white border-primary'
                : 'bg-pure-surface border-whisper-border hover:bg-surface-container'
            }`}
          >
            Theo tiến độ
          </button>
          <button
            onClick={() => setSortBy('score')}
            className={`px-4 py-2 rounded-lg font-mono text-xs border transition-colors ${
              sortBy === 'score'
                ? 'bg-primary text-white border-primary'
                : 'bg-pure-surface border-whisper-border hover:bg-surface-container'
            }`}
          >
            Theo điểm số
          </button>
        </div>
      </div>

      {/* Loading skeleton */}
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="bg-pure-surface border border-whisper-border rounded-xl p-5 animate-pulse">
              <div className="h-10 w-10 bg-surface-container rounded-lg mb-4" />
              <div className="h-4 bg-surface-container rounded w-3/4 mb-2" />
              <div className="h-3 bg-surface-container rounded w-1/2 mb-4" />
              <div className="h-2 bg-surface-container rounded w-full" />
            </div>
          ))}
        </div>
      )}

      {/* Error */}
      {!loading && error && (
        <p className="text-sm text-outline text-center py-8">
          Không thể tải dữ liệu chuyên đề. Vui lòng thử lại.
        </p>
      )}

      {/* Empty */}
      {!loading && !error && topics.length === 0 && (
        <p className="text-sm text-outline text-center py-8">
          Chưa có dữ liệu chuyên đề nào.
        </p>
      )}

      {/* Data */}
      {!loading && !error && topics.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
          {sorted.map((topic, idx) => {
            const score = Number(topic.officialPoint || 0);
            const style = getTopicStyle(score);
            const icon = ICON_POOL[idx % ICON_POOL.length];

            return (
              <div
                key={topic.tagId}
                className={`bg-pure-surface border rounded-xl p-5 flex flex-col relative overflow-hidden transition-transform hover:-translate-y-1 ${
                  style.flagged
                    ? 'border-2 border-deep-rose/30'
                    : 'border-whisper-border'
                }`}
              >
                {/* Flag badge */}
                {style.flagged && (
                  <div className="absolute top-0 right-0">
                    <div className="bg-deep-rose text-white text-[10px] font-bold px-3 py-1 rounded-bl-xl shadow-sm flex items-center gap-1">
                      <MaterialIcon name="priority_high" size={12} filled />
                      CẦN PHỤ ĐẠO
                    </div>
                  </div>
                )}

                <div className="flex justify-between items-start mb-4">
                  <div className={`p-2 rounded-lg ${icon.bg}`}>
                    <MaterialIcon name={icon.name} className={icon.color} />
                  </div>
                  <span className={`text-[11px] px-2 py-0.5 rounded-full uppercase font-bold ${style.statusClass}`}>
                    {style.status}
                  </span>
                </div>

                <h4 className="text-base font-bold mb-1 text-on-surface">{topic.tagName}</h4>
                <p className="text-on-surface-variant text-sm mb-4">Năng lực chuyên đề</p>

                <div className="flex items-center gap-4 mt-auto">
                  <div className="flex-1">
                    <div className="flex justify-between font-mono text-xs mb-1">
                      <span>Năng lực</span>
                      <span className={`font-bold ${style.badgeText}`}>{score}/10</span>
                    </div>
                    <ProgressBar
                      value={score}
                      max={10}
                      height="h-2"
                      colorClass={style.barColor}
                      trackClass="bg-surface-container"
                    />
                  </div>
                  <div className={`w-10 h-10 border-2 ${style.badgeBorder} rounded-full flex items-center justify-center`}>
                    <span className={`font-mono text-xs ${style.badgeText}`}>
                      {Math.round(score * 10)}%
                    </span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
