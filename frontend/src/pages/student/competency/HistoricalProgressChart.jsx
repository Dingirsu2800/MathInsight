/**
 * Historical competency progress line chart (SVG-based).
 */
export default function HistoricalProgressChart() {
  // TODO: Replace with API data
  const months = ['Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12', 'Tháng 1', 'Hiện tại'];
  const values = [2.5, 3.0, 4.0, 4.5, 6.0, 7.0]; // mapped to y coordinates
  const yCoords = values.map((v) => 300 - (v / 10) * 300);
  const xStep = 1000 / (values.length - 1);
  const xCoords = values.map((_, i) => i * xStep);

  const pathD = xCoords
    .map((x, i) => `${i === 0 ? 'M' : 'L'}${x},${yCoords[i]}`)
    .join(' ');

  const areaD = `${pathD} L${xCoords[xCoords.length - 1]},300 L${xCoords[0]},300 Z`;

  return (
    <section className="bg-pure-surface border border-whisper-border rounded-xl p-6">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h3 className="text-lg font-semibold text-on-surface">Lịch sử tiến bộ</h3>
          <p className="text-on-surface-variant text-sm">
            Biểu đồ theo dõi điểm năng lực trung bình qua 6 tháng
          </p>
        </div>
        <select className="bg-pure-surface border border-whisper-border rounded-lg font-mono text-xs py-2 px-4 focus:ring-primary outline-none">
          <option>6 tháng gần nhất</option>
          <option>Học kỳ 1</option>
          <option>Tất cả thời gian</option>
        </select>
      </div>

      <div className="h-[300px] w-full relative">
        <svg className="w-full h-full overflow-visible" viewBox="0 0 1000 300">
          {/* Grid lines */}
          {[0, 75, 150, 225, 300].map((y) => (
            <line
              key={y}
              x1="0" y1={y} x2="1000" y2={y}
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
            <circle
              key={i}
              cx={x}
              cy={yCoords[i]}
              r="6"
              fill="white"
              stroke="currentColor"
              strokeWidth="3"
              className="text-primary"
            />
          ))}
        </svg>

        {/* X-axis labels */}
        <div className="flex justify-between mt-4">
          {months.map((m) => (
            <span key={m} className="font-mono text-xs text-on-surface-variant">
              {m}
            </span>
          ))}
        </div>
      </div>
    </section>
  );
}
