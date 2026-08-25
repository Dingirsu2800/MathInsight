import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import useCurrentUser from '../../../hooks/useCurrentUser';
import { getTargets } from '../../../services/gamificationApi';
import { getAllTagsMastery, calculateOverallCompetencyScore } from '../../../services/recommenderApi';
import { getStudentHistoryStats } from '../../../services/gradingApi';

/**
 * Tính phần trăm tiến độ mục tiêu trung bình từ danh sách targets.
 * Dùng progressPercentage nếu có, ngược lại tính currentPoint/targetPoint.
 */
function resolveWeeklyProgress(targets) {
  if (!Array.isArray(targets) || targets.length === 0) return null;
  const total = targets.reduce((sum, t) => {
    const pct = t.progressPercentage != null
      ? Number(t.progressPercentage)
      : t.targetPoint > 0 ? Math.min(100, (Number(t.currentPoint) / Number(t.targetPoint)) * 100) : 0;
    return sum + pct;
  }, 0);
  return Math.round(total / targets.length);
}

export default function WelcomeBanner() {
  const { profile, displayName: userName } = useCurrentUser('Bạn');

  const [competencyPoint, setCompetencyPoint] = useState(null);
  const [weeklyProgress, setWeeklyProgress] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;

    async function load() {
      const [tagMasteryResult, targetsResult, statsResult] = await Promise.allSettled([
        getAllTagsMastery(),
        getTargets(),
        getStudentHistoryStats(),
      ]);

      if (!isMounted) return;

      const tagMastery = tagMasteryResult.status === 'fulfilled' ? tagMasteryResult.value : null;
      const targets = targetsResult.status === 'fulfilled' ? targetsResult.value : null;

      setCompetencyPoint(calculateOverallCompetencyScore(tagMastery));
      setWeeklyProgress(resolveWeeklyProgress(targets));
      setLoading(false);
    }

    load();
    return () => { isMounted = false; };
  }, []);

  // Lấy khối lớp từ profile; profile trả về từ /api/v1/accounts/profile
  const gradeLabel = profile?.grade ? `Khối ${profile.grade}` : null;

  const displayCompetency = competencyPoint != null ? competencyPoint : '—';
  const displayProgress = weeklyProgress != null ? weeklyProgress : null;

  return (
    <section className="relative overflow-hidden rounded-2xl bg-primary text-white p-8 shadow-md">
      {/* Background decorative */}
      <div className="absolute top-0 right-0 w-1/2 h-full opacity-10">
        <MaterialIcon name="functions" className="text-[200px] absolute -top-8 -right-8" />
      </div>

      <div className="relative z-10 flex flex-col md:flex-row items-center justify-between gap-8">
        {/* Left: greeting */}
        <div className="space-y-2 max-w-xl text-center md:text-left">
          <h2 className="text-[30px] leading-[38px] font-semibold">
            Chào buổi sáng, {userName}! 👋
          </h2>
          {gradeLabel && (
            <p className="text-primary-fixed-dim/70 text-sm font-medium">{gradeLabel}</p>
          )}
          <p className="text-primary-fixed-dim/80 text-base">
            {displayProgress != null ? (
              <>
                Hôm nay là một ngày tuyệt vời để chinh phục Toán học. Bạn đã hoàn thành{' '}
                <span className="font-semibold text-white">{displayProgress}%</span> mục tiêu rồi!
              </>
            ) : (
              'Hôm nay là một ngày tuyệt vời để chinh phục Toán học. Hãy bắt đầu ngay!'
            )}
          </p>
          <div className="flex flex-wrap gap-4 pt-4 justify-center md:justify-start">
            <Link
              to="/student/lectures"
              className="px-6 py-2 bg-white !text-primary rounded-full font-bold shadow-lg hover:bg-surface-bright transition-colors active:scale-95 no-underline"
            >
              Tiếp tục học
            </Link>
            <Link
              to="/student/test"
              className="px-6 py-2 bg-primary-container border border-on-primary-container/30 text-white rounded-full font-bold hover:bg-primary-container/80 transition-colors active:scale-95"
            >
              Luyện tập
            </Link>
          </div>
        </div>

        {/* Right: competency gauge */}
        <div className="bg-white/10 backdrop-blur-md p-6 rounded-2xl border border-white/20 flex flex-col items-center">
          <p className="text-[12px] font-bold uppercase tracking-widest text-primary-fixed-dim mb-4">
            Điểm năng lực hiện tại
          </p>
          <div className="relative w-[140px] h-[70px]">
            <svg className="w-full h-full" viewBox="0 0 100 50">
              {/* Track */}
              <path
                d="M 10 45 A 35 35 0 0 1 90 45"
                fill="none"
                stroke="rgba(255,255,255,0.2)"
                strokeWidth="6"
                strokeLinecap="round"
              />
              {/* Progress arc — shows 0 while loading */}
              <path
                d="M 10 45 A 35 35 0 0 1 90 45"
                fill="none"
                stroke="white"
                strokeWidth="6"
                strokeLinecap="round"
                strokeDasharray="110"
                strokeDashoffset={
                  loading || competencyPoint == null
                    ? 110
                    : 110 - 110 * (competencyPoint / 10)
                }
                className="transition-all duration-1000"
              />
            </svg>
            <div className="absolute inset-0 flex items-end justify-center pb-1">
              {loading ? (
                <span className="text-lg font-bold text-white animate-pulse">…</span>
              ) : (
                <>
                  <span className="text-2xl font-bold text-white">{displayCompetency}</span>
                  <span className="text-primary-fixed-dim/60 text-sm ml-1 mb-0.5">/10</span>
                </>
              )}
            </div>
          </div>
          <p className="text-xs text-white/60 mt-2">
            Tính trên các chủ đề đã học
          </p>
        </div>
      </div>
    </section>
  );
}
