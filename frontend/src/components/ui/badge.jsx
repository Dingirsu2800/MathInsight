import * as React from "react";
import { cn } from "../../utils/cn";

export function Badge({ className, variant = "default", ...props }) {
  const normalizedVariant = typeof variant === "string"
    ? variant.trim().toUpperCase()
    : variant;

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 font-bold text-[11px] tracking-wider uppercase px-2.5 py-0.5 rounded-full border transition-colors",
        {
          "text-emerald-success bg-emerald-success/10 border-emerald-success/20": normalizedVariant === "SUCCESS" || normalizedVariant === "APPROVED",
          "text-error bg-error-container/40 border-error/20": normalizedVariant === "ERROR" || normalizedVariant === "REPORTED" || normalizedVariant === "REJECTED",
          "text-amber-warning bg-amber-warning/10 border-amber-warning/20": normalizedVariant === "WARNING" || normalizedVariant === "WARN" || normalizedVariant === "PENDING",
          "text-on-surface-variant bg-surface-dim border-whisper-border": normalizedVariant === "SECONDARY" || normalizedVariant === "DEACTIVATED",
          "text-primary bg-primary/10 border-primary/20": normalizedVariant === "PRIMARY",
          "text-on-surface bg-surface border-whisper-border": normalizedVariant === "OUTLINE" || normalizedVariant === "DEFAULT",
        },
        className
      )}
      {...props}
    />
  );
}
