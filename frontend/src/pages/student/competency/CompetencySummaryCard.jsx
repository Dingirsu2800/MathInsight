/**
 * Overall competency summary card with radial progress gauge.
 * Data: derived from recommenderApi.getWeakTags() — average of officialPoint values.
 */
import { useEffect, useState } from 'react';
import { getWeakTags } from '../../../services/recommenderApi';

export default function CompetencySummaryCard() {
  const [score, setScore] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    getWeakTags()
      .then((data) => {
        if (cancelled) return;
        if (data && data.length > 0) {
          const avg = data.reduce((sum, t) => sum + Number(t.officialPoint || 0), 0) / data.length;
          setScore(Math.round(avg * 10) / 10);
        }
      })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, []);

  const maxScore = 10;
  const radius = 88;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (score / maxScore) * circumference;

  // Derive mastery label
  let masteryLabel = 'Chưa đủ dữ liệu';
  let masteryClass = 'text-outline';
  if (!loading && !error && score > 0) {
    if (score >= 8) { masteryLabel = 'Giỏi'; masteryClass = 'text-emerald-success'; }
    else if (score >= 6.5) { masteryLabel = 'Trung bình - Khá'; masteryClass = 'text-amber-warning'; }
    else if (score >= 5) { masteryLabel = 'Trung bình'; masteryClass = 'text-amber-warning'; }
    else { masteryLabel = 'Cần cải thiện'; masteryClass = 'text-deep-rose'; }
  }

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-6 flex flex-col items-center justify-center">
      <h3 className="text-lg font-semibold text-on-surface self-start mb-6">
        Chỉ số năng lực tổng quát
      </h3>

      {loading && (
        <div className="w-48 h-48 rounded-full bg-surface-container animate-pulse" />
      )}

      {!loading && error && (
        <p className="text-sm text-outline text-center py-6">
          Không thể tải dữ liệu năng lực.
        </p>
      )}

      {!loading && !error && (
        <>
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

          <p className="mt-4 text-center text-on-surface-variant text-sm">
            Năng lực của bạn đang ở mức{' '}
            <span className={`font-bold ${masteryClass}`}>{masteryLabel}</span>.
          </p>
        </>
      )}
    </div>
  );
}
