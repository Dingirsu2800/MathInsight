import { useEffect, useMemo, useState } from 'react';
import StudentLayout from '../../components/layout/StudentLayout';
import MaterialIcon from '../../components/ui/MaterialIcon';
import ProgressBar from '../../components/ui/ProgressBar';
import { getTargets, createTarget, updateTarget } from '../../services/gamificationApi';
import { getWeakTags } from '../../services/recommenderApi';

export default function TargetScorePage() {
  const [targets, setTargets] = useState([]);
  const [weakTags, setWeakTags] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedTagId, setSelectedTagId] = useState('');
  const [newTargetPoint, setNewTargetPoint] = useState(7);
  const [editingTargetId, setEditingTargetId] = useState(null);
  const [editingPoint, setEditingPoint] = useState(0);
  const [error, setError] = useState('');

  async function reload() {
    const [targetList, tagList] = await Promise.all([getTargets(), getWeakTags()]);
    setTargets(targetList || []);
    setWeakTags(tagList || []);
  }

  useEffect(() => {
    reload().finally(() => setLoading(false));
  }, []);

  const targetedTagIds = useMemo(() => new Set(targets.map((t) => t.tagId)), [targets]);
  const availableTags = weakTags.filter((tag) => !targetedTagIds.has(tag.tagId));

  async function handleCreate(event) {
    event.preventDefault();
    setError('');
    if (!selectedTagId) return;

    try {
      await createTarget(selectedTagId, Number(newTargetPoint));
      setSelectedTagId('');
      setNewTargetPoint(7);
      await reload();
    } catch (err) {
      setError(err?.response?.data?.error || 'Không thể tạo mục tiêu. Chủ đề này có thể đã có mục tiêu.');
    }
  }

  function startEdit(target) {
    setEditingTargetId(target.targetId);
    setEditingPoint(target.targetPoint);
  }

  async function handleUpdate(targetId) {
    setError('');
    try {
      await updateTarget(targetId, Number(editingPoint));
      setEditingTargetId(null);
      await reload();
    } catch (err) {
      setError(err?.response?.data?.error || 'Không thể cập nhật mục tiêu.');
    }
  }

  return (
    <StudentLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-on-surface flex items-center gap-2">
            <MaterialIcon name="ads_click" className="text-primary" />
            Mục tiêu điểm số
          </h1>
          <p className="text-on-surface-variant text-sm mt-1">
            Đặt điểm mục tiêu (0-10) cho từng chủ đề để theo dõi tiến độ học tập.
          </p>
        </div>

        {error && (
          <div className="bg-error/10 text-error text-sm rounded-lg px-4 py-2">{error}</div>
        )}

        <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
          <h2 className="font-semibold text-on-surface mb-4">Đặt mục tiêu mới</h2>
          <form onSubmit={handleCreate} className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-[220px]">
              <label className="text-xs text-on-surface-variant block mb-1">Chủ đề</label>
              <select
                value={selectedTagId}
                onChange={(e) => setSelectedTagId(e.target.value)}
                className="w-full border border-whisper-border rounded-lg px-3 py-2 text-sm bg-transparent"
              >
                <option value="">-- Chọn chủ đề --</option>
                {availableTags.map((tag) => (
                  <option key={tag.tagId} value={tag.tagId}>
                    {tag.tagName} (hiện tại {tag.officialPoint}/10)
                  </option>
                ))}
              </select>
            </div>
            <div className="w-28">
              <label className="text-xs text-on-surface-variant block mb-1">Mục tiêu</label>
              <input
                type="number"
                min={0}
                max={10}
                step="0.5"
                value={newTargetPoint}
                onChange={(e) => setNewTargetPoint(e.target.value)}
                className="w-full border border-whisper-border rounded-lg px-3 py-2 text-sm bg-transparent"
              />
            </div>
            <button
              type="submit"
              disabled={!selectedTagId}
              className="px-4 py-2 rounded-lg bg-primary text-white text-sm font-medium disabled:opacity-50"
            >
              Thêm mục tiêu
            </button>
          </form>
        </div>

        <div className="bg-pure-surface border border-whisper-border rounded-2xl p-6 shadow-sm">
          <h2 className="font-semibold text-on-surface mb-4">Mục tiêu hiện tại</h2>

          {loading ? (
            <p className="text-sm text-on-surface-variant">Đang tải...</p>
          ) : targets.length === 0 ? (
            <p className="text-sm text-on-surface-variant">Bạn chưa đặt mục tiêu nào.</p>
          ) : (
            <div className="space-y-5">
              {targets.map((target) => (
                <div key={target.targetId} className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg bg-surface-container-low flex items-center justify-center flex-shrink-0">
                    <MaterialIcon
                      name={target.isAchieved ? 'check_circle' : 'flag'}
                      size={20}
                      className="text-primary"
                    />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex justify-between text-sm mb-1.5 items-center">
                      <span className="text-on-surface font-medium truncate">{target.tagName}</span>
                      {editingTargetId === target.targetId ? (
                        <span className="flex items-center gap-2">
                          <input
                            type="number"
                            min={0}
                            max={10}
                            step="0.5"
                            value={editingPoint}
                            onChange={(e) => setEditingPoint(e.target.value)}
                            className="w-16 border border-whisper-border rounded px-2 py-1 text-xs"
                          />
                          <button
                            onClick={() => handleUpdate(target.targetId)}
                            className="text-xs text-primary font-medium"
                          >
                            Lưu
                          </button>
                        </span>
                      ) : (
                        <button
                          onClick={() => startEdit(target)}
                          className="text-primary font-bold hover:underline"
                        >
                          {target.currentPoint}/{target.targetPoint}
                        </button>
                      )}
                    </div>
                    <ProgressBar value={target.currentPoint} max={target.targetPoint} height="h-1.5" />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </StudentLayout>
  );
}
