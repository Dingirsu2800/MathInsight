import * as React from "react";
import { cn } from "../../utils/cn";

export function Badge({ className, variant = "default", ...props }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 font-bold text-[11px] tracking-wider uppercase px-2.5 py-0.5 rounded-full border transition-colors",
        {
          "text-emerald-success bg-emerald-success/10 border-emerald-success/20": variant === "success" || variant === "APPROVED" || variant === "approved",
          "text-error bg-error-container/40 border-error/20": variant === "error" || variant === "reported" || variant === "REPORTED" || variant === "rejected" || variant === "REJECTED",
          "text-amber-warning bg-amber-warning/10 border-amber-warning/20": variant === "warning" || variant === "warn",
          "text-on-surface-variant bg-surface-dim border-whisper-border": variant === "secondary" || variant === "deactivated" || variant === "DEACTIVATED",
          "text-primary bg-primary/10 border-primary/20": variant === "primary",
          "text-on-surface bg-surface border-whisper-border": variant === "outline" || variant === "default",
        },
        className
      )}
      {...props}
    />
  );
}
