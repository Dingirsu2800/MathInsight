/**
 * PracticeSetupPanel — UI cho chế độ Luyện tập tự do.
 *
 * Luồng:
 * 1. Fetch danh sách tag topic từ questionBankApi.getTopicTags()
 * 2. Fetch điểm năng lực từ recommenderApi.getWeakTags() (trả toàn bộ tag + officialPoint)
 * 3. Học sinh chọn 1 tag → hiển thị thanh điểm pTag + mức câu gợi ý
 * 4. Nút "Bắt đầu luyện tập" → gọi startPracticeSession (tạm giữ disabled nếu chưa có API)
 */
import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { questionBankApi } from '../../services/questionBankApi';
import { getWeakTags } from '../../services/recommenderApi';
import { startPracticeSession } from '../../services/testingApi';
import { Button } from '../ui/button';

/* ── helpers ─────────────────────────────────────────────── */

/**
 * Gợi ý mức câu hỏi dựa trên officialPoint (0–10).
 * Nếu học sinh chưa làm bài (numberDone === 0) → Bắt đầu từ Nhận biết.
 */
function suggestDifficulty(score, numberDone = 0) {
  if (numberDone === 0 || score === null || score === undefined) {
    return { label: 'Nhận biết', value: 'Easy', color: 'text-emerald-success', bg: 'bg-emerald-success/15 border-emerald-success/30', icon: 'school' };
  }
  if (score < 4) {
    return { label: 'Nhận biết', value: 'Easy', color: 'text-emerald-success', bg: 'bg-emerald-success/15 border-emerald-success/30', icon: 'school' };
  }
  if (score < 7) {
    return { label: 'Thông hiểu', value: 'Medium', color: 'text-amber-warning', bg: 'bg-amber-warning/10 border-amber-warning/30', icon: 'psychology' };
  }
  if (score < 9) {
    return { label: 'Vận dụng', value: 'Hard', color: 'text-primary', bg: 'bg-primary/10 border-primary/20', icon: 'bolt' };
  }
  return { label: 'Vận dụng cao', value: 'Expert', color: 'text-deep-rose', bg: 'bg-deep-rose/10 border-deep-rose/20', icon: 'military_tech' };
}

function getScoreColor(score) {
  if (score === null || score === undefined) return 'text-on-surface-variant';
  if (score < 4) return 'text-error';
  if (score < 7) return 'text-amber-warning';
  if (score < 9) return 'text-primary';
  return 'text-emerald-success';
}

function getBarColor(score) {
  if (score === null || score === undefined) return 'bg-surface-container-high';
  if (score < 4) return 'bg-error';
  if (score < 7) return 'bg-amber-warning';
  if (score < 9) return 'bg-primary';
  return 'bg-emerald-success';
}

/* ── main component ──────────────────────────────────────── */

export default function PracticeSetupPanel() {
  const navigate = useNavigate();

  // Tag list (for selection)
  const [tags, setTags] = useState([]);
  const [tagsLoading, setTagsLoading] = useState(true);
  const [tagsError, setTagsError] = useState('');

  // Score map: tagId → { officialPoint, numberDone }
  const [scoreMap, setScoreMap] = useState({});
  const [scoresLoading, setScoresLoading] = useState(true);

  // Selected tag
  const [selectedTagId, setSelectedTagId] = useState(null);

  // Search/filter
  const [search, setSearch] = useState('');

  // Starting session
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState('');
  const startInFlightRef = useRef(false);

  // --- Load tags ---
  useEffect(() => {
    setTagsLoading(true);
    questionBankApi
      .getTopicTags()
      .then((res) => {
        const items = res?.data?.items ?? res?.data ?? res ?? [];
        setTags(Array.isArray(items) ? items : []);
      })
      .catch(() => setTagsError('Không thể tải danh sách chủ đề. Vui lòng thử lại.'))
      .finally(() => setTagsLoading(false));
  }, []);

  // --- Load scores ---
  useEffect(() => {
    setScoresLoading(true);
    getWeakTags()
      .then((data) => {
        const map = {};
        (data || []).forEach((t) => {
          map[t.tagId] = { officialPoint: t.officialPoint, numberDone: t.numberDone ?? 0 };
        });
        setScoreMap(map);
      })
      .catch(() => { /* scores are optional — degrade gracefully */ })
      .finally(() => setScoresLoading(false));
  }, []);

  const selectedTag = tags.find((t) => t.tagId === selectedTagId) ?? null;
  const selectedScore = selectedTagId ? scoreMap[selectedTagId] : null;
  const officialPoint = selectedScore?.officialPoint ?? null;
  const numberDone = selectedScore?.numberDone ?? 0;
  const difficulty = suggestDifficulty(officialPoint, numberDone);

  const filteredTags = search.trim()
    ? tags.filter((t) => t.tagName?.toLowerCase().includes(search.trim().toLowerCase()))
    : tags;

  const handleStart = async () => {
    if (!selectedTagId || startInFlightRef.current) return;
    startInFlightRef.current = true;
    setStarting(true);
    setStartError('');
    try {
      const data = await startPracticeSession(selectedTagId, difficulty.value);
      const sessionId = data?.sessionId;
      if (sessionId) {
        navigate(`/student/test/${sessionId}`);
      } else {
        throw new Error('Không nhận được phiên luyện tập từ máy chủ.');
      }
    } catch (err) {
      const code = err?.response?.data?.code;
      if (code === 'TESTING_SESSION_ALREADY_IN_PROGRESS') {
        const existingId = err?.response?.data?.existingSessionId;
        if (existingId) {
          navigate(`/student/test/${existingId}`);
          return;
        }
      }
      setStartError(
        err?.response?.data?.message ||
          'Không thể bắt đầu luyện tập. Tính năng có thể chưa được kích hoạt trên máy chủ.'
      );
      startInFlightRef.current = false;
      setStarting(false);
    }
  };

  /* ── render ── */
  return (
    <div className="flex flex-col gap-6">
      {/* ── 1. Tag selector ── */}
      <div className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm overflow-hidden">
        <div className="p-4 border-b border-whisper-border flex items-center justify-between gap-3 flex-wrap">
          <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-primary text-[22px]">tag</span>
            <div>
              <h2 className="text-sm font-bold text-on-surface">Chọn chủ đề luyện tập</h2>
              <p className="text-xs text-on-surface-variant">Hệ thống sẽ tạo bài luyện tập phù hợp với năng lực của bạn.</p>
            </div>
          </div>
          {/* search */}
          <div className="relative w-full sm:w-56">
            <span className="material-symbols-outlined absolute left-2.5 top-1/2 -translate-y-1/2 text-on-surface-variant text-[18px] pointer-events-none">search</span>
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Tìm chủ đề..."
              className="w-full h-9 pl-8 pr-3 bg-surface-container-low border border-whisper-border rounded-lg text-xs text-on-surface focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all"
            />
          </div>
        </div>

        {/* Tag grid */}
        <div className="p-4">
          {tagsLoading ? (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
              {Array.from({ length: 8 }).map((_, i) => (
                <div key={i} className="h-12 bg-surface-container-low rounded-xl animate-pulse" />
              ))}
            </div>
          ) : tagsError ? (
            <div className="flex items-center gap-2 text-error text-xs font-semibold p-3 bg-error/10 rounded-xl border border-error/20">
              <span className="material-symbols-outlined text-[18px]">error</span>
              {tagsError}
            </div>
          ) : filteredTags.length === 0 ? (
            <p className="text-xs text-on-surface-variant text-center py-6">Không tìm thấy chủ đề nào.</p>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
              {filteredTags.map((tag) => {
                const tagScore = scoreMap[tag.tagId];
                const pt = tagScore?.officialPoint ?? null;
                const isSelected = tag.tagId === selectedTagId;
                const scoreLabel = pt !== null ? `${Number(pt).toFixed(1)}/10` : '—';
                const scoreCol = getScoreColor(pt);

                return (
                  <button
                    key={tag.tagId}
                    type="button"
                    onClick={() => {
                      setSelectedTagId(tag.tagId);
                      setStartError('');
                    }}
                    className={`flex flex-col items-start gap-1 px-3 py-2.5 rounded-xl border text-left transition-all ${
                      isSelected
                        ? 'border-primary bg-primary/8 ring-2 ring-primary/20'
                        : 'border-whisper-border hover:border-primary/50 hover:bg-surface-container-low'
                    }`}
                  >
                    <span className={`text-xs font-bold leading-tight line-clamp-2 ${isSelected ? 'text-primary' : 'text-on-surface'}`}>
                      {tag.tagName}
                    </span>
                    {!scoresLoading && (
                      <span className={`text-[10px] font-mono font-bold ${scoreCol}`}>
                        {scoreLabel}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* ── 2. Score + Suggestion Panel ── */}
      {selectedTag && (
        <div className="bg-pure-surface border border-whisper-border rounded-xl p-5 shadow-sm flex flex-col gap-4 animate-in fade-in slide-in-from-bottom-2 duration-200">
          {/* Header */}
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-primary/10 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-primary text-[22px]">auto_stories</span>
            </div>
            <div>
              <h3 className="text-sm font-bold text-on-surface">{selectedTag.tagName}</h3>
              <p className="text-xs text-on-surface-variant">Năng lực hiện tại &amp; gợi ý luyện tập</p>
            </div>
          </div>

          {/* Score bar */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-on-surface-variant">Điểm năng lực (pTag)</span>
              {scoresLoading ? (
                <div className="h-4 w-16 bg-surface-container rounded animate-pulse" />
              ) : officialPoint !== null ? (
                <span className={`text-sm font-mono font-extrabold ${getScoreColor(officialPoint)}`}>
                  {Number(officialPoint).toFixed(1)} / 10
                </span>
              ) : (
                <span className="text-xs text-on-surface-variant italic">Chưa có dữ liệu</span>
              )}
            </div>
            <div className="h-3 w-full bg-surface-container rounded-full overflow-hidden">
              {scoresLoading ? (
                <div className="h-full w-1/3 bg-surface-container-high rounded-full animate-pulse" />
              ) : (
                <div
                  className={`h-full rounded-full transition-all duration-700 ${getBarColor(officialPoint)}`}
                  style={{ width: `${((officialPoint ?? 0) / 10) * 100}%` }}
                />
              )}
            </div>
            {numberDone > 0 && !scoresLoading && (
              <p className="text-[10px] text-on-surface-variant">
                Dựa trên <strong>{numberDone}</strong> bài đã làm
              </p>
            )}
            {numberDone === 0 && !scoresLoading && (
              <p className="text-[10px] text-on-surface-variant italic">
                Bạn chưa làm bài nào cho chủ đề này — hãy thử ngay!
              </p>
            )}
          </div>

          {/* Suggested difficulty */}
          <div className={`flex items-start gap-3 p-3.5 rounded-xl border ${difficulty.bg}`}>
            <span className={`material-symbols-outlined text-[22px] shrink-0 mt-0.5 ${difficulty.color}`}>
              {difficulty.icon}
            </span>
            <div>
              <p className={`text-xs font-extrabold ${difficulty.color}`}>
                Mức câu gợi ý: {difficulty.label}
              </p>
              <p className="text-[11px] text-on-surface-variant mt-0.5 leading-relaxed">
                {difficulty.value === 'Easy' && 'Câu hỏi nhận biết — xây dựng nền tảng kiến thức cơ bản.'}
                {difficulty.value === 'Medium' && 'Câu hỏi thông hiểu — củng cố và áp dụng kiến thức đã học.'}
                {difficulty.value === 'Hard' && 'Câu hỏi vận dụng — giải quyết bài toán có tính ứng dụng cao.'}
                {difficulty.value === 'Expert' && 'Câu hỏi vận dụng cao — thách thức tư duy sáng tạo và tổng hợp.'}
              </p>
            </div>
          </div>

          {/* Error */}
          {startError && (
            <div role="alert" className="flex items-start gap-2 p-3 bg-error/10 border border-error/20 rounded-xl text-error text-xs font-semibold">
              <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">error</span>
              <p className="flex-1 leading-relaxed">{startError}</p>
            </div>
          )}

          {/* CTA */}
          <Button
            type="button"
            variant="primary"
            disabled={starting}
            onClick={handleStart}
            className="w-full h-11 font-bold justify-center text-sm"
          >
            {starting ? (
              <div className="flex items-center gap-2">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                <span>Đang tạo bài luyện tập...</span>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-[20px]">fitness_center</span>
                <span>Bắt đầu luyện tập — {difficulty.label}</span>
              </div>
            )}
          </Button>
        </div>
      )}

      {/* Placeholder khi chưa chọn tag */}
      {!selectedTag && !tagsLoading && !tagsError && (
        <div className="bg-pure-surface border border-dashed border-whisper-border rounded-xl p-10 flex flex-col items-center justify-center gap-3 text-center">
          <span className="material-symbols-outlined text-[48px] text-outline-variant">touch_app</span>
          <p className="text-sm font-bold text-on-surface">Chọn một chủ đề để bắt đầu</p>
          <p className="text-xs text-on-surface-variant max-w-xs">
            Sau khi chọn chủ đề, hệ thống sẽ hiển thị điểm năng lực của bạn và gợi ý mức câu phù hợp.
          </p>
        </div>
      )}
    </div>
  );
}
