import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getStoredUser } from '../../../services/authApi';

// TODO: Replace with real API data.
// Needs endpoint: GET /api/v1/reports/competency-summary (not yet implemented in backend)
// Expected DTO: { userName, grade, competencyPoint, weeklyProgressPercent, trendDelta }
const MOCK_DATA = {
  grade: 'Khối 12 — Học kỳ 2',
  competencyPoint: 6.8,
  weeklyProgress: 75,
  trendDelta: 0.4,
};

export default function WelcomeBanner() {
  const user = getStoredUser();
  const userName = user?.username || 'Bạn';
  const { grade, competencyPoint, weeklyProgress, trendDelta } = MOCK_DATA;

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
          <p className="text-primary-fixed-dim/80 text-base">
            Hôm nay là một ngày tuyệt vời để chinh phục các bài tập Tích phân. Bạn đã hoàn thành{' '}
            {weeklyProgress}% mục tiêu tuần rồi!
          </p>
          <div className="flex flex-wrap gap-4 pt-4 justify-center md:justify-start">
            <button className="px-6 py-2 bg-white text-primary rounded-full font-bold shadow-lg hover:bg-surface-bright transition-colors active:scale-95">
              Tiếp tục học
            </button>
            <button className="px-6 py-2 bg-primary-container border border-on-primary-container/30 text-white rounded-full font-bold hover:bg-primary-container/80 transition-colors active:scale-95">
              Xem lộ trình
            </button>
          </div>
        </div>

        {/* Right: competency gauge */}
        <div className="bg-white/10 backdrop-blur-md p-6 rounded-2xl border border-white/20 flex flex-col items-center">
          <p className="text-[12px] font-bold uppercase tracking-widest text-primary-fixed-dim mb-4">
            Điểm năng lực hiện tại
          </p>
          <div className="relative w-[140px] h-[70px]">
            <svg className="w-full h-full" viewBox="0 0 100 50">
              <path
                d="M 10 45 A 35 35 0 0 1 90 45"
                fill="none"
                stroke="rgba(255,255,255,0.2)"
                strokeWidth="6"
                strokeLinecap="round"
              />
              <path
                d="M 10 45 A 35 35 0 0 1 90 45"
                fill="none"
                stroke="white"
                strokeWidth="6"
                strokeLinecap="round"
                strokeDasharray="110"
                strokeDashoffset={110 - 110 * (competencyPoint / 10)}
                className="transition-all duration-1000"
              />
            </svg>
            <div className="absolute inset-0 flex items-end justify-center pb-1">
              <span className="text-2xl font-bold text-white">{competencyPoint}</span>
              <span className="text-primary-fixed-dim/60 text-sm ml-1 mb-0.5">/10</span>
            </div>
          </div>
          <p className="text-xs text-white/70 mt-2">
            Tăng {trendDelta} so với tuần trước
          </p>
        </div>
      </div>
    </section>
  );
}
