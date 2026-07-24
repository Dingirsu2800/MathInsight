import * as React from "react";
import { useState, useEffect, useCallback } from "react";

const ICONS = {
  success: "check_circle",
  error: "error",
  warning: "warning",
  info: "info",
};

const COLORS = {
  success: {
    bg: "bg-[#ecfdf5]",
    border: "border-[#6ee7b7]",
    icon: "text-[#059669]",
    bar: "bg-[#059669]",
  },
  error: {
    bg: "bg-[#fef2f2]",
    border: "border-[#fca5a5]",
    icon: "text-[#dc2626]",
    bar: "bg-[#dc2626]",
  },
  warning: {
    bg: "bg-[#fffbeb]",
    border: "border-[#fcd34d]",
    icon: "text-[#d97706]",
    bar: "bg-[#d97706]",
  },
  info: {
    bg: "bg-[#eff6ff]",
    border: "border-[#93c5fd]",
    icon: "text-[#2563eb]",
    bar: "bg-[#2563eb]",
  },
};

function ToastItem({ toast, onDismiss }) {
  const [exiting, setExiting] = useState(false);
  const [progress, setProgress] = useState(100);
  const colors = COLORS[toast.type] || COLORS.info;
  const icon = ICONS[toast.type] || ICONS.info;
  const duration = toast.duration || 3500;

  const dismiss = useCallback(() => {
    setExiting(true);
    setTimeout(() => onDismiss(toast.id), 350);
  }, [toast.id, onDismiss]);

  useEffect(() => {
    const startTime = Date.now();
    const interval = setInterval(() => {
      const elapsed = Date.now() - startTime;
      const remaining = Math.max(0, 100 - (elapsed / duration) * 100);
      setProgress(remaining);
      if (remaining <= 0) {
        clearInterval(interval);
        dismiss();
      }
    }, 30);
    return () => clearInterval(interval);
  }, [duration, dismiss]);

  return (
    <div
      className={`
        relative overflow-hidden flex items-start gap-3 px-5 py-4 rounded-xl border shadow-lg backdrop-blur-sm
        ${colors.bg} ${colors.border}
        transition-all duration-350 ease-out
        ${exiting
          ? "opacity-0 translate-x-[120%] scale-95"
          : "opacity-100 translate-x-0 scale-100 animate-[slideInRight_0.4s_ease-out]"
        }
      `}
      style={{ minWidth: 320, maxWidth: 440 }}
    >
      {/* Icon */}
      <span
        className={`material-symbols-outlined ${colors.icon} text-[24px] mt-0.5 shrink-0`}
        style={{ fontVariationSettings: "'FILL' 1" }}
      >
        {icon}
      </span>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className="text-[14px] font-semibold text-[#1e293b] leading-snug">{toast.message}</p>
        {toast.description && (
          <p className="text-[13px] text-[#64748b] mt-0.5 leading-snug">{toast.description}</p>
        )}
      </div>

      {/* Close button */}
      <button
        onClick={dismiss}
        className="text-[#94a3b8] hover:text-[#475569] transition-colors shrink-0 mt-0.5"
      >
        <span className="material-symbols-outlined text-[18px]">close</span>
      </button>

      {/* Progress bar */}
      <div className="absolute bottom-0 left-0 right-0 h-[3px] bg-black/5">
        <div
          className={`h-full ${colors.bar} transition-none rounded-full`}
          style={{ width: `${progress}%`, opacity: 0.6 }}
        />
      </div>
    </div>
  );
}

// ── Global Toast Manager ──────────────────────────────
let addToastGlobal = null;

export function toast(message, type = "success", options = {}) {
  if (addToastGlobal) {
    addToastGlobal({ message, type, ...options, id: Date.now() + Math.random() });
  }
}

toast.success = (message, options) => toast(message, "success", options);
toast.error = (message, options) => toast(message, "error", options);
toast.warning = (message, options) => toast(message, "warning", options);
toast.info = (message, options) => toast(message, "info", options);

export default function ToastContainer() {
  const [toasts, setToasts] = useState([]);

  useEffect(() => {
    addToastGlobal = (t) => setToasts((prev) => [...prev, t]);
    return () => { addToastGlobal = null; };
  }, []);

  const handleDismiss = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  if (toasts.length === 0) return null;

  return (
    <div className="fixed top-6 right-6 z-[9999] flex flex-col gap-3 pointer-events-auto">
      {toasts.map((t) => (
        <ToastItem key={t.id} toast={t} onDismiss={handleDismiss} />
      ))}
    </div>
  );
}
