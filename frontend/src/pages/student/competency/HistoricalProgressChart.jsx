/**
 * Historical competency progress line chart (SVG-based).
 * Data: gradingApi.getSessionHistory() — aggregates average score per month.
 */
import { useEffect, useState } from 'react';
import { getSessionHistory } from '../../../services/gradingApi';

/** Group sessions by month and compute average score per month. */
function aggregateByMonth(sessions) {
  const buckets = {};
  const order = [];

  sessions.forEach((s) => {
    if (!s.submittedAt) return;
    const d = new Date(s.submittedAt);
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    if (!buckets[key]) {
      buckets[key] = { total: 0, count: 0, label: formatMonthLabel(d) };
      order.push(key);
    }
    buckets[key].total += Number(s.score || 0);
    buckets[key].count += 1;
  });

  // Sort chronologically
  order.sort();

  // Take last 6
  const recent = order.slice(-6);
  return recent.map((key) => ({
    label: buckets[key].label,
    value: Math.round((buckets[key].total / buckets[key].count) * 10) / 10,
  }));
}

function formatMonthLabel(date) {
  const months = [
    'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
    'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12',
  ];
  return months[date.getMonth()];
}

export default function HistoricalProgressChart() {
  const [dataPoints, setDataPoints] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    // Fetch enough data to aggregate — up to 200 most recent sessions
    getSessionHistory({ page: 1, pageSize: 200 })
      .then((data) => {
        if (cancelled) return;
        const items = data.items || [];
        const aggregated = aggregateByMonth(items);
        setDataPoints(aggregated);
      })
      .catch(() => { /* keep empty */ })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, []);

  const months = dataPoints.map((d) => d.label);
  const values = dataPoints.map((d) => d.value);

  // SVG coordinates
  const chartW = 1000;
  const chartH = 300;
  const yCoords = values.map((v) => chartH - (v / 10) * chartH);
  const xStep = values.length > 1 ? chartW / (values.length - 1) : chartW / 2;
  const xCoords = values.map((_, i) => i * xStep);

  const pathD = xCoords
    .map((x, i) => `${i === 0 ? 'M' : 'L'}${x},${yCoords[i]}`)
    .join(' ');

  const areaD = xCoords.length > 0
    ? `${pathD} L${xCoords[xCoords.length - 1]},${chartH} L${xCoords[0]},${chartH} Z`
    : '';

  return (
    <section className="bg-pure-surface border border-whisper-border rounded-xl p-6">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h3 className="text-lg font-semibold text-on-surface">Lịch sử tiến bộ</h3>
          <p className="text-on-surface-variant text-sm">
            Biểu đồ theo dõi điểm năng lực trung bình qua các tháng
          </p>
        </div>
      </div>

      {loading && (
        <div className="h-[300px] w-full bg-surface-container rounded-lg animate-pulse" />
      )}

      {!loading && values.length === 0 && (
        <p className="text-sm text-outline text-center py-12">
          Chưa có dữ liệu lịch sử để hiển thị biểu đồ.
        </p>
      )}

      {!loading && values.length > 0 && (
        <div className="h-[300px] w-full relative">
          <svg className="w-full h-full overflow-visible" viewBox={`0 0 ${chartW} ${chartH}`}>
            {/* Grid lines */}
            {[0, 75, 150, 225, 300].map((y) => (
              <line
                key={y}
                x1="0" y1={y} x2={chartW} y2={y}
                stroke="currentColor"
                strokeWidth="1"
                className="text-surface-container"
              />
            ))}

            {/* Gradient fill under line */}
            <defs>
              <linearGradient id="lineGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="var(--color-primary)" />
                <stop offset="100%" stopColor="transparent" />
              </linearGradient>
            </defs>
            <path d={areaD} fill="url(#lineGradient)" opacity="0.1" />

            {/* The line */}
            <path
              d={pathD}
              fill="none"
              stroke="currentColor"
              strokeWidth="4"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="text-primary"
            />

            {/* Data points */}
            {xCoords.map((x, i) => (
              <g key={i}>
                <circle
                  cx={x}
                  cy={yCoords[i]}
                  r="6"
                  fill="white"
                  stroke="currentColor"
                  strokeWidth="3"
                  className="text-primary"
                />
                {/* Value label above dot */}
                <text
                  x={x}
                  y={yCoords[i] - 14}
                  textAnchor="middle"
                  className="fill-primary text-xs font-bold"
                >
                  {values[i]}
                </text>
              </g>
            ))}
          </svg>

          {/* X-axis labels */}
          <div className="flex justify-between mt-4">
            {months.map((m, i) => (
              <span key={i} className="font-mono text-xs text-on-surface-variant">
                {m}
              </span>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}
