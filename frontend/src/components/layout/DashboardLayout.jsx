import * as React from "react";
import { useLocation } from "react-router-dom";
import DashboardSidebar from "./DashboardSidebar";
import DashboardTopbar from "./DashboardTopbar";

export default function DashboardLayout({
  brandName = "MathInsight",
  roleLabel = "Quản trị viên",
  appTitle = "Hệ thống Quản lý Toán học",
  navItems = [],
  primaryAction,
  onLogout,
  topNavItems = [],
  userAvatarUrl,
  userName,
  userRoleLabel,
  userInitials,
  profilePath,
  onExport,
  exportLabel,
  showSidebarLogout = false,
  children
}) {
  const location = useLocation();
  const [darkMode, setDarkMode] = React.useState(() => {
    return localStorage.getItem("theme") === "dark" || 
      (!localStorage.getItem("theme") && window.matchMedia("(prefers-color-scheme: dark)").matches);
  });

  React.useEffect(() => {
    if (darkMode) {
      document.documentElement.classList.add("dark");
      localStorage.setItem("theme", "dark");
    } else {
      document.documentElement.classList.remove("dark");
      localStorage.setItem("theme", "light");
    }
  }, [darkMode]);

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-background text-on-background font-body">
      {/* Sidebar navigation panel */}
      <DashboardSidebar
        brandName={brandName}
        roleLabel={roleLabel}
        navItems={navItems}
        primaryAction={primaryAction}
        onLogout={showSidebarLogout ? onLogout : undefined}
        currentPath={location.pathname}
      />

      {/* Main viewport area */}
      <div className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
        {/* Top header bar */}
        <DashboardTopbar
          appTitle={appTitle}
          darkMode={darkMode}
          setDarkMode={setDarkMode}
          topNavItems={topNavItems}
          userAvatarUrl={userAvatarUrl}
          userName={userName}
          userRoleLabel={userRoleLabel}
          userInitials={userInitials}
          profilePath={profilePath}
          onLogout={onLogout}
          onExport={onExport}
          exportLabel={exportLabel}
        />

        {/* Dynamic page content container */}
        <main className="flex-1 overflow-y-auto bg-canvas-white">
          {children}
        </main>
      </div>
    </div>
  );
}
