/**
 * Overall competency summary card with radial progress gauge.
 * Data: derived from recommenderApi.getWeakTags() — average of officialPoint values.
 */
import { useEffect, useRef, useState } from 'react';
import { getWeakTags } from '../../../services/recommenderApi';

function InfoPopover({ content }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (!open) return;
    function handleClick(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  return (
    <span ref={ref} className="relative inline-flex items-center ml-1.5" style={{ verticalAlign: 'middle' }}>
      <button
        type="button"
        aria-label="Thông tin"
        onClick={() => setOpen((v) => !v)}
        className="w-[18px] h-[18px] rounded-full flex items-center justify-center text-[11px] font-bold leading-none
          border border-current text-outline hover:text-primary hover:border-primary
          transition-colors duration-150 focus:outline-none focus:ring-2 focus:ring-primary/40"
      >
        i
      </button>

      {open && (
        <div
          className="absolute z-[200] top-full left-1/2 -translate-x-1/2 mt-2
            w-64 rounded-xl bg-pure-surface border border-whisper-border shadow-xl p-3
            text-xs text-on-surface-variant leading-relaxed"
        >
          {/* Arrow */}
          <span
            className="absolute left-1/2 -translate-x-1/2 -top-[7px]
              w-3 h-3 bg-pure-surface border-l border-t border-whisper-border
              rotate-45"
          />
          {content}
        </div>
      )}
    </span>
  );
}

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
      <h3 className="text-lg font-semibold text-on-surface self-start mb-6 flex items-center">
        Chỉ số năng lực tổng quát
        <InfoPopover
          content={
            <>
              <p className="font-semibold text-on-surface mb-1">Chỉ số năng lực tổng quát</p>
              <p>
                Điểm trung bình của tất cả các chủ đề bạn đã học, dao động từ{' '}
                <strong>0 đến 10</strong>.
              </p>
              <ul className="mt-2 space-y-0.5 pl-3 list-disc">
                <li><strong>≥ 8</strong> — Giỏi</li>
                <li><strong>≥ 6.5</strong> — Trung bình - Khá</li>
                <li><strong>≥ 5</strong> — Trung bình</li>
                <li><strong>&lt; 5</strong> — Cần cải thiện</li>
              </ul>
            </>
          }
        />
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
