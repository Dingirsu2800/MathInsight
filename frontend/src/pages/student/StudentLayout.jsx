import DashboardLayout from "../../components/layout/DashboardLayout";
import { studentNavItems } from "../../config/dashboardNav";
import { logout } from "../../services/auth";
import useCurrentUser from "../../hooks/useCurrentUser";

const ROLE_LABEL = "Học sinh";

// Second student shell, used by the lecture pages (the merge left two of these — see the
// note in the handover). Kept in sync with components/layout/StudentLayout on the two things
// that matter: the real account name, and a sign-out that actually ends the session.
export default function StudentLayout({ children }) {
  const { displayName, initials, profile } = useCurrentUser(ROLE_LABEL);

  const topNavItems = [
    { label: "Trang chủ", to: "/student/dashboard" },
    { label: "Cộng đồng", to: "/student/community", disabled: true }
  ];

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel={ROLE_LABEL}
      appTitle="Hệ thống Học tập & Đánh giá"
      navItems={studentNavItems}
      topNavItems={topNavItems}
      userName={displayName}
      userRoleLabel={ROLE_LABEL}
      userInitials={initials}
      userAvatarUrl={profile?.avatarUrl || null}
      profilePath="/profile"
      primaryAction={{
        label: "Làm bài tập",
        icon: "play_arrow",
        to: "/student/test"
      }}
      onLogout={logout}
    >
      {children}
    </DashboardLayout>
  );
}
