import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../ui/MaterialIcon';
import { getLectures } from '../../services/learningApi';

export default function RelatedLecturesList({ currentLectureId, tagId, teacherId }) {
  const [lectures, setLectures] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    
    // We only fetch related lectures if we have a tagId or teacherId to search by.
    if (!tagId && !teacherId) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(false);

    // Prioritize fetching by same topic (tagId). If not enough, the backend doesn't automatically fallback, 
    // but for simplicity, we just fetch by topic.
    getLectures({ topic: tagId, pageSize: 10 })
      .then((res) => {
        if (!cancelled) {
          // Exclude the current lecture
          const filtered = (res.data?.items || []).filter(l => l.lectureId !== currentLectureId);
          setLectures(filtered.slice(0, 6)); // limit to 6 items
        }
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [currentLectureId, tagId, teacherId]);

  if (loading) {
    return (
      <div className="flex flex-col gap-4 w-full">
        <div className="h-6 bg-surface-container rounded animate-pulse w-1/2 mb-2"></div>
        {[1, 2, 3, 4].map(i => (
          <div key={i} className="flex gap-3 animate-pulse">
            <div className="w-40 h-24 bg-surface-container rounded-lg shrink-0"></div>
            <div className="flex-1 py-1 space-y-2">
              <div className="h-4 bg-surface-container-high rounded w-full"></div>
              <div className="h-4 bg-surface-container-high rounded w-3/4"></div>
              <div className="h-3 bg-surface-container rounded w-1/2 mt-3"></div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (error || lectures.length === 0) return null;

  return (
    <div className="w-full">
      <h3 className="text-[16px] font-bold text-on-surface mb-4 flex items-center gap-2">
        <MaterialIcon name="video_library" className="text-primary" />
        Bài giảng liên quan
      </h3>
      <div className="flex flex-col gap-4">
        {lectures.map((lecture) => (
          <Link
            key={lecture.lectureId}
            to={`/student/lectures/${lecture.lectureId}`}
            className="group flex gap-3 cursor-pointer items-start"
          >
            {/* Thumbnail */}
            <div className="relative w-40 h-24 bg-surface-container rounded-lg overflow-hidden shrink-0">
              {lecture.thumbnailUrl ? (
                <img src={lecture.thumbnailUrl} alt={lecture.title} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
              ) : (
                <div className="absolute inset-0 flex items-center justify-center bg-gradient-to-br from-primary/10 to-primary-container/30">
                  <MaterialIcon name="play_circle" size={32} className="text-primary/40 group-hover:text-primary transition-colors" />
                </div>
              )}
              {/* Duration or overlay icon could go here */}
            </div>

            {/* Info */}
            <div className="flex-1 min-w-0 pr-1">
              <h4 className="text-[14px] font-semibold text-on-surface leading-snug line-clamp-2 group-hover:text-primary transition-colors mb-1" title={lecture.title}>
                {lecture.title}
              </h4>
              <p className="text-[12px] text-on-surface-variant line-clamp-1 mb-0.5">
                {lecture.teacherName || "Giáo viên"}
              </p>
              <div className="flex items-center gap-2 text-[12px] text-on-surface-variant">
                <span className="flex items-center gap-1">
                  <MaterialIcon name="favorite" size={14} className="text-outline" />
                  {lecture.likes || 0}
                </span>
                <span>•</span>
                <span>{new Date(lecture.createdTime).toLocaleDateString('vi-VN')}</span>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
