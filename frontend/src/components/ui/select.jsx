import * as React from "react";
import { cn } from "../../utils/cn";

export const Select = React.forwardRef(({ className, children, ...props }, ref) => {
  return (
    <div className="relative inline-block w-full">
      <select
        ref={ref}
        className={cn(
          "w-full bg-pure-surface border border-whisper-border rounded-lg py-2 pl-3 pr-10 font-body-sm text-body-sm text-on-surface focus:ring-2 focus:ring-primary focus:border-primary outline-none hover:bg-surface-container-low transition-colors appearance-none cursor-pointer",
          className
        )}
        {...props}
      >
        {children}
      </select>
      <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3 text-on-surface-variant">
        <span className="material-symbols-outlined text-[18px]">keyboard_arrow_down</span>
      </div>
    </div>
  );
});
Select.displayName = "Select";
