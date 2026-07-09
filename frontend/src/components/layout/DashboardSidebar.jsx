import * as React from "react";
import { Link, useNavigate } from "react-router-dom";
import { cn } from "../../utils/cn";

export default function DashboardSidebar({
  brandName = "MathInsight",
  roleLabel = "Quản trị viên",
  navItems = [],
  primaryAction,
  onLogout,
  currentPath = ""
}) {
  const navigate = useNavigate();
  const brandInitials = brandName ? brandName.charAt(0) : "M";

  return (
    <aside className="w-sidebar-width h-screen bg-surface-container-low border-r border-whisper-border flex flex-col py-gutter px-4 z-20 shrink-0 select-none">
      {/* Brand */}
      <div className="flex items-center gap-3 mb-8 px-2">
        <div className="w-10 h-10 rounded-lg bg-primary-container text-on-primary-container flex items-center justify-center font-headline-md font-bold text-lg">
          {brandInitials}
        </div>
        <div>
          <h1 className="font-bold text-[18px] text-primary leading-tight">{brandName}</h1>
          <p className="text-[12px] text-on-surface-variant">{roleLabel}</p>
        </div>
      </div>

      {/* Primary Action Button (CTA) */}
      {primaryAction && (
        <button
          onClick={() => {
            if (primaryAction.onClick) {
              primaryAction.onClick();
            } else if (primaryAction.to) {
              navigate(primaryAction.to);
            }
          }}
          className="w-full mb-8 bg-primary text-on-primary py-3 px-4 rounded-lg font-bold text-xs uppercase tracking-wider flex items-center justify-center gap-2 hover:bg-primary/90 transition-all active:translate-y-px cursor-pointer border-0 outline-none"
        >
          {primaryAction.icon && (
            <span className="material-symbols-outlined text-[18px]">
              {primaryAction.icon}
            </span>
          )}
          {primaryAction.label}
        </button>
      )}

      {/* Navigation Links */}
      <nav className="flex-1 flex flex-col gap-1 overflow-y-auto">
        {navItems.map((item) => {
          const isActive = currentPath.startsWith(item.path);
          if (item.disabled) {
            return (
              <div
                key={item.path || item.label}
                className="flex items-center gap-3 px-3 py-3 rounded-lg text-on-surface-variant/40 cursor-not-allowed select-none"
                title="Tính năng chưa khả dụng"
              >
                <span className="material-symbols-outlined opacity-50">
                  {item.icon}
                </span>
                <span className="text-[15px]">{item.label}</span>
              </div>
            );
          }

          return (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                "flex items-center gap-3 px-3 py-3 rounded-lg transition-all active:-translate-y-px",
                isActive
                  ? "text-primary font-bold bg-surface-container-high"
                  : "text-on-surface-variant hover:bg-surface-container"
              )}
            >
              <span
                className="material-symbols-outlined"
                style={{ fontVariationSettings: isActive ? "'FILL' 1" : "'FILL' 0" }}
              >
                {item.icon}
              </span>
              <span className="text-[15px]">{item.label}</span>
            </Link>
          );
        })}
      </nav>

      {/* Sidebar Footer Link & Logout */}
      <div className="mt-auto flex flex-col gap-1 border-t border-whisper-border pt-4">
        <a
          href="#"
          onClick={(e) => e.preventDefault()}
          className="flex items-center gap-3 px-3 py-2 rounded-lg text-on-surface-variant hover:bg-surface-container transition-colors"
        >
          <span className="material-symbols-outlined">help</span>
          <span className="text-[14px]">Trợ giúp</span>
        </a>
        {onLogout && (
          <button
            onClick={onLogout}
            className="flex items-center gap-3 px-3 py-2 rounded-lg text-error hover:bg-error/10 transition-colors w-full text-left cursor-pointer border-0 bg-transparent outline-none font-body"
          >
            <span className="material-symbols-outlined">logout</span>
            <span className="text-[14px] font-bold">Đăng xuất</span>
          </button>
        )}
      </div>
    </aside>
  );
}
