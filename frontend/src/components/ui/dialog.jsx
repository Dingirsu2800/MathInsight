import * as React from "react";
import { cn } from "../../utils/cn";

export function Dialog({ isOpen, onClose, children, className, variant = "modal" }) {
  React.useEffect(() => {
    const handleEscape = (e) => {
      if (e.key === "Escape") onClose();
    };
    if (isOpen) {
      document.body.style.overflow = "hidden";
      window.addEventListener("keydown", handleEscape);
    }
    return () => {
      document.body.style.overflow = "unset";
      window.removeEventListener("keydown", handleEscape);
    };
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className={cn("fixed inset-0 z-50 flex", {
      "items-center justify-center": variant === "modal",
      "justify-end": variant === "drawer"
    })}>
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-charcoal-ink/30 backdrop-blur-[2px] transition-opacity duration-300"
        onClick={onClose}
      />
      {/* Dialog container */}
      <div
        className={cn(
          "relative z-50 bg-pure-surface border border-whisper-border p-6 flex flex-col shadow-2xl",
          {
            "w-full max-w-lg rounded-2xl diffused-shadow animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]": variant === "modal",
            "h-full w-full max-w-2xl border-l border-whisper-border animate-in slide-in-from-right duration-300": variant === "drawer"
          },
          className
        )}
      >
        {children}
        {/* Close Button */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 p-1.5 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer"
        >
          <span className="material-symbols-outlined text-[20px]">close</span>
        </button>
      </div>
    </div>
  );
}

export function DialogHeader({ className, children, ...props }) {
  return (
    <div className={cn("mb-4 flex flex-col gap-1 border-b border-whisper-border pb-3 pr-8", className)} {...props}>
      {children}
    </div>
  );
}

export function DialogTitle({ className, children, ...props }) {
  return (
    <h3 className={cn("text-headline-md font-bold text-on-background", className)} {...props}>
      {children}
    </h3>
  );
}

export function DialogDescription({ className, children, ...props }) {
  return (
    <p className={cn("text-body-sm text-on-surface-variant mt-1", className)} {...props}>
      {children}
    </p>
  );
}

export function DialogContent({ className, children, ...props }) {
  return (
    <div className={cn("flex-1 overflow-y-auto pr-1 py-2", className)} {...props}>
      {children}
    </div>
  );
}

export function DialogFooter({ className, children, ...props }) {
  return (
    <div className={cn("mt-4 pt-3 border-t border-whisper-border flex justify-end gap-3", className)} {...props}>
      {children}
    </div>
  );
}
