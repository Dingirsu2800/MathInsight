import * as React from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { cn } from "../../utils/cn";

export default function DashboardTopbar({
  appTitle = "Hệ thống Quản lý Toán học",
  darkMode,
  setDarkMode,
  topNavItems = [],
  userAvatarUrl,
  userName,
  userRoleLabel,
  userInitials,
  profilePath,
  onLogout,
  onExport,
  exportLabel = "Xuất dữ liệu"
}) {
  const location = useLocation();
  const navigate = useNavigate();
  const [isAccountMenuOpen, setIsAccountMenuOpen] = React.useState(false);
  const menuRef = React.useRef(null);

  React.useEffect(() => {
    const handleClickOutside = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsAccountMenuOpen(false);
      }
    };

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        setIsAccountMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, []);

  const getInitials = () => {
    if (userInitials) return userInitials;
    if (userName) {
      const parts = userName.trim().split(/\s+/);
      if (parts.length >= 2) {
        return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
      }
      if (parts.length === 1 && parts[0]) {
        return parts[0].substring(0, 2).toUpperCase();
      }
    }
    return "MI";
  };

  const resolvedInitials = getInitials();

  return (
    // z-50: the sticky header creates its own stacking context, so the account dropdown can
    // never paint above a sibling with a HIGHER z-index no matter what z the dropdown itself
    // uses. Page content sets `relative z-10` (e.g. the dashboard score card) and, sitting
    // later in the DOM, used to tie and win — which is what clipped the menu. Keep this above
    // any z-index used inside <main>.
    <header className="h-header-height border-b border-whisper-border bg-surface flex justify-between items-center w-full px-gutter z-50 sticky top-0 shrink-0 select-none">
      <div className="flex items-center gap-8 h-full">
        <h2 className="text-[20px] font-bold text-primary">{appTitle}</h2>
        {topNavItems.length > 0 && (
          <nav className="hidden md:flex gap-6 h-full items-center">
            {topNavItems.map((item, idx) => {
              const isActive = item.isActive !== undefined 
                ? item.isActive 
                : (item.to && location.pathname.startsWith(item.to));

              if (item.disabled) {
                return (
                  <span
                    key={idx}
                    className="text-[14px] text-on-surface-variant/40 cursor-not-allowed select-none font-bold"
                    title="Tính năng chưa khả dụng"
                  >
                    {item.label}
                  </span>
                );
              }
              return (
                <Link
                  key={idx}
                  to={item.to || "#"}
                  className={cn(
                    "text-[14px] transition-all pb-1 font-bold h-full flex items-center pt-1 hover:text-primary active:scale-95",
                    isActive
                      ? "text-primary border-b-2 border-primary"
                      : "text-on-surface-variant hover:text-primary"
                  )}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
        )}
      </div>

      {/* Right: Actions */}
      <div className="flex items-center gap-4">
        {onExport && (
          <button 
            onClick={onExport}
            className="text-[13px] text-primary border border-primary px-4 py-2 rounded flex items-center gap-2 hover:bg-primary/5 transition-colors cursor-pointer font-bold active:scale-[0.98] transition-all"
          >
            <span className="material-symbols-outlined text-[18px]">download</span>
            {exportLabel}
          </button>
        )}
        <div className="flex items-center gap-2 border-l border-whisper-border pl-4">
          <button
            type="button"
            className="p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer border-0 bg-transparent outline-none"
            aria-label="Thông báo"
          >
            <span className="material-symbols-outlined">notifications</span>
          </button>
          <button
            type="button"
            onClick={() => setDarkMode(!darkMode)}
            className="p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer border-0 bg-transparent outline-none"
            aria-label={darkMode ? "Chuyển sang chế độ sáng" : "Chuyển sang chế độ tối"}
          >
            <span className="material-symbols-outlined">
              {darkMode ? "light_mode" : "dark_mode"}
            </span>
          </button>
          
          {/* Avatar / Account Dropdown Button container wrapped with menuRef */}
          <div className="relative flex items-center" ref={menuRef}>
            <button
              type="button"
              onClick={() => setIsAccountMenuOpen(!isAccountMenuOpen)}
              className="flex items-center justify-center p-0.5 rounded-full border border-whisper-border hover:border-primary/50 transition-colors cursor-pointer outline-none bg-transparent"
              aria-label="Menu tài khoản"
              aria-expanded={isAccountMenuOpen}
            >
              {userAvatarUrl ? (
                <img
                  alt="Người dùng"
                  className="w-8 h-8 rounded-full object-cover"
                  src={userAvatarUrl}
                />
              ) : (
                <div className="w-8 h-8 rounded-full bg-primary/10 border border-primary/20 text-primary flex items-center justify-center font-bold text-xs select-none">
                  {resolvedInitials}
                </div>
              )}
            </button>

            {/* Account Dropdown Menu */}
            {isAccountMenuOpen && (
              <div className="absolute right-0 top-full mt-2 w-56 rounded-xl bg-pure-surface border border-whisper-border diffused-shadow p-2 z-[60] flex flex-col gap-1 text-[13px] animate-in fade-in-0 slide-in-from-top-2 duration-150">
                <div className="px-3 py-2 border-b border-whisper-border">
                  <p className="font-bold text-on-surface truncate">{userName || "Người dùng"}</p>
                  <p className="text-[11px] text-on-surface-variant truncate font-semibold uppercase tracking-wider">{userRoleLabel || "Tài khoản"}</p>
                </div>

                {profilePath && (
                  <button
                    type="button"
                    onClick={() => {
                      setIsAccountMenuOpen(false);
                      navigate(profilePath);
                    }}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg text-on-surface hover:bg-surface-container transition-colors w-full text-left cursor-pointer border-0 bg-transparent outline-none font-bold"
                  >
                    <span className="material-symbols-outlined text-[18px]">person</span>
                    Hồ sơ cá nhân
                  </button>
                )}

                {onLogout && (
                  <button
                    type="button"
                    onClick={() => {
                      setIsAccountMenuOpen(false);
                      onLogout();
                    }}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg text-error hover:bg-error/10 transition-colors w-full text-left cursor-pointer border-0 bg-transparent outline-none font-bold"
                  >
                    <span className="material-symbols-outlined text-[18px]">logout</span>
                    Đăng xuất
                  </button>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
