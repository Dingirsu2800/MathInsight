import * as React from "react";
import { useLocation } from "react-router-dom";
import DashboardSidebar from "./DashboardSidebar";
import DashboardTopbar from "./DashboardTopbar";
import { useNavigationGuard } from "../../contexts/NavigationGuardContext";

function DashboardLayoutShell({
  brandName = "MathInsight",
  roleLabel = "Quản trị viên",
  appTitle = "Hệ thống Quản lý Toán học",
  logoPath,
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
  showThemeToggle = true,
  showNotifications = true,
  children
}) {
  const location = useLocation();
  const { confirmNavigation } = useNavigationGuard();
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
        logoPath={logoPath}
        navItems={navItems}
        primaryAction={primaryAction}
        onLogout={showSidebarLogout ? onLogout : undefined}
        currentPath={location.pathname}
        profilePath={profilePath}
        userName={userName}
        userInitials={userInitials}
        onBeforeNavigate={confirmNavigation}
      />

      {/* Main viewport area */}
      <div className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
        {/* Top header bar */}
        <DashboardTopbar
          appTitle={appTitle}
          logoPath={logoPath}
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
          showThemeToggle={showThemeToggle}
          showNotifications={showNotifications}
          onBeforeNavigate={confirmNavigation}
        />

        {/* Dynamic page content container */}
        <main className="flex-1 overflow-y-auto bg-canvas-white">
          {children}
        </main>
      </div>
    </div>
  );
}

export default DashboardLayoutShell;
