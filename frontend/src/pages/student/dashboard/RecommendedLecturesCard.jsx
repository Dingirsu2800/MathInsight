import { useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getRecommendedLectures, getRecommenderErrorMessage } from '../../../services/recommenderApi';

function getRecommendationExplanation(lecture) {
  if (lecture.isDifficultyFallback || (lecture.reason && lecture.reason.includes('LowerDifficultyFallback'))) {
    return `Bài giảng nền tảng để ôn lại trước mức ${lecture.targetDifficultyLevel}.`;
  }
  switch (lecture.reason) {
    case 'WeakTopicExactDifficulty':
      return 'Đề xuất vì bạn đang cần củng cố chủ đề này.';
    case 'ProgressionExactDifficulty':
      return 'Mức học tiếp theo phù hợp với tiến độ hiện tại của bạn.';
    case 'ColdStartGradeFoundation':
      return 'Bài giảng khởi đầu phù hợp với khối lớp của bạn.';
    default:
      return lecture.reason || 'Bài giảng được đề xuất cho bạn.';
  }
}

export default function RecommendedLecturesCard({ layoutVariant = 'dashboard' }) {
  const [lectures, setLectures] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const fetchRecommendations = useCallback(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);
    setErrorMessage('');

    getRecommendedLectures()
      .then((data) => {
        if (!cancelled) {
          setLectures(Array.isArray(data) ? data : []);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(true);
          setErrorMessage(getRecommenderErrorMessage(err));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const cleanup = fetchRecommendations();
    return cleanup;
  }, [fetchRecommendations]);

  const isLibrary = layoutVariant === 'library';
  const containerClass = isLibrary 
    ? "relative bg-gradient-to-br from-primary/10 via-surface to-surface border border-primary/20 rounded-3xl p-6 lg:p-8 shadow-sm overflow-hidden"
    : "bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm";
    
  const gridClass = isLibrary
    ? "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 relative z-10"
    : "grid grid-cols-1 md:grid-cols-2 gap-4";

  return (
    <div className={containerClass}>
      {isLibrary && (
        <div className="absolute -top-24 -right-24 w-64 h-64 bg-primary/20 rounded-full blur-3xl pointer-events-none" />
      )}
      <h3 className="text-[20px] font-bold text-on-surface flex items-center gap-2 mb-6 relative z-10">
        <span className="flex items-center justify-center w-8 h-8 rounded-full bg-primary/20 text-primary">
          <MaterialIcon name="auto_awesome" size={20} />
        </span>
        Bài giảng đề xuất dành riêng cho bạn
      </h3>

      {loading && (
        <div className={gridClass}>
          {[1, 2].map((i) => (
            <div key={i} className="rounded-xl border border-whisper-border overflow-hidden animate-pulse">
              <div className="w-full h-[180px] bg-surface-container" />
              <div className="p-3 space-y-2">
                <div className="h-3 bg-surface-container-high rounded w-3/4" />
                <div className="h-2 bg-surface-container-high rounded w-1/2" />
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Technical error state with retry button */}
      {!loading && error && (
        <div className="text-center py-6 space-y-3">
          <p className="text-sm text-outline">
            {errorMessage || 'Không thể tải bài giảng đề xuất. Vui lòng thử lại sau.'}
          </p>
          <button
            type="button"
            onClick={fetchRecommendations}
            className="px-4 py-1.5 bg-primary/10 text-primary hover:bg-primary/20 rounded-lg text-xs font-bold transition-colors focus-visible:ring-2 focus-visible:ring-primary focus-visible:outline-none inline-flex items-center gap-1.5"
          >
            <MaterialIcon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {/* Empty state */}
      {!loading && !error && lectures.length === 0 && (
        <p className="text-sm text-outline text-center py-6">
          Chưa có bài giảng đề xuất nào dành cho bạn.
        </p>
      )}

      {/* Data Grid */}
      {!loading && !error && lectures.length > 0 && (
        <div className={gridClass}>
          {lectures.map((lecture) => {
            const explanation = getRecommendationExplanation(lecture);
            const isColdStart = lecture.reason === 'ColdStartGradeFoundation';
            const isRemedial = lecture.reason?.startsWith('WeakTopic') === true;
            const chipColor = isRemedial ? 'bg-deep-rose' : 'bg-primary';
            const chipLabel = isRemedial ? `Phụ đạo: ${lecture.tagName}` : lecture.tagName;

            const difficultyMeta = {
              1: { label: 'Cơ bản',   color: 'bg-emerald-500' },
              2: { label: 'Trung bình', color: 'bg-amber-500'  },
              3: { label: 'Khá',       color: 'bg-orange-500'  },
              4: { label: 'Nâng cao',  color: 'bg-red-500'     },
            };
            const diff = difficultyMeta[lecture.difficultyLevel] ?? { label: `Mức ${lecture.difficultyLevel}`, color: 'bg-outline' };

            return (
              <Link
                key={lecture.lectureId}
                to={`/student/lectures/${lecture.lectureId}`}
                className="group block rounded-xl overflow-hidden border border-whisper-border hover:border-primary/30 focus-visible:ring-2 focus-visible:ring-primary focus-visible:outline-none transition-all bg-pure-surface flex flex-col justify-between"
              >
                <div>
                  {/* Thumbnail / Header Area */}
                  <div className="relative w-full h-[180px] bg-surface-container overflow-hidden">
                    {lecture.thumbnailUrl ? (
                      <img
                        src={lecture.thumbnailUrl}
                        alt={lecture.title}
                        className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                      />
                    ) : (
                      <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-primary-container/30">
                        <MaterialIcon name="play_circle" size={48} className="text-primary/40" />
                      </div>
                    )}

                    {/* Topic & Difficulty Chips */}
                    <div className="absolute top-3 left-3 flex flex-wrap gap-1.5 max-w-[85%]">
                      <span className={`${chipColor} text-white text-[10px] font-bold px-2 py-0.5 rounded shadow-sm`}>
                        {chipLabel}
                      </span>
                      {lecture.difficultyName && (
                        <span className="bg-surface-container-highest/90 text-on-surface text-[10px] font-bold px-2 py-0.5 rounded backdrop-blur-sm shadow-sm border border-whisper-border">
                          {lecture.difficultyName}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Body Content */}
                  <div className="p-3.5 space-y-2">
                    <h4 className="text-sm font-bold text-on-surface line-clamp-2 group-hover:text-primary transition-colors">
                      {lecture.title}
                    </h4>

                    {/* Explanation */}
                    <p className="text-xs text-outline leading-relaxed flex items-start gap-1">
                      <MaterialIcon name="info" size={14} className="text-primary/70 shrink-0 mt-0.5" />
                      <span>{explanation}</span>
                    </p>
                  </div>
                  <div className={`absolute top-3 right-3 ${diff.color} text-white text-[10px] font-bold px-2.5 py-1 rounded`}>
                    {diff.label}
                  </div>
                </div>

                {/* Footer Score (Mastery-based only) */}
                <div className="px-3.5 pb-3.5 pt-1 border-t border-whisper-border/50 flex items-center justify-between text-[11px] text-on-surface-variant">
                  {lecture.officialPoint != null && !isColdStart ? (
                    <span className="font-semibold text-primary">
                      Điểm chủ đề: {Number(lecture.officialPoint).toFixed(1)}/10
                    </span>
                  ) : (
                    <span className="italic text-outline">
                      Bài giảng nền tảng
                    </span>
                  )}
                  {lecture.likes > 0 && (
                    <span className="flex items-center gap-1 font-medium">
                      <MaterialIcon name="favorite" size={12} className="text-deep-rose" />
                      {lecture.likes}
                    </span>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
