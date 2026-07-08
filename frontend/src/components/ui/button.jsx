import * as React from "react";
import { cn } from "../../utils/cn";

export const Button = React.forwardRef(({ className, variant = "primary", size = "default", ...props }, ref) => {
  return (
    <button
      ref={ref}
      className={cn(
        "inline-flex items-center justify-center rounded-lg font-bold transition-all duration-200 active:scale-95 disabled:opacity-50 disabled:pointer-events-none cursor-pointer",
        {
          "bg-primary text-on-primary hover:bg-primary/90 shadow-sm": variant === "primary",
          "bg-secondary-container text-on-secondary-container hover:bg-secondary-container/85": variant === "secondary",
          "border border-outline bg-transparent text-on-surface hover:bg-surface-container": variant === "outline",
          "hover:bg-surface-container text-on-surface-variant": variant === "ghost",
          "bg-error text-on-error hover:bg-error/90": variant === "destructive",
        },
        {
          "px-4 py-2 text-[14px]": size === "default",
          "px-3 py-1.5 text-[12px]": size === "sm",
          "px-4 py-3 text-[16px]": size === "lg",
        },
        className
      )}
      {...props}
    />
  );
});
Button.displayName = "Button";
