import * as React from "react";
import { getStreak } from "../../services/gamificationApi";

export default function StreakIndicator() {
  const [streakData, setStreakData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);

  const fetchStreak = () => {
    getStreak()
      .then((data) => {
        setStreakData(data);
      })
      .catch((err) => {
        console.error("Lỗi khi tải Streak:", err);
      })
      .finally(() => {
        setLoading(false);
      });
  };

  React.useEffect(() => {
    fetchStreak();
    window.addEventListener("gamification_updated", fetchStreak);
    return () => window.removeEventListener("gamification_updated", fetchStreak);
  }, []);

  if (loading) return null;

  // The gamification API returns { currentStreak, longestStreak, lastActivityDate, isActive }
  // according to spec, if isActive is false, it returns 0.
  const displayStreak = streakData?.currentStreak || 0;
  const isHot = displayStreak > 0;

  return (
    <div 
      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full border ${
        isHot 
          ? "bg-error/10 border-error/20 text-error" 
          : "bg-surface-variant/50 border-whisper-border text-on-surface-variant"
      } font-bold select-none cursor-default shadow-sm transition-colors`}
      title="Chuỗi ngày học liên tiếp"
    >
      <span 
        className="material-symbols-outlined text-[18px]"
        style={{ fontVariationSettings: isHot ? "'FILL' 1" : "'FILL' 0" }}
      >
        local_fire_department
      </span>
      <span className="text-[14px] font-extrabold tracking-tight">
        {displayStreak}
      </span>
    </div>
  );
}
