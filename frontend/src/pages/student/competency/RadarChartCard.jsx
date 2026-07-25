/**
 * Multi-dimensional competency radar chart (SVG-based).
 * Mirrors the Stitch design's manual SVG radar implementation.
 */
export default function RadarChartCard() {
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

      <div className="h-[280px] w-full relative flex items-center justify-center">
        <svg className="w-full h-full max-w-[350px]" viewBox="0 0 400 400">
          {/* Background Grid */}
          <polygon
            fill="none"
            points="200,40 360,160 300,340 100,340 40,160"
            stroke="currentColor"
            strokeWidth="1"
            className="text-surface-variant"
          />
          <polygon
            fill="none"
            points="200,80 320,180 280,320 120,320 80,180"
            stroke="currentColor"
            strokeWidth="1"
            className="text-surface-variant"
          />
          <polygon
            fill="none"
            points="200,120 280,200 250,300 150,300 120,200"
            stroke="currentColor"
            strokeWidth="1"
            className="text-surface-variant"
          />

          {/* Target Shape (dashed) */}
          <polygon
            fill="rgba(114, 119, 132, 0.1)"
            points="200,60 340,170 280,330 120,330 60,170"
            stroke="currentColor"
            strokeDasharray="4"
            strokeWidth="2"
            className="text-outline"
          />

          {/* Current Shape */}
          <polygon
            fill="rgba(0, 88, 190, 0.2)"
            points="200,100 300,190 220,280 180,310 110,180"
            stroke="currentColor"
            strokeWidth="3"
            className="text-primary"
          />

          {/* Labels */}
          <text className="fill-on-surface-variant text-xs" textAnchor="middle" x="200" y="30">
            Giải tích
          </text>
          <text className="fill-on-surface-variant text-xs" textAnchor="start" x="370" y="160">
            Hình học
          </text>
          <text className="fill-on-surface-variant text-xs" textAnchor="middle" x="310" y="360">
            Xác suất
          </text>
          <text className="fill-on-surface-variant text-xs" textAnchor="middle" x="90" y="360">
            Đại số
          </text>
          <text className="fill-on-surface-variant text-xs" textAnchor="end" x="30" y="160">
            Lượng giác
          </text>
        </svg>
      </div>
    </div>
  );
}
