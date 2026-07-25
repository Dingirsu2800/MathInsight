import * as React from "react";

export default function DashboardPageHeader({ title, subtitle, children }) {
  return (
    <div className="flex justify-between items-center">
      <div>
        <h1 className="text-2xl lg:text-3xl font-bold text-on-background">{title}</h1>
        {subtitle && (
          <p className="text-sm text-on-surface-variant mt-1">{subtitle}</p>
        )}
      </div>
      {children && <div className="flex items-center gap-3">{children}</div>}
    </div>
  );
}
