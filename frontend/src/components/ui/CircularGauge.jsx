import { clsx } from 'clsx';

/**
 * SVG Circular gauge with animated fill.
 * @param {{ value: number, max?: number, size?: number, strokeWidth?: number, className?: string, colorClass?: string }} props
 */
export default function CircularGauge({
  value,
  max = 10,
  size = 160,
  strokeWidth = 12,
  className,
  colorClass = 'text-primary',
}) {
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const progress = Math.min(value / max, 1);
  const offset = circumference - progress * circumference;
  const center = size / 2;

  return (
    <div className={clsx('relative inline-flex items-center justify-center', className)}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        {/* Background track */}
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="transparent"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-surface-container"
        />
        {/* Progress arc */}
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="transparent"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          strokeLinecap="round"
          className={clsx(colorClass, 'transition-all duration-1000 ease-out')}
          style={{
            transform: 'rotate(-90deg)',
            transformOrigin: '50% 50%',
          }}
        />
      </svg>
      {/* Center content slot */}
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-4xl font-extrabold text-primary">
          {value.toFixed(value % 1 === 0 ? 0 : 2)}
        </span>
        <span className="text-xs text-outline font-medium">/ {max.toFixed(max % 1 === 0 ? 0 : 1)}</span>
      </div>
    </div>
  );
}
