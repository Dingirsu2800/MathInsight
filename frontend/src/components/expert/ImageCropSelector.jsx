import React from "react";

function clamp(value) {
  return Math.min(1, Math.max(0, value));
}

export default function ImageCropSelector({ sourceUrl, selection, onSelectionChange, disabled }) {
  const containerRef = React.useRef(null);
  const dragStartRef = React.useRef(null);
  const [localSelection, setLocalSelection] = React.useState(selection);

  React.useEffect(() => setLocalSelection(selection), [selection, sourceUrl]);

  const getPointerPosition = (event) => {
    const bounds = containerRef.current?.getBoundingClientRect();
    if (!bounds) return null;
    return {
      x: clamp((event.clientX - bounds.left) / bounds.width),
      y: clamp((event.clientY - bounds.top) / bounds.height)
    };
  };

  const updateSelection = (end) => {
    const start = dragStartRef.current;
    if (!start || !end) return null;
    return {
      x: Math.min(start.x, end.x),
      y: Math.min(start.y, end.y),
      width: Math.abs(end.x - start.x),
      height: Math.abs(end.y - start.y)
    };
  };

  const handlePointerDown = (event) => {
    if (disabled) return;
    const start = getPointerPosition(event);
    if (!start) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    dragStartRef.current = start;
    setLocalSelection({ x: start.x, y: start.y, width: 0, height: 0 });
  };

  const handlePointerMove = (event) => {
    if (!dragStartRef.current) return;
    const next = updateSelection(getPointerPosition(event));
    if (next) setLocalSelection(next);
  };

  const handlePointerUp = (event) => {
    if (!dragStartRef.current) return;
    const next = updateSelection(getPointerPosition(event));
    dragStartRef.current = null;
    if (!next || next.width < 0.03 || next.height < 0.03) {
      setLocalSelection(null);
      onSelectionChange(null);
      return;
    }
    setLocalSelection(next);
    onSelectionChange(next);
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <div>
          <p className="text-xs font-bold text-on-surface">Khoanh đúng một câu trước khi quét</p>
          <p className="text-[10px] text-on-surface-variant">Kéo từ góc này đến góc đối diện của câu hỏi.</p>
        </div>
        {localSelection && (
          <button
            type="button"
            onClick={() => {
              setLocalSelection(null);
              onSelectionChange(null);
            }}
            disabled={disabled}
            className="text-[10px] font-bold text-primary hover:underline cursor-pointer disabled:cursor-not-allowed"
          >
            Xóa vùng chọn
          </button>
        )}
      </div>
      <div
        ref={containerRef}
        className="relative overflow-hidden rounded-lg border border-dashed border-primary/50 bg-surface-container-low touch-none"
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerCancel={handlePointerUp}
      >
        <img src={sourceUrl} alt="Chọn vùng ảnh để OCR" draggable={false} className="block w-full select-none" />
        {localSelection && (
          <div
            className="pointer-events-none absolute border-2 border-primary bg-primary/15"
            style={{
              left: `${localSelection.x * 100}%`,
              top: `${localSelection.y * 100}%`,
              width: `${localSelection.width * 100}%`,
              height: `${localSelection.height * 100}%`
            }}
          />
        )}
      </div>
    </div>
  );
}
