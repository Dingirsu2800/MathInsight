import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';
import { getWeakTags } from '../../../services/recommenderApi';
import { testGeneratorApi } from '../../../services/testGeneratorApi';

/** Map số difficulty → nhãn tiếng Việt (khớp với PracticeSetupPanel). */
const DIFFICULTY_LABEL = {
  1: 'Nhận biết',
  2: 'Thông hiểu',
  3: 'Vận dụng',
  4: 'Vận dụng cao',
};

/** Derive display style from officialPoint (0–10 scale). */
function getTopicStyle(score) {
  const s = Number(score);
  if (s < 5) {
    return {
      statusLabel: 'Cần cải thiện',
      statusClass: 'text-error bg-error-container',
      barColor: 'bg-error',
      cardClass: 'bg-error-container/20 border-error-container/50',
    };
  }
  if (s < 7.5) {
    return {
      statusLabel: 'Sắp hoàn thành',
      statusClass: 'text-tertiary bg-tertiary-fixed',
      barColor: 'bg-tertiary-fixed-dim',
      cardClass: 'bg-surface-container-low border-whisper-border',
    };
  }
  return {
    statusLabel: 'Đã ổn định',
    statusClass: 'text-emerald-success bg-emerald-success/20',
    barColor: 'bg-emerald-success',
    cardClass: 'bg-surface-container-low border-whisper-border',
  };
}

export default function WeakTopicsCard() {
  const [topics, setTopics] = useState([]);
  const [practiceOptions, setPracticeOptions] = useState({}); // tagId → { recommendedDifficultyLevel }
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);

    Promise.allSettled([
      getWeakTags(),
      testGeneratorApi.getTopicPracticeOptions(),
    ]).then(([weakTagsResult, practiceResult]) => {
      if (cancelled) return;

      if (weakTagsResult.status === 'fulfilled') {
        setTopics(weakTagsResult.value ?? []);
      } else {
        setError(true);
      }

      if (practiceResult.status === 'fulfilled') {
        const rawTopics = practiceResult.value?.data?.topics ?? [];
        const map = {};
        rawTopics.forEach((t) => {
          if (t?.tagId != null) {
            map[t.tagId] = {
              recommendedDifficultyLevel: Number.isInteger(Number(t.recommendedDifficultyLevel))
                ? Number(t.recommendedDifficultyLevel)
                : null,
              canGenerate: t.canGenerate === true,
            };
          }
        });
        setPracticeOptions(map);
      }

      setLoading(false);
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
        <div className="relative">
          {/* Scrollable list – hiển thị ~4 item, scroll để xem thêm */}
          <div
            className="space-y-4 overflow-y-auto pr-1"
            style={{ maxHeight: '420px' }}
          >
          {topics.map((topic) => {
            const score = Number(topic.officialPoint);
            const style = getTopicStyle(score);
            const practice = practiceOptions[topic.tagId];
            const difficultyLevel = practice?.recommendedDifficultyLevel ?? null;
            const difficultyLabel = difficultyLevel != null ? DIFFICULTY_LABEL[difficultyLevel] : null;

            // Gợi ý hiển thị theo độ khó practice hiện tại
            let hint = null;
            if (difficultyLabel) {
              hint = `Gợi ý: Luyện tập ở mức "${difficultyLabel}" để cải thiện chủ đề này.`;
            } else if (score < 5) {
              hint = 'Gợi ý: Ôn lại kiến thức nền và làm thêm bài tập mức "Thông hiểu".';
            }

            const card = (
              <div className={`p-4 rounded-xl border ${style.cardClass} transition-all`}>
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
                {hint && (
                  <p className="text-[11px] text-outline mt-2 italic">{hint}</p>
                )}
                {/* Nhãn độ khó nếu có */}
                {difficultyLabel && (
                  <div className="flex items-center gap-1 mt-2">
                    <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-primary/10 text-primary border border-primary/20">
                      Mức: {difficultyLabel}
                    </span>
                    <span className="text-[10px] text-on-surface-variant">
                      — Bấm để luyện ngay
                    </span>
                  </div>
                )}
              </div>
            );

            // Nếu có thể tạo bài practice → wrap bằng Link
            if (practice?.canGenerate !== false) {
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
            }

            return <div key={topic.tagId}>{card}</div>;
          })}
          </div>

          {/* Gradient fade – chỉ hiển thị khi có nhiều hơn 4 tag */}
          {topics.length > 4 && (
            <div
              className="pointer-events-none absolute bottom-0 left-0 right-0 h-12 rounded-b-xl"
              style={{
                background: 'linear-gradient(to bottom, transparent, var(--color-pure-surface, #fff))',
              }}
            />
          )}
        </div>
      )}
    </div>
  );
}
