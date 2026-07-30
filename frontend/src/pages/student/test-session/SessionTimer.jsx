import { useEffect, useRef, useState } from 'react';

/**
 * SessionTimer supports both timed countdown and unlimited practice count-up modes.
 *
 * @param {{
 *   hasTimeLimit?: boolean,
 *   remainingSeconds?: number | null,
 *   elapsedSeconds?: number,
 *   onTimeUp?: () => void
 * }} props
 */
export default function SessionTimer({
  hasTimeLimit = true,
  remainingSeconds,
  elapsedSeconds = 0,
  onTimeUp
}) {
  const isTimed = hasTimeLimit === true && typeof remainingSeconds === 'number' && remainingSeconds !== null;

  // Timed Countdown State
  const [countdownSeconds, setCountdownSeconds] = useState(
    isTimed ? Math.max(0, Math.floor(remainingSeconds)) : 0
  );

  // Unlimited Elapsed Count-up State
  const [elapsedCount, setElapsedCount] = useState(
    Math.max(0, Math.floor(elapsedSeconds))
  );

  const onTimeUpRef = useRef(onTimeUp);
  const timeoutTriggeredRef = useRef(false);
  const hasInitializedRef = useRef(false);
  onTimeUpRef.current = onTimeUp;

  // 1. Sync Timed countdown when remainingSeconds updates from server
  useEffect(() => {
    if (isTimed) {
      const nextSeconds = Math.max(0, Math.floor(remainingSeconds));
      setCountdownSeconds(nextSeconds);
      hasInitializedRef.current = true;

      if (nextSeconds > 0) {
        timeoutTriggeredRef.current = false;
      } else if (nextSeconds === 0 && !timeoutTriggeredRef.current) {
        timeoutTriggeredRef.current = true;
        onTimeUpRef.current?.();
      }
    }
  }, [isTimed, remainingSeconds]);

  // 2. Sync Unlimited elapsed count when elapsedSeconds updates from server
  useEffect(() => {
    if (!isTimed && typeof elapsedSeconds === 'number') {
      const serverElapsed = Math.max(0, Math.floor(elapsedSeconds));
      setElapsedCount((prev) => (serverElapsed > prev ? serverElapsed : prev));
    }
  }, [isTimed, elapsedSeconds]);

  // 3. Tick Timer Effect
  useEffect(() => {
    if (isTimed) {
      if (countdownSeconds <= 0) {
        if (hasInitializedRef.current && !timeoutTriggeredRef.current) {
          timeoutTriggeredRef.current = true;
          onTimeUpRef.current?.();
        }
        return undefined;
      }

      const interval = setInterval(() => {
        setCountdownSeconds((prev) => {
          if (prev <= 1) {
            clearInterval(interval);
            if (hasInitializedRef.current && !timeoutTriggeredRef.current) {
              timeoutTriggeredRef.current = true;
              onTimeUpRef.current?.();
            }
            return 0;
          }
          return prev - 1;
        });
      }, 1000);

      return () => clearInterval(interval);
    } else {
      // Unlimited mode: count-up every 1 second
      const interval = setInterval(() => {
        setElapsedCount((prev) => prev + 1);
      }, 1000);

      return () => clearInterval(interval);
    }
  }, [isTimed, countdownSeconds > 0]); // eslint-disable-line react-hooks/exhaustive-deps

  const pad = (n) => String(n).padStart(2, '0');

  if (!isTimed) {
    const hrs = Math.floor(elapsedCount / 3600);
    const mins = Math.floor((elapsedCount % 3600) / 60);
    const secs = elapsedCount % 60;

    return (
      <div className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl border border-primary/20 bg-primary/10 text-primary font-mono text-xs md:text-sm font-bold shadow-sm select-none">
        <span className="material-symbols-outlined text-[18px]">timer</span>
        <span>Đã làm {pad(hrs)}:{pad(mins)}:{pad(secs)}</span>
      </div>
    );
  }

  const hrs = Math.floor(countdownSeconds / 3600);
  const mins = Math.floor((countdownSeconds % 3600) / 60);
  const secs = countdownSeconds % 60;

  // Color thresholds for timed mode
  const isWarning = countdownSeconds <= 300 && countdownSeconds > 60; // ≤ 5 min
  const isDanger = countdownSeconds <= 60; // ≤ 1 min

  const colorClass = isDanger
    ? 'text-white bg-red-600 border-red-700 animate-pulse'
    : isWarning
      ? 'text-amber-900 bg-amber-100 border-amber-300'
      : 'text-on-surface bg-surface-container-low border-whisper-border';

  return (
    <div
      className={`flex items-center gap-1.5 px-3.5 py-2 rounded-xl border font-mono text-xs md:text-sm font-bold transition-colors select-none ${colorClass}`}
    >
      <span className="material-symbols-outlined text-[18px]">timer</span>
      {hrs > 0 && <span>{pad(hrs)}:</span>}
      <span>{pad(mins)}:{pad(secs)}</span>
    </div>
  );
}
