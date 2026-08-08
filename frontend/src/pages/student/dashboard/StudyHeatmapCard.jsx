import { useMemo, useState, useEffect } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import api from '../../../services/api';

const INTENSITY_COLORS = [
  'bg-surface-container',       // 0 - no activity
  'bg-primary/20',              // 1 - low
  'bg-primary/40',              // 2 - medium-low
  'bg-primary/60',              // 3 - medium
  'bg-primary',                 // 4 - high
];

const DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export default function StudyHeatmapCard() {
  const [heatmapData, setHeatmapData] = useState([]);
  const [loading, setLoading] = useState(true);

  // Calculate the 12-week grid (columns = weeks, rows = Monday to Sunday)
  const gridData = useMemo(() => {
    // 1. Determine "today" and the Monday of the current week
    const now = new Date();
    // getDay(): 0 = Sun, 1 = Mon... 6 = Sat. We want 0 = Mon... 6 = Sun
    const dayOfWeek = now.getDay() === 0 ? 6 : now.getDay() - 1; 
    
    // 2. The start of our 12-week window is 11 weeks ago, on Monday
    const startDate = new Date(now);
    startDate.setDate(now.getDate() - dayOfWeek - (11 * 7));
    startDate.setHours(0, 0, 0, 0);

    // 3. Build the 12x7 grid
    const grid = [];
    for (let w = 0; w < 12; w++) {
      const week = [];
      for (let d = 0; d < 7; d++) {
        const cellDate = new Date(startDate);
        cellDate.setDate(startDate.getDate() + (w * 7) + d);
        
        // Find if we have API data for this date
        const dateString = cellDate.toLocaleDateString('en-CA'); // YYYY-MM-DD
        const apiDay = heatmapData.find(item => item.date === dateString);
        
        const count = apiDay ? apiDay.activityCount : 0;
        let intensity = 0;
        if (count === 1) intensity = 1;
        else if (count === 2) intensity = 2;
        else if (count === 3) intensity = 3;
        else if (count >= 4) intensity = 4;

        // Future dates in the current week shouldn't look like "0 intensity", but for simplicity we keep them as 0.
        // We can add an 'isFuture' flag if we want to gray them out differently later.
        const isFuture = cellDate > now;

        week.push({
          date: dateString,
          count: count,
          intensity: isFuture ? 0 : intensity
        });
      }
      grid.push(week);
    }
    return grid;
  }, [heatmapData]);

  useEffect(() => {
    const fetchHeatmap = async () => {
      setLoading(true);
      try {
        const res = await api.get('/gamification/heatmap');
        setHeatmapData(res.data?.days || []);
      } catch (err) {
        console.error("Lỗi khi tải biểu đồ tần suất:", err);
      } finally {
        setLoading(false);
      }
    };
    fetchHeatmap();
  }, []);

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
          {gridData.map((week, wi) => (
            <div key={wi} className="flex flex-col gap-1 flex-1">
              {week.map((cell, di) => (
                <div
                  key={di}
                  className={`h-4 rounded-sm ${INTENSITY_COLORS[cell.intensity]} hover:ring-2 hover:ring-primary/30 transition-all cursor-default ${loading ? 'animate-pulse' : ''}`}
                  title={`${cell.date}: ${cell.count} hoạt động`}
                />
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
