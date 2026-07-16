import { useNavigate } from 'react-router-dom';
import DashboardLayout from './DashboardLayout';
import { studentNavItems, studentTopNavItems } from '../../config/studentNav';
import { logout, getStoredUser } from '../../services/authApi';

/**
 * Shared layout wrapper for all Student pages.
 * Wraps DashboardLayout with student-specific navigation and branding.
 * Reads real user info from localStorage (populated during login).
 */
export default function StudentLayout({ children }) {
  const navigate = useNavigate();
  const user = getStoredUser();

  const userName = user?.username || 'Học sinh';
  const initials = userName
    .split(/\s+/)
    .filter(Boolean)
    .map((w) => w.charAt(0))
    .slice(0, 2)
    .join('')
    .toUpperCase() || 'HS';

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Học sinh"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={studentNavItems}
      topNavItems={studentTopNavItems}
      userName={userName}
      userRoleLabel="Học sinh"
      userInitials={initials}
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
