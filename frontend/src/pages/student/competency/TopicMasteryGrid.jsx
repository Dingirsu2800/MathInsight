/**
 * Topic mastery grid — one card per tag with score, status, and progress bar.
 * Data: recommenderApi.getAllTagsMastery() — returns ALL topics (not only weak ones).
 * UC-55 / RCM-17
 */
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import useCurrentUser from '../../../hooks/useCurrentUser';
import { getAllTagsMastery } from '../../../services/recommenderApi';

// Icon pool cycled per index (no icon field in DTO)
const ICON_POOL = [
  { name: 'function', bg: 'bg-primary-fixed', color: 'text-primary' },
  { name: 'change_history', bg: 'bg-error-container', color: 'text-error' },
  { name: 'rebase_edit', bg: 'bg-surface-container-high', color: 'text-on-surface-variant' },
  { name: 'data_exploration', bg: 'bg-primary-fixed', color: 'text-primary' },
  { name: 'calculate', bg: 'bg-tertiary-fixed', color: 'text-tertiary' },
  { name: 'schema', bg: 'bg-emerald-success/20', color: 'text-emerald-success' },
];

/**
 * Derive visual style from masteryStatus (server-authoritative) and numberDone.
 * masteryStatus: "NotLearned" | "Learning" | "Mastered"
 */
function getTopicStyle(masteryStatus, officialPoint) {
  if (masteryStatus === 'NotLearned') {
    return {
      status: 'Chưa làm',
      statusClass: 'bg-surface-container-high text-on-surface-variant',
      barColor: 'bg-surface-container-highest',
      badgeBorder: 'border-outline/30',
      badgeText: 'text-on-surface-variant',
      flagged: false,
      isUnpracticed: true,
    };
  }
  const score = Number(officialPoint);
  if (score < 5) {
    return {
      status: 'Cần cải thiện',
      statusClass: 'bg-error-container/30 text-error',
      barColor: 'bg-error',
      badgeBorder: 'border-error',
      badgeText: 'text-error',
      flagged: true,
      isUnpracticed: false,
    };
  }
  if (masteryStatus === 'Learning') {
    return {
      status: 'Đang học',
      statusClass: 'bg-surface-container-high text-on-surface-variant',
      barColor: 'bg-amber-warning',
      badgeBorder: 'border-amber-warning',
      badgeText: 'text-amber-warning',
      flagged: false,
      isUnpracticed: false,
    };
  }
  return {
    status: 'Thành thạo',
    statusClass: 'bg-emerald-success/20 text-emerald-success',
    barColor: 'bg-emerald-success',
    badgeBorder: 'border-emerald-success',
    badgeText: 'text-emerald-success',
    flagged: false,
    isUnpracticed: false,
  };
}

/**
 * Helper to identify whether a topic belongs to a specific grade.
 * Uses topic.grade if present; falls back to parsing topic.tagName.
 */
export function isTopicInGrade(topic, grade) {
  if (!grade) return true;
  if (topic?.grade) {
    return Number(topic.grade) === Number(grade);
  }
  const gradeStr = String(grade);
  const name = topic?.tagName || '';
  const regex = new RegExp(`(?:Lớp|Khối|K)\\s*${gradeStr}\\b`, 'i');
  return regex.test(name);
}

/**
 * Extract the grade number from topic.grade or topic.tagName.
 */
export function getTopicGrade(topic) {
  if (topic?.grade) return Number(topic.grade);
  const match = (topic?.tagName || '').match(/(?:Lớp|Khối|K)\s*(\d{1,2})\b/i);
  return match ? Number(match[1]) : null;
}

export default function TopicMasteryGrid() {
  const { profile } = useCurrentUser('Học sinh');
  const currentGrade = profile?.student?.currentGrade;

  const [topics, setTopics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [sortBy, setSortBy] = useState('progress'); // 'progress' | 'score'
  const [filterScope, setFilterScope] = useState('current'); // 'current' | 'all'

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    getAllTagsMastery()
      .then((data) => { if (!cancelled) setTopics(data || []); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, []);

  // When student has no currentGrade set, fall back to viewing all
  const effectiveScope = currentGrade ? filterScope : 'all';

  const filteredTopics = topics.filter((topic) => {
    if (effectiveScope === 'all') return true;
    return isTopicInGrade(topic, currentGrade);
  });

  // Sort
  const sorted = [...filteredTopics].sort((a, b) => {
    if (sortBy === 'score') return Number(b.officialPoint) - Number(a.officialPoint);
    // progress: flagged (weak) first, then ascending score
    const sa = Number(a.officialPoint);
    const sb = Number(b.officialPoint);
    if (sa < 5 && sb >= 5) return -1;
    if (sb < 5 && sa >= 5) return 1;
    return sa - sb;
  });

  return (
    <section>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4">
        <div className="flex items-center gap-3">
          <h3 className="text-lg font-semibold text-on-surface">Chi tiết từng chủ đề</h3>
          {!loading && !error && (
            <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-surface-container-high text-on-surface-variant">
              {sorted.length} chủ đề
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-3">
          {/* Grade filter pills */}
          <div className="inline-flex p-1 bg-surface-container-low rounded-xl border border-whisper-border">
            <button
              type="button"
              onClick={() => setFilterScope('current')}
              disabled={!currentGrade}
              title={!currentGrade ? 'Chưa cập nhật thông tin khối lớp' : `Chỉ hiển thị chủ đề Lớp ${currentGrade}`}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                effectiveScope === 'current'
                  ? 'bg-primary text-white shadow-sm'
                  : currentGrade
                  ? 'text-on-surface-variant hover:text-on-surface hover:bg-surface-container'
                  : 'text-outline/50 cursor-not-allowed'
              }`}
            >
              <MaterialIcon name="school" className="text-sm" />
              <span>{currentGrade ? `Lớp ${currentGrade}` : 'Lớp hiện tại'}</span>
            </button>

            <button
              type="button"
              onClick={() => setFilterScope('all')}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                effectiveScope === 'all'
                  ? 'bg-primary text-white shadow-sm'
                  : 'text-on-surface-variant hover:text-on-surface hover:bg-surface-container'
              }`}
            >
              <MaterialIcon name="apps" className="text-sm" />
              <span>Hiển thị toàn bộ</span>
            </button>
          </div>

          {/* Divider */}
          <div className="hidden sm:block h-5 w-[1px] bg-whisper-border" />

          {/* Sort buttons */}
          <div className="flex gap-1.5">
            <button
              type="button"
              onClick={() => setSortBy('progress')}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                sortBy === 'progress'
                  ? 'bg-primary text-white border-primary'
                  : 'bg-pure-surface border-whisper-border text-on-surface-variant hover:bg-surface-container'
              }`}
            >
              Theo tiến độ
            </button>
            <button
              type="button"
              onClick={() => setSortBy('score')}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                sortBy === 'score'
                  ? 'bg-primary text-white border-primary'
                  : 'bg-pure-surface border-whisper-border text-on-surface-variant hover:bg-surface-container'
              }`}
            >
              Theo điểm số
            </button>
          </div>
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

      {/* Empty - Overall */}
      {!loading && !error && topics.length === 0 && (
        <p className="text-sm text-outline text-center py-8">
          Chưa có dữ liệu chuyên đề nào.
        </p>
      )}

      {/* Empty - Filter result */}
      {!loading && !error && topics.length > 0 && sorted.length === 0 && (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-8 text-center">
          <div className="inline-flex p-3 rounded-full bg-surface-container mb-3 text-on-surface-variant">
            <MaterialIcon name="school" className="text-2xl" />
          </div>
          <h4 className="text-sm font-semibold text-on-surface mb-1">
            Không có chủ đề nào thuộc Lớp {currentGrade}
          </h4>
          <p className="text-xs text-outline mb-4">
            Hiện tại chưa có chủ đề tương ứng cho khối lớp của bạn.
          </p>
          <button
            type="button"
            onClick={() => setFilterScope('all')}
            className="px-4 py-2 bg-primary text-white text-xs font-medium rounded-lg hover:bg-primary-hover transition-colors"
          >
            Hiển thị toàn bộ chủ đề
          </button>
        </div>
      )}

      {/* Data */}
      {!loading && !error && sorted.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
          {sorted.map((topic, idx) => {
            const score = Number(topic.officialPoint || 0);
            const style = getTopicStyle(topic.masteryStatus, score);
            const icon = ICON_POOL[idx % ICON_POOL.length];
            const topicGrade = getTopicGrade(topic);

            const card = (
              <div
                className={`bg-pure-surface border rounded-xl p-5 flex flex-col relative overflow-hidden transition-transform hover:-translate-y-1 ${
                  style.flagged
                    ? 'border-2 border-deep-rose/30'
                    : 'border-whisper-border'
                }`}
              >
                <div className="flex justify-between items-start mb-4">
                  <div className={`p-2 rounded-lg ${icon.bg}`}>
                    <MaterialIcon name={icon.name} className={icon.color} />
                  </div>
                  <div className="flex items-center gap-1.5">
                    {topicGrade && (
                      <span className="text-[11px] px-2 py-0.5 rounded-md font-semibold bg-surface-container text-on-surface-variant border border-whisper-border">
                        Lớp {topicGrade}
                      </span>
                    )}
                    <span className={`text-[11px] px-2 py-0.5 rounded-full uppercase font-bold ${style.statusClass}`}>
                      {style.status}
                    </span>
                  </div>
                </div>

                <h4 className="text-base font-bold mb-1 text-on-surface">{topic.tagName}</h4>
                <p className="text-on-surface-variant text-sm mb-4">
                  {style.isUnpracticed ? 'Chưa làm bài tập/bài thi' : 'Năng lực chuyên đề'}
                </p>

                {/* Score + progress bar — only for tags with data */}
                {style.isUnpracticed ? (
                  <div className="mt-auto pt-2 border-t border-whisper-border">
                    <p className="text-xs text-on-surface-variant italic">Chưa có điểm năng lực</p>
                  </div>
                ) : (
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
                )}
              </div>
            );

            return (
              <Link
                key={topic.tagId}
                to="/student/test/topics"
                state={{ preselectedTagId: topic.tagId }}
                className="block group hover:scale-[1.01] transition-transform"
                aria-label={`Luyện tập chủ đề ${topic.tagName}`}
              >
                {card}
              </Link>
            );
          })}
        </div>
      )}
    </section>
  );
}
