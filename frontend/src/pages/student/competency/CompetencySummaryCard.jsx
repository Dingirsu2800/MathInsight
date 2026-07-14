/**
 * Overall competency summary card with radial progress gauge.
 */
export default function CompetencySummaryCard() {
  // TODO: Replace with real API data.
  // Needs endpoint: GET /api/v1/reports/competency-summary (not yet implemented in backend)
  // Expected DTO: { officialPoint, trendDelta, masteryLabel }
  const score = 6.8;
  const maxScore = 10;
  const trend = '+0.3';

  const radius = 88;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (score / maxScore) * circumference;

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-6 flex flex-col items-center justify-center">
      <h3 className="text-lg font-semibold text-on-surface self-start mb-6">
        Chỉ số năng lực tổng quát
      </h3>

      {/* Radial gauge */}
      <div className="relative w-48 h-48 flex items-center justify-center">
        <svg className="w-full h-full" viewBox="0 0 192 192">
          <circle
            cx="96" cy="96" r={radius}
            fill="transparent"
            stroke="currentColor"
            strokeWidth="12"
            className="text-surface-container"
          />
          <circle
            cx="96" cy="96" r={radius}
            fill="transparent"
            stroke="currentColor"
            strokeWidth="12"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            strokeLinecap="round"
            className="text-primary transition-all duration-1000"
            style={{ transform: 'rotate(-90deg)', transformOrigin: '50% 50%' }}
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-[30px] leading-[38px] font-semibold text-primary">{score}</span>
          <span className="font-mono text-xs text-on-surface-variant">/ {maxScore.toFixed(1)}</span>
        </div>
      </div>

      {/* Trend badge */}
      <div className="mt-6 flex items-center gap-2 bg-emerald-success/10 text-emerald-success px-3 py-1.5 rounded-full font-bold text-xs">
        <span className="material-symbols-outlined text-sm">trending_up</span>
        <span>{trend} so với tháng trước</span>
      </div>

      <p className="mt-4 text-center text-on-surface-variant text-sm">
        Năng lực của bạn đang ở mức{' '}
        <span className="text-amber-warning font-bold">Trung bình - Khá</span>.
        Hãy tập trung cải thiện các phần kiến thức còn yếu để đạt mục tiêu 8.5.
      </p>
    </div>
  );
}
