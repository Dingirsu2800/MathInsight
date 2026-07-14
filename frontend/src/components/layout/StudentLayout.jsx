import DashboardLayout from './DashboardLayout';
import { studentNavItems, studentTopNavItems } from '../../config/studentNav';

/**
 * Shared layout wrapper for all Student pages.
 * Wraps DashboardLayout with student-specific navigation and branding.
 */
export default function StudentLayout({ children }) {
  // TODO: Replace with auth context values
  const handleLogout = () => {
    localStorage.removeItem('token');
    window.location.href = '/login';
  };

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Học sinh"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={studentNavItems}
      topNavItems={studentTopNavItems}
      userName="Nguyễn Văn A"
      userRoleLabel="Học sinh"
      userInitials="NV"
      userAvatarUrl={null}
      profilePath="/student/profile"
      primaryAction={{
        label: "🚀 Luyện tập ngay",
        icon: "rocket_launch",
        to: "/student/tests",
      }}
      onLogout={handleLogout}
    >
      {children}
    </DashboardLayout>
  );
}
