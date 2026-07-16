import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getRecommendedLectures } from '../../../services/recommenderApi';

export default function RecommendedLecturesCard() {
  const [lectures, setLectures] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);

    getRecommendedLectures()
      .then((data) => {
        if (!cancelled) setLectures(data);
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
      <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2 mb-6">
        <MaterialIcon name="auto_awesome" className="text-primary" />
        Bài giảng đề xuất riêng cho bạn
      </h3>

      {/* Loading skeleton */}
      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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

      {/* Error state */}
      {!loading && error && (
        <p className="text-sm text-outline text-center py-6">
          Không thể tải bài giảng đề xuất. Vui lòng thử lại sau.
        </p>
      )}

      {/* Empty state */}
      {!loading && !error && lectures.length === 0 && (
        <p className="text-sm text-outline text-center py-6">
          Chưa có bài giảng đề xuất nào dành cho bạn.
        </p>
      )}

      {/* Data */}
      {!loading && !error && lectures.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {lectures.map((lecture) => {
            const chipColor = lecture.isRemedial ? 'bg-deep-rose' : 'bg-primary';
            const chipLabel = lecture.isRemedial
              ? `Phụ đạo: ${lecture.tagName}`
              : lecture.tagName;

            return (
              <div
                key={lecture.lectureId}
                className="group cursor-pointer rounded-xl overflow-hidden border border-whisper-border hover:border-primary/30 transition-all"
              >
                {/* Thumbnail placeholder */}
                <div className="relative w-full h-[180px] bg-surface-container overflow-hidden">
                  <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-primary-container/30">
                    <MaterialIcon name="play_circle" size={48} className="text-primary/40" />
                  </div>
                  <div className={`absolute top-3 left-3 ${chipColor} text-white text-[10px] font-bold px-2.5 py-1 rounded`}>
                    {chipLabel}
                  </div>
                </div>
                <div className="p-3">
                  <h4 className="text-sm font-bold text-on-surface truncate group-hover:text-primary transition-colors">
                    {lecture.title}
                  </h4>
                  {lecture.description && (
                    <p className="text-xs text-outline mt-1 line-clamp-2">
                      {lecture.description}
                    </p>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

