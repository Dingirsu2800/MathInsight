/**
 * Historical competency progress line chart (SVG-based).
 * Displays test session scores completed within the last 1 week (7 days) from current view time.
 * Data: gradingApi.getSessionHistory({ fromDate, pageSize: 100 })
 */
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getSessionHistory } from '../../../services/gradingApi';

function formatTestDateTime(isoString) {
  if (!isoString) return { date: '', time: '', full: '' };
  const d = new Date(isoString);
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const hours = String(d.getHours()).padStart(2, '0');
  const minutes = String(d.getMinutes()).padStart(2, '0');
  return {
    date: `${day}/${month}`,
    time: `${hours}:${minutes}`,
    full: `${day}/${month}/${d.getFullYear()} ${hours}:${minutes}`,
  };
}

export default function HistoricalProgressChart() {
  const navigate = useNavigate();
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [activePoint, setActivePoint] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(false);

    const now = new Date();
    const oneWeekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

    getSessionHistory({
      pageIndex: 1,
      pageSize: 100,
      fromDate: oneWeekAgo.toISOString(),
    })
      .then((data) => {
        if (cancelled) return;
        const items = Array.isArray(data?.items) ? data.items : [];

        // Filter tests strictly within the last 7 days up to now
        const filtered = items.filter((s) => {
          if (!s.submittedAt) return false;
          const submitTime = new Date(s.submittedAt).getTime();
          return submitTime >= oneWeekAgo.getTime() && submitTime <= now.getTime();
        });

        // Sort chronologically (oldest to newest)
        filtered.sort((a, b) => new Date(a.submittedAt) - new Date(b.submittedAt));

        const mapped = filtered.map((s) => {
          const { date, time, full } = formatTestDateTime(s.submittedAt);
          return {
            sessionId: s.sessionId,
            testName: s.testName || (s.testFormat === 'Exam' ? 'Bài kiểm tra' : 'Bài luyện tập'),
            testFormat: s.testFormat,
            score: s.score != null ? Math.round(Number(s.score) * 10) / 10 : 0,
            numCorrect: s.numCorrect ?? 0,
            totalQuestion: s.totalQuestion ?? 0,
            submittedAt: s.submittedAt,
            dateLabel: date,
            timeLabel: time,
            fullDateTime: full,
          };
        });

        setSessions(mapped);
      })
      .catch(() => {
        if (!cancelled) setError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // SVG dimensions and padding
  const chartW = 1000;
  const chartH = 300;
  const padTop = 45;
  const padBottom = 40;
  const padLeft = 60;
  const padRight = 50;
  const plotW = chartW - padLeft - padRight;
  const plotH = chartH - padTop - padBottom;

  const yTicks = [
    { label: '10.0', val: 10 },
    { label: '7.5', val: 7.5 },
    { label: '5.0', val: 5.0 },
    { label: '2.5', val: 2.5 },
    { label: '0.0', val: 0 },
  ];

  const getYCoord = (score) => padTop + plotH - (Math.min(Math.max(score, 0), 10) / 10) * plotH;
  const getXCoord = (index, total) => {
    if (total <= 1) return padLeft + plotW / 2;
    return padLeft + (index / (total - 1)) * plotW;
  };

  const points = sessions.map((s, i) => ({
    ...s,
    x: getXCoord(i, sessions.length),
    y: getYCoord(s.score),
  }));

  const pathD = points.length > 0
    ? points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x},${p.y}`).join(' ')
    : '';

  const areaD = points.length > 0
    ? `${pathD} L${points[points.length - 1].x},${padTop + plotH} L${points[0].x},${padTop + plotH} Z`
    : '';

  const avgScore = sessions.length > 0
    ? (sessions.reduce((sum, s) => sum + s.score, 0) / sessions.length).toFixed(1)
    : null;

  return (
    <section className="bg-pure-surface border border-whisper-border rounded-xl p-6 relative">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
        <div>
          <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2">
            Lịch sử tiến bộ
          </h3>
          <p className="text-on-surface-variant text-sm mt-0.5">
            Điểm các bài kiểm tra trong 1 tuần qua (7 ngày gần nhất)
          </p>
        </div>

        {!loading && !error && sessions.length > 0 && (
          <div className="flex items-center gap-3 self-start sm:self-auto">
            <div className="px-3 py-1.5 rounded-lg bg-surface-container border border-whisper-border text-xs">
              <span className="text-on-surface-variant">Tổng số bài: </span>
              <span className="font-bold text-on-surface">{sessions.length}</span>
            </div>
            <div className="px-3 py-1.5 rounded-lg bg-primary-fixed text-primary text-xs font-bold flex items-center gap-1">
              <MaterialIcon name="grade" size={15} className="text-primary" />
              <span>Điểm TB: {avgScore}/10</span>
            </div>
          </div>
        )}
      </div>

      {loading && (
        <div className="h-[300px] w-full bg-surface-container rounded-lg animate-pulse" />
      )}

      {!loading && error && (
        <p className="text-sm text-outline text-center py-12">
          Không thể tải dữ liệu lịch sử bài kiểm tra. Vui lòng thử lại sau.
        </p>
      )}

      {!loading && !error && sessions.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="w-12 h-12 rounded-full bg-surface-container flex items-center justify-center text-outline mb-3">
            <MaterialIcon name="history" size={24} />
          </div>
          <p className="text-sm font-semibold text-on-surface">
            Chưa có bài kiểm tra nào trong 1 tuần qua
          </p>
          <p className="text-xs text-on-surface-variant mt-1 max-w-sm">
            Hoàn thành các bài kiểm tra hoặc bài luyện tập để theo dõi biểu đồ tiến bộ của bạn.
          </p>
        </div>
      )}

      {!loading && !error && sessions.length > 0 && (
        <div className="relative">
          <div className="h-[300px] w-full relative">
            <svg
              className="w-full h-full overflow-visible"
              viewBox={`0 0 ${chartW} ${chartH}`}
              preserveAspectRatio="none"
            >
              {/* Grid lines & Y-axis labels */}
              {yTicks.map(({ label, val }) => {
                const y = getYCoord(val);
                return (
                  <g key={val}>
                    <line
                      x1={padLeft}
                      y1={y}
                      x2={chartW - padRight}
                      y2={y}
                      stroke="currentColor"
                      strokeWidth="1"
                      strokeDasharray={val === 0 || val === 10 ? 'none' : '4 4'}
                      className="text-whisper-border"
                    />
                    <text
                      x={padLeft - 10}
                      y={y + 4}
                      textAnchor="end"
                      className="fill-on-surface-variant font-mono text-[11px]"
                    >
                      {label}
                    </text>
                  </g>
                );
              })}

              {/* Gradient fill under line */}
              <defs>
                <linearGradient id="progressLineGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--color-primary, #1e40af)" stopOpacity="0.25" />
                  <stop offset="100%" stopColor="var(--color-primary, #1e40af)" stopOpacity="0.0" />
                </linearGradient>
              </defs>

              {areaD && <path d={areaD} fill="url(#progressLineGradient)" />}

              {/* The progress line (if more than 1 point) */}
              {points.length > 1 && (
                <path
                  d={pathD}
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="3.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="text-primary"
                />
              )}

              {/* Data points */}
              {points.map((p, i) => {
                const isHovered = activePoint?.sessionId === p.sessionId;
                return (
                  <g
                    key={p.sessionId || i}
                    className="cursor-pointer"
                    onMouseEnter={() => setActivePoint(p)}
                    onMouseLeave={() => setActivePoint(null)}
                    onClick={() => p.sessionId && navigate(`/student/test-result/${p.sessionId}`)}
                  >
                    {/* Invisible larger hover hitbox */}
                    <circle cx={p.x} cy={p.y} r="20" fill="transparent" />

                    {/* Outer glow ring on hover */}
                    {isHovered && (
                      <circle
                        cx={p.x}
                        cy={p.y}
                        r="12"
                        fill="currentColor"
                        className="text-primary opacity-20 transition-all"
                      />
                    )}

                    {/* Circle dot */}
                    <circle
                      cx={p.x}
                      cy={p.y}
                      r={isHovered ? '7' : '5.5'}
                      fill="white"
                      stroke="currentColor"
                      strokeWidth={isHovered ? '3.5' : '2.5'}
                      className="text-primary transition-all duration-150"
                    />

                    {/* Score text label above dot */}
                    <text
                      x={p.x}
                      y={p.y - 12}
                      textAnchor="middle"
                      className="fill-primary text-[13px] font-bold select-none"
                    >
                      {p.score}
                    </text>
                  </g>
                );
              })}
            </svg>

            {/* Hover Tooltip card overlay */}
            {activePoint && (
              <div
                className="absolute z-30 pointer-events-none bg-pure-surface border border-whisper-border rounded-xl p-3 shadow-xl text-xs -translate-x-1/2 -translate-y-full mb-3 min-w-[200px]"
                style={{
                  left: `${(activePoint.x / chartW) * 100}%`,
                  top: `${(activePoint.y / chartH) * 100}%`,
                }}
              >
                <div className="flex items-center justify-between gap-2 mb-1.5">
                  <span
                    className={`px-2 py-0.5 rounded text-[10px] font-bold uppercase ${
                      activePoint.testFormat === 'Exam'
                        ? 'bg-tertiary-fixed text-tertiary'
                        : 'bg-primary-fixed text-primary'
                    }`}
                  >
                    {activePoint.testFormat === 'Exam' ? 'Kiểm tra' : 'Luyện tập'}
                  </span>
                  <span className="text-on-surface-variant font-mono text-[11px]">
                    {activePoint.fullDateTime}
                  </span>
                </div>
                <p className="font-semibold text-on-surface truncate mb-1">
                  {activePoint.testName}
                </p>
                <div className="flex items-center justify-between text-[11px] text-on-surface-variant font-mono pt-1 border-t border-whisper-border">
                  <span>Điểm: <strong className="text-primary text-xs">{activePoint.score}/10</strong></span>
                  <span>{activePoint.numCorrect}/{activePoint.totalQuestion} đúng</span>
                </div>
                <p className="text-[10px] text-primary mt-1.5 text-center font-medium">
                  Nhấp để xem chi tiết kết quả
                </p>
              </div>
            )}
          </div>

          {/* X-axis labels below the chart */}
          <div className="relative w-full mt-2" style={{ paddingLeft: `${(padLeft / chartW) * 100}%`, paddingRight: `${(padRight / chartW) * 100}%` }}>
            <div className="flex justify-between items-start">
              {points.map((p, i) => (
                <div
                  key={p.sessionId || i}
                  className="flex flex-col items-center text-center cursor-pointer hover:text-primary transition-colors"
                  style={{
                    position: points.length === 1 ? 'relative' : 'absolute',
                    left: points.length === 1 ? 'auto' : `${(p.x / chartW) * 100}%`,
                    transform: points.length === 1 ? 'none' : 'translateX(-50%)',
                  }}
                  onClick={() => p.sessionId && navigate(`/student/test-result/${p.sessionId}`)}
                >
                  <span className="font-mono text-xs font-semibold text-on-surface">
                    {p.dateLabel}
                  </span>
                  <span className="font-mono text-[11px] text-on-surface-variant">
                    {p.timeLabel}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

