import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getRecommendedMaterials } from '../../../services/recommenderApi';

/** Derive bullet color from officialPoint */
function getBulletColor(officialPoint) {
  const s = Number(officialPoint);
  if (s < 5) return 'bg-deep-rose';
  if (s < 7.5) return 'bg-amber-warning';
  return 'bg-emerald-success';
}

/**
 * Improvement call-to-action + review suggestions footer from Stitch competency design.
 * Suggestions section is powered by GET /api/v1/recommender/materials (UC-54).
 */
export default function ImprovementCTACard() {
  const [suggestions, setSuggestions] = useState([]);

  useEffect(() => {
    let cancelled = false;

    getRecommendedMaterials()
      .then((data) => {
        if (!cancelled) {
          // Prefer remedial materials first, take top 3
          const remedial = data.filter((m) => m.isRemedial);
          const top3 = (remedial.length > 0 ? remedial : data).slice(0, 3);
          setSuggestions(top3);
        }
      })
      .catch(() => {
        // Silently fail — static fallback content stays visible
      });

    return () => { cancelled = true; };
  }, []);

  // Static fallback when no API data yet
  const FALLBACK_SUGGESTIONS = [
    { key: 'fallback-1', color: 'bg-deep-rose', text: 'Trắc nghiệm Hình học không gian (Dễ)' },
    { key: 'fallback-2', color: 'bg-amber-warning', text: 'Video: Công thức Lượng giác' },
    { key: 'fallback-3', color: 'bg-emerald-success', text: 'Thử thách Giải tích nâng cao' },
  ];

  const displaySuggestions = suggestions.length > 0
    ? suggestions.map((m) => ({
        key: m.materialId,
        color: getBulletColor(m.officialPoint),
        text: m.title,
      }))
    : FALLBACK_SUGGESTIONS;

  return (
    <footer className="flex flex-col md:flex-row gap-6">
      {/* CTA card */}
      <div className="flex-1 bg-primary text-white rounded-xl p-6 flex items-center justify-between overflow-hidden relative">
        <div className="relative z-10">
          <h4 className="text-xl font-semibold mb-2">Cải thiện ngay kết quả</h4>
          <p className="text-sm opacity-90 max-w-md">
            Chúng tôi đã thiết kế một lộ trình học tập cá nhân hóa dựa trên các chuyên đề bạn cần
            bổ sung kiến thức.
          </p>
          <button className="mt-4 bg-white text-primary px-6 py-2.5 rounded-lg font-bold hover:bg-primary-fixed transition-colors active:scale-95">
            Bắt đầu lộ trình
          </button>
        </div>
        <div className="absolute right-[-20px] bottom-[-20px] opacity-10">
          <MaterialIcon name="auto_awesome" size={160} />
        </div>
      </div>

      {/* Suggestions card */}
      <div className="w-full md:w-[350px] bg-pure-surface border border-whisper-border rounded-xl p-6">
        <h4 className="text-lg font-semibold text-on-surface mb-4">Gợi ý ôn tập</h4>
        <div className="space-y-4">
          {displaySuggestions.map((s) => (
            <div key={s.key} className="flex items-center gap-3">
              <div className={`w-2 h-2 rounded-full ${s.color} flex-shrink-0`} />
              <p className="text-sm text-on-surface">{s.text}</p>
            </div>
          ))}
        </div>
        <button className="w-full mt-6 text-primary font-bold text-center text-sm hover:underline">
          Xem tất cả gợi ý
        </button>
      </div>
    </footer>
  );
}

