import { useMemo } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';

// TODO: Replace with API data
const MOCK_HEATMAP_DATA = generateMockHeatmap();

function generateMockHeatmap() {
  const weeks = 12;
  const days = 7;
  const data = [];
  for (let w = 0; w < weeks; w++) {
    const week = [];
    for (let d = 0; d < days; d++) {
      week.push(Math.floor(Math.random() * 5)); // 0-4 intensity levels
    }
    data.push(week);
  }
  return data;
}

const INTENSITY_COLORS = [
  'bg-surface-container',       // 0 - no activity
  'bg-primary/20',              // 1 - low
  'bg-primary/40',              // 2 - medium-low
  'bg-primary/60',              // 3 - medium
  'bg-primary',                 // 4 - high
];

const DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export default function StudyHeatmapCard() {
  const months = useMemo(() => {
    const now = new Date();
    const result = [];
    for (let i = 2; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      result.push(d.toLocaleDateString('vi-VN', { month: 'long' }));
    }
    return result;
  }, []);

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-on-surface flex items-center gap-2">
          <MaterialIcon name="calendar_month" className="text-primary" />
          Tần suất học tập
        </h3>
        <div className="flex items-center gap-2 text-[11px] text-outline">
          <span>Ít</span>
          {INTENSITY_COLORS.map((color, i) => (
            <div key={i} className={`w-3 h-3 rounded-sm ${color}`} />
          ))}
          <span>Nhiều</span>
        </div>
      </div>

      {/* Month labels */}
      <div className="flex gap-4 ml-8 mb-2">
        {months.map((m) => (
          <span key={m} className="text-[10px] text-outline flex-1 text-center font-medium">
            {m}
          </span>
        ))}
      </div>

      {/* Heatmap grid */}
      <div className="flex gap-1">
        {/* Day labels */}
        <div className="flex flex-col gap-1">
          {DAY_LABELS.map((d) => (
            <span key={d} className="text-[10px] text-outline w-6 h-4 flex items-center">
              {d}
            </span>
          ))}
        </div>

        {/* Grid cells */}
        <div className="flex gap-1 flex-1">
          {MOCK_HEATMAP_DATA.map((week, wi) => (
            <div key={wi} className="flex flex-col gap-1 flex-1">
              {week.map((intensity, di) => (
                <div
                  key={di}
                  className={`h-4 rounded-sm ${INTENSITY_COLORS[intensity]} hover:ring-2 hover:ring-primary/30 transition-all cursor-default`}
                  title={`${intensity} bài tập`}
                />
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
