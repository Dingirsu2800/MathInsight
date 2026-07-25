import { clsx } from 'clsx';

/**
 * Color-coded circular score badge.
 * @param {{ score: number }} props
 */
export default function ScoreBadge({ score }) {
  let bgClass, textClass;
  if (score >= 8.5) {
    bgClass = 'bg-emerald-success';
    textClass = 'text-white';
  } else if (score >= 7) {
    bgClass = 'bg-emerald-success/20';
    textClass = 'text-emerald-success';
  } else if (score >= 5) {
    bgClass = 'bg-amber-warning/20';
    textClass = 'text-amber-warning';
  } else {
    bgClass = 'bg-error-container';
    textClass = 'text-error';
  }

  return (
    <div
      className={clsx(
        'inline-flex items-center justify-center w-10 h-10 rounded-full font-mono text-[16px] font-bold',
        bgClass,
        textClass
      )}
    >
      {score.toFixed(1)}
    </div>
  );
}
