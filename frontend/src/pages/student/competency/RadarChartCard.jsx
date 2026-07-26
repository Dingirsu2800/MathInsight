/**
 * Multi-dimensional competency radar chart (SVG-based).
 * Data: recommenderApi.getWeakTags() — officialPoint per tag.
 */
import { useEffect, useMemo, useState } from 'react';
import { getWeakTags } from '../../../services/recommenderApi';

const SVG_SIZE = 400;
const CENTER = SVG_SIZE / 2;
const MAX_R = 160; // outermost polygon radius

/** Calculate SVG polygon point given angle & normalized value (0–1). */
function polarToXY(index, total, value) {
  const angle = (Math.PI * 2 * index) / total - Math.PI / 2;
  const r = MAX_R * value;
  return {
    x: CENTER + r * Math.cos(angle),
    y: CENTER + r * Math.sin(angle),
  };
}

/** Build polygon points string from values. */
function buildPolygon(values) {
  return values
    .map((v, i) => {
      const { x, y } = polarToXY(i, values.length, v);
      return `${x},${y}`;
    })
    .join(' ');
}

/** Build grid polygon at a given scale. */
function gridPolygon(count, scale) {
  return Array.from({ length: count }, (_, i) => {
    const { x, y } = polarToXY(i, count, scale);
    return `${x},${y}`;
  }).join(' ');
}

export default function RadarChartCard() {
  const [tags, setTags] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    getWeakTags()
      .then((data) => {
        if (!cancelled && data?.length > 0) setTags(data);
      })
      .catch(() => { /* keep empty */ })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  // Ensure at least 3 axes for a valid radar
  const axes = useMemo(() => {
    if (tags.length < 3) return [];
    // Take up to 8 tags for readability
    return tags.slice(0, 8);
  }, [tags]);

  const currentValues = axes.map((t) => Math.min(Number(t.officialPoint || 0) / 10, 1));
  // Target: 0.85 for all axes
  const targetValues = axes.map(() => 0.85);

  const n = axes.length;

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-on-surface">Bản đồ năng lực đa chiều</h3>
        <div className="flex gap-4">
          <div className="flex items-center gap-2">
            <span className="w-3 h-3 rounded-full bg-primary" />
            <span className="font-mono text-xs text-on-surface-variant">Hiện tại</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="w-3 h-3 rounded-full bg-outline" />
            <span className="font-mono text-xs text-on-surface-variant">Mục tiêu</span>
          </div>
        </div>
      </div>

      <div className="h-[320px] w-full relative flex items-center justify-center">
        {loading && (
          <div className="w-[280px] h-[280px] rounded-full bg-surface-container animate-pulse" />
        )}

        {!loading && n < 3 && (
          <p className="text-sm text-outline text-center">
            Cần ít nhất 3 chủ đề để hiển thị biểu đồ radar.
          </p>
        )}

        {!loading && n >= 3 && (
          <svg className="w-full h-full max-w-[380px]" viewBox={`0 0 ${SVG_SIZE} ${SVG_SIZE}`}>
            {/* Background grid (3 rings) */}
            {[1, 0.66, 0.33].map((scale) => (
              <polygon
                key={scale}
                fill="none"
                points={gridPolygon(n, scale)}
                stroke="currentColor"
                strokeWidth="1"
                className="text-surface-variant"
              />
            ))}

            {/* Axis lines */}
            {axes.map((_, i) => {
              const { x, y } = polarToXY(i, n, 1);
              return (
                <line
                  key={i}
                  x1={CENTER} y1={CENTER} x2={x} y2={y}
                  stroke="currentColor" strokeWidth="1" className="text-surface-variant"
                />
              );
            })}

            {/* Target shape (dashed) */}
            <polygon
              fill="rgba(114, 119, 132, 0.08)"
              points={buildPolygon(targetValues)}
              stroke="currentColor"
              strokeDasharray="4"
              strokeWidth="2"
              className="text-outline"
            />

            {/* Current shape */}
            <polygon
              fill="rgba(0, 88, 190, 0.15)"
              points={buildPolygon(currentValues)}
              stroke="currentColor"
              strokeWidth="3"
              className="text-primary"
            />

            {/* Data dots */}
            {currentValues.map((v, i) => {
              const { x, y } = polarToXY(i, n, v);
              return (
                <circle
                  key={i} cx={x} cy={y} r="5"
                  fill="white" stroke="currentColor" strokeWidth="2.5"
                  className="text-primary"
                />
              );
            })}

            {/* Labels */}
            {axes.map((tag, i) => {
              const { x, y } = polarToXY(i, n, 1.18);
              let anchor = 'middle';
              if (x < CENTER - 10) anchor = 'end';
              else if (x > CENTER + 10) anchor = 'start';
              return (
                <text
                  key={tag.tagId}
                  className="fill-on-surface-variant text-xs"
                  textAnchor={anchor}
                  x={x} y={y}
                  dominantBaseline="central"
                >
                  {tag.tagName}
                </text>
              );
            })}
          </svg>
        )}
      </div>
    </div>
  );
}
