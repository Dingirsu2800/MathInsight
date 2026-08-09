import { useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import { getRecommendedMaterials } from '../../../services/recommenderApi';

/** Derive bullet color from officialPoint. */
function getBulletColor(officialPoint) {
  const score = Number(officialPoint);
  if (score < 5) return 'bg-deep-rose';
  if (score < 7.5) return 'bg-amber-warning';
  return 'bg-emerald-success';
}

function isSafeMaterialUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === 'https:' || url.protocol === 'http:';
  } catch {
    return false;
  }
}

/**
 * Improvement call-to-action and personalized material suggestions.
 * Materials are returned by GET /api/v1/recommender/materials (UC-54).
 */
export default function ImprovementCTACard() {
  const [suggestions, setSuggestions] = useState([]);
  const [materialStatus, setMaterialStatus] = useState('loading');

  useEffect(() => {
    let cancelled = false;

    getRecommendedMaterials()
      .then((data) => {
        if (cancelled) return;

        const materials = Array.isArray(data) ? data : [];
        const remedial = materials.filter((material) => material.isRemedial);
        setSuggestions((remedial.length > 0 ? remedial : materials).slice(0, 3));
        setMaterialStatus('ready');
      })
      .catch(() => {
        if (!cancelled) setMaterialStatus('error');
      });

    return () => { cancelled = true; };
  }, []);

  return (
    <footer className="flex flex-col md:flex-row gap-6">
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

      <div className="w-full md:w-[350px] bg-pure-surface border border-whisper-border rounded-xl p-6">
        <h4 className="text-lg font-semibold text-on-surface mb-4">Gợi ý ôn tập</h4>
        <div className="space-y-4">
          {materialStatus === 'loading' && (
            <p className="text-sm text-on-surface-variant">Đang tải tài liệu gợi ý...</p>
          )}
          {materialStatus === 'error' && (
            <p data-testid="recommendation-materials-error" className="text-sm text-on-surface-variant">
              Không thể tải tài liệu gợi ý. Vui lòng thử lại sau.
            </p>
          )}
          {materialStatus === 'ready' && suggestions.length === 0 && (
            <p data-testid="recommendation-materials-empty" className="text-sm text-on-surface-variant">
              Chưa có tài liệu phù hợp cho các chủ đề cần ôn tập.
            </p>
          )}
          {materialStatus === 'ready' && suggestions.map((suggestion) => (
            <div key={suggestion.materialId} className="flex items-center gap-3">
              <div className={`w-2 h-2 rounded-full ${getBulletColor(suggestion.officialPoint)} flex-shrink-0`} />
              {isSafeMaterialUrl(suggestion.fileUrl) ? (
                <a
                  className="text-sm text-primary hover:underline"
                  href={suggestion.fileUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {suggestion.title}
                </a>
              ) : (
                <p className="text-sm text-on-surface">{suggestion.title}</p>
              )}
            </div>
          ))}
        </div>
      </div>
    </footer>
  );
}
