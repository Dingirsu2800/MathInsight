import { clsx } from 'clsx';

/**
 * Horizontal progress bar.
 * @param {{ value: number, max?: number, height?: string, colorClass?: string, trackClass?: string, className?: string }} props
 */
export default function ProgressBar({
  value,
  max = 100,
  height = 'h-2',
  colorClass = 'bg-primary',
  trackClass = 'bg-surface-container',
  className,
}) {
  const percent = Math.min((value / max) * 100, 100);

  return (
    <div className={clsx('w-full rounded-full overflow-hidden', trackClass, height, className)}>
      <div
        className={clsx('h-full rounded-full transition-all duration-1000 ease-out', colorClass)}
        style={{ width: `${percent}%` }}
      />
    </div>
  );
}
