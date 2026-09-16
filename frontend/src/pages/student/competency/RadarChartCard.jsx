/**
 * Multi-dimensional competency radar chart (SVG-based).
 * Data: recommenderApi.getAllTagsMastery() — ALL topics including NotLearned (UC-55).
 * Tags with numberDone === 0 appear at the center (score = 0) with muted labels.
 * Grade tabs (Lớp 10 / 11 / 12) filter axes to keep the chart readable.
 * Only grades up to the student's current grade are shown.
 * Target line: gamificationApi.getTargets() — targetPoint per tagId (falls back to no line).
 */
import { useEffect, useMemo, useRef, useState } from 'react';
import { getAllTagsMastery } from '../../../services/recommenderApi';
import { getTargets } from '../../../services/gamificationApi';
import { getTopicGrade } from './TopicMasteryGrid';
import useCurrentUser from '../../../hooks/useCurrentUser';

// ─── Grade tabs ────────────────────────────────────────────────────────────────
const ALL_GRADE_TABS = [
  { label: 'Lớp 10', grade: 10 },
  { label: 'Lớp 11', grade: 11 },
  { label: 'Lớp 12', grade: 12 },
];

// ─── Info popover ──────────────────────────────────────────────────────────────
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
          className="absolute z-[200] top-full left-0 mt-2
            w-72 rounded-xl bg-pure-surface border border-whisper-border shadow-xl p-3
            text-xs text-on-surface-variant leading-relaxed"
        >
          {/* Arrow */}
          <span
            className="absolute left-4 -top-[7px]
              w-3 h-3 bg-pure-surface border-l border-t border-whisper-border
              rotate-45"
          />
          {content}
        </div>
      )}
    </span>
  );
}

// ─── SVG constants ─────────────────────────────────────────────────────────────
const SVG_WIDTH = 580;
const SVG_HEIGHT = 400;
const CENTER_X = SVG_WIDTH / 2;
const CENTER_Y = SVG_HEIGHT / 2;
const MAX_R = 120; // outermost polygon radius
const LABEL_R = MAX_R + 18; // radius where label anchors sit

/** Calculate SVG polygon point given angle & normalized value (0–1). */
function polarToXY(index, total, value, maxR = MAX_R, cx = CENTER_X, cy = CENTER_Y) {
  const angle = (Math.PI * 2 * index) / total - Math.PI / 2;
  const r = maxR * value;
  return {
    x: cx + r * Math.cos(angle),
    y: cy + r * Math.sin(angle),
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

/** Wrap long Vietnamese topic titles into at most 2 lines */
function splitLabel(text, maxChars = 18) {
  if (!text) return [];
  const trimmed = text.trim();
  if (trimmed.length <= maxChars) return [trimmed];
  const words = trimmed.split(/\s+/);
  if (words.length <= 1) return [trimmed];

  const lines = [];
  let current = '';
  for (const w of words) {
    const candidate = current ? `${current} ${w}` : w;
    if (candidate.length <= maxChars) {
      current = candidate;
    } else {
      if (current) lines.push(current);
      current = w;
    }
  }
  if (current) lines.push(current);

  if (lines.length > 2) {
    return [lines[0], lines.slice(1).join(' ')];
  }
  return lines;
}

// ─── Main component ────────────────────────────────────────────────────────────
export default function RadarChartCard() {
  const { profile } = useCurrentUser('Học sinh');
  const currentGrade = profile?.student?.currentGrade ?? null;

  const [tags, setTags] = useState([]);
  const [targetMap, setTargetMap] = useState({});
  const [loading, setLoading] = useState(true);
  const [activeGrade, setActiveGrade] = useState(null); // will be set after data loads

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      getAllTagsMastery().catch(() => []),
      getTargets().catch(() => []),
    ]).then(([masteryData, targetData]) => {
      if (cancelled) return;
      if (masteryData?.length > 0) setTags(masteryData);
      // Build a map: tagId -> targetPoint (0-10)
      const map = {};
      if (Array.isArray(targetData)) {
        targetData.forEach((t) => { map[t.tagId] = Number(t.targetPoint || 0); });
      }
      setTargetMap(map);
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  // Determine available grades from data (intersected with student's current grade)
  const availableGrades = useMemo(() => {
    const grades = new Set();
    tags.forEach((t) => {
      const g = getTopicGrade(t);
      if (g) grades.add(g);
    });
    // Only show grades that exist in data AND are <= student's current grade
    return [10, 11, 12].filter((g) => {
      if (!grades.has(g)) return false;
      if (currentGrade != null && g > currentGrade) return false;
      return true;
    });
  }, [tags, currentGrade]);

  // Set default activeGrade once we know currentGrade and availableGrades
  useEffect(() => {
    if (availableGrades.length === 0) return;
    // Default to currentGrade if available, otherwise the highest available grade
    const preferred = currentGrade != null && availableGrades.includes(currentGrade)
      ? currentGrade
      : availableGrades[availableGrades.length - 1];
    setActiveGrade((prev) => {
      // Only reset if prev is not in the available list (handles grade change)
      if (prev != null && availableGrades.includes(prev)) return prev;
      return preferred;
    });
  }, [availableGrades, currentGrade]);

  // Tabs to render: only grades <= student's current grade that have data
  const visibleTabs = useMemo(() =>
    ALL_GRADE_TABS.filter((tab) => availableGrades.includes(tab.grade)),
  [availableGrades]);

  // Filter axes by activeGrade; include ALL tags (practiced + unpracticed)
  const axes = useMemo(() => {
    if (activeGrade == null) return [];
    return tags.filter((t) => getTopicGrade(t) === activeGrade);
  }, [tags, activeGrade]);

  const currentValues = axes.map((t) => Math.min(Number(t.officialPoint || 0) / 10, 1));

  // Target: only use explicitly set targets — null means no target for that axis
  const targetValues = axes.map((t) => {
    const tp = targetMap[t.tagId];
    return (tp != null && tp > 0) ? Math.min(tp / 10, 1) : null;
  });
  const hasAnyTarget = targetValues.some((v) => v !== null);

  const n = axes.length;

  // Check if any tab has data
  const hasAnyData = tags.length > 0;

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl p-6">
      {/* ── Header ── */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 mb-5">
        <h3 className="text-lg font-semibold text-on-surface flex items-center flex-shrink-0">
          Bản đồ năng lực đa chiều
          <InfoPopover
            content={
              <>
                <p className="font-semibold text-on-surface mb-1">Bản đồ năng lực đa chiều</p>
                <p>
                  Biểu đồ radar thể hiện điểm số (0–10) của từng chủ đề,
                  giúp bạn dễ dàng nhận ra các mảng mạnh và cần cải thiện.
                </p>
                <ul className="mt-2 space-y-1 pl-3 list-disc">
                  <li>
                    <span className="inline-block w-2 h-2 rounded-full bg-primary mr-1 align-middle" />
                    <strong>Hiện tại</strong> — kết quả thực tế của bạn.
                  </li>
                  <li>
                    <span className="inline-block w-2 h-2 rounded-full bg-outline/40 border border-dashed border-outline mr-1 align-middle" />
                    <strong>Chưa làm</strong> — chủ đề bạn chưa luyện tập (điểm = 0).
                  </li>
                  {hasAnyTarget && (
                    <li>
                      <span className="inline-block w-2 h-2 rounded-full bg-outline mr-1 align-middle" />
                      <strong>Mục tiêu</strong> — mục tiêu bạn đã đặt cho từng chủ đề.
                    </li>
                  )}
                </ul>
                <p className="mt-2">
                  Vùng tô sáng càng gần viền ngoài, năng lực càng cao.
                </p>
              </>
            }
          />
        </h3>

        {/* Legend */}
        <div className="flex flex-wrap gap-4 items-center">
          <div className="flex items-center gap-2">
            <span className="w-3 h-3 rounded-full bg-primary" />
            <span className="font-mono text-xs text-on-surface-variant">Hiện tại</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="w-3 h-3 rounded-full bg-surface-container-high border border-dashed border-outline/50" />
            <span className="font-mono text-xs text-on-surface-variant">Chưa làm</span>
          </div>
          {hasAnyTarget && (
            <div className="flex items-center gap-2">
              <span className="w-3 h-3 rounded-full bg-outline" />
              <span className="font-mono text-xs text-on-surface-variant">Mục tiêu</span>
            </div>
          )}
        </div>
      </div>

      {/* ── Grade Tabs ── */}
      {!loading && hasAnyData && visibleTabs.length > 1 && (
        <div className="flex gap-1 p-1 bg-surface-container-low rounded-xl border border-whisper-border w-fit mb-5">
          {visibleTabs.map((tab) => (
            <button
              key={tab.grade}
              type="button"
              onClick={() => setActiveGrade(tab.grade)}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                activeGrade === tab.grade
                  ? 'bg-primary text-white shadow-sm'
                  : 'text-on-surface-variant hover:text-on-surface hover:bg-surface-container'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      )}

      {/* ── Chart area ── */}
      <div className="h-[360px] sm:h-[400px] w-full relative flex items-center justify-center">
        {loading && (
          <div className="w-[280px] h-[280px] rounded-full bg-surface-container animate-pulse" />
        )}

        {!loading && n < 3 && hasAnyData && (
          <p className="text-sm text-outline text-center">
            Không có đủ chủ đề{activeGrade ? ` cho Lớp ${activeGrade}` : ''} để hiển thị biểu đồ radar
            {n > 0 ? ` (cần ít nhất 3, hiện có ${n})` : ''}.
          </p>
        )}

        {!loading && !hasAnyData && (
          <p className="text-sm text-outline text-center">
            Chưa có dữ liệu chủ đề nào.
          </p>
        )}

        {!loading && n >= 3 && (
          <svg
            className="w-full h-full max-w-[620px] max-h-[400px] overflow-visible"
            viewBox={`0 0 ${SVG_WIDTH} ${SVG_HEIGHT}`}
          >
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
                  x1={CENTER_X}
                  y1={CENTER_Y}
                  x2={x}
                  y2={y}
                  stroke="currentColor"
                  strokeWidth="1"
                  className="text-surface-variant"
                />
              );
            })}

            {/* Target markers — dashed circle per axis that has a target */}
            {hasAnyTarget && axes.map((tag, i) => {
              const tv = targetValues[i];
              if (tv === null) return null;
              const { x, y } = polarToXY(i, n, tv);
              return (
                <circle
                  key={`target-${tag.tagId}`}
                  cx={x}
                  cy={y}
                  r="6"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeDasharray="3 2"
                  className="text-outline"
                />
              );
            })}

            {/* Current shape */}
            <polygon
              fill="rgba(0, 88, 190, 0.15)"
              points={buildPolygon(currentValues)}
              stroke="currentColor"
              strokeWidth="3"
              className="text-primary"
            />

            {/* Data dots — practiced tags solid, unpracticed hollow/muted */}
            {axes.map((tag, i) => {
              const v = currentValues[i];
              const isPracticed = tag.numberDone > 0;
              const { x, y } = polarToXY(i, n, v);

              if (isPracticed) {
                return (
                  <circle
                    key={tag.tagId}
                    cx={x}
                    cy={y}
                    r="5"
                    fill="white"
                    stroke="currentColor"
                    strokeWidth="2.5"
                    className="text-primary"
                  />
                );
              }
              // Unpracticed: small muted dot at center (v=0)
              return (
                <circle
                  key={tag.tagId}
                  cx={x}
                  cy={y}
                  r="4"
                  fill="currentColor"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeDasharray="2 1"
                  className="text-outline/40"
                />
              );
            })}

            {/* Labels */}
            {axes.map((tag, i) => {
              const angle = (Math.PI * 2 * i) / n - Math.PI / 2;
              const cos = Math.cos(angle);
              const sin = Math.sin(angle);
              const rawX = CENTER_X + LABEL_R * cos;
              const rawY = CENTER_Y + LABEL_R * sin;

              let anchor = 'middle';
              let x = rawX;
              let y = rawY;

              if (cos > 0.25) {
                anchor = 'start';
                x += 6;
              } else if (cos < -0.25) {
                anchor = 'end';
                x -= 6;
              }

              const lines = splitLabel(tag.tagName, 18);
              const isTop = sin < -0.55;
              const isBottom = sin > 0.55;
              const isPracticed = tag.numberDone > 0;

              return (
                <text
                  key={tag.tagId}
                  className={`text-[11px] sm:text-xs font-medium select-none ${
                    isPracticed ? 'fill-on-surface' : 'fill-outline'
                  }`}
                  textAnchor={anchor}
                  x={x}
                  y={y}
                >
                  <title>{
                    isPracticed
                      ? `${tag.tagName}: ${Number(tag.officialPoint || 0).toFixed(1)}/10`
                      : `${tag.tagName}: Chưa làm`
                  }</title>
                  {lines.map((line, idx) => {
                    let dy = '0.35em';
                    if (lines.length > 1) {
                      if (idx === 0) {
                        dy = isTop ? '-1.3em' : isBottom ? '0.8em' : '-0.3em';
                      } else {
                        dy = '1.25em';
                      }
                    } else {
                      dy = isTop ? '-0.4em' : isBottom ? '1.0em' : '0.35em';
                    }
                    return (
                      <tspan key={idx} x={x} dy={dy}>
                        {line}
                      </tspan>
                    );
                  })}
                </text>
              );
            })}
          </svg>
        )}
      </div>
    </div>
  );
}
