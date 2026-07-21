import { useEffect, useRef, useState } from 'react';

/**
 * Countdown timer synced with server-provided remaining seconds (BR-12).
 * Display-only countdown — the server enforces the real deadline.
 *
 * @param {{ remainingSeconds: number, onTimeUp: () => void }} props
 */
export default function SessionTimer({ remainingSeconds, onTimeUp }) {
  const [seconds, setSeconds] = useState(Math.max(0, Math.floor(remainingSeconds)));
  const onTimeUpRef = useRef(onTimeUp);
  onTimeUpRef.current = onTimeUp;

  // Sync when server gives a new remainingSeconds (e.g. after auto-save response)
  useEffect(() => {
    setSeconds(Math.max(0, Math.floor(remainingSeconds)));
  }, [remainingSeconds]);

  // Tick every second
  useEffect(() => {
    if (seconds <= 0) return;

    const interval = setInterval(() => {
      setSeconds((prev) => {
        if (prev <= 1) {
          clearInterval(interval);
          onTimeUpRef.current?.();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(interval);
  }, [seconds > 0]); // eslint-disable-line react-hooks/exhaustive-deps

  const hrs = Math.floor(seconds / 3600);
  const mins = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;
  const pad = (n) => String(n).padStart(2, '0');

  // Color thresholds
  const isWarning = seconds <= 300 && seconds > 60; // ≤ 5 min
  const isDanger = seconds <= 60; // ≤ 1 min

  const colorClass = isDanger
    ? 'text-white bg-red-600 animate-pulse'
    : isWarning
      ? 'text-amber-900 bg-amber-100 border-amber-300'
      : 'text-on-surface bg-surface-container-low border-whisper-border';

  return (
    <div
      className={`flex items-center gap-2 px-4 py-2 rounded-xl border font-mono text-sm font-bold transition-colors ${colorClass}`}
    >
      <span className="material-symbols-outlined text-base">timer</span>
      {hrs > 0 && <span>{pad(hrs)}:</span>}
      <span>{pad(mins)}:{pad(secs)}</span>
    </div>
  );
}
