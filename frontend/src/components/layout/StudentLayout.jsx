import DashboardLayout from './DashboardLayout';
import { studentNavItems, studentTopNavItems } from '../../config/studentNav';
import { logout } from '../../services/auth';
import useCurrentUser from '../../hooks/useCurrentUser';
import GradeOnboardingModal from '../student/GradeOnboardingModal';

// UC-04 is role-agnostic and lives at "/profile" — there is no "/student/profile" route.
const PROFILE_PATH = '/profile';
const ROLE_LABEL = 'Học sinh';

/**
 * Shared layout wrapper for all Student pages.
 * Wraps DashboardLayout with student-specific navigation and branding.
 *
 * The signed-in user's real name comes from the profile endpoint via useCurrentUser; the role
 * label is only a last-resort fallback. Sign-out goes through the shared logout() (BR-10),
 * which revokes the session server-side, clears authStorage, and redirects to /login.
 */
export default function StudentLayout({ children }) {
  const { displayName, initials, profile, loading } = useCurrentUser(ROLE_LABEL);

  const isMissingGrade = !loading && profile && profile.roleName === 'Student' && !profile.student?.currentGrade;

  return (
    <>
      <DashboardLayout
        brandName="MathInsight"
        roleLabel={ROLE_LABEL}
        appTitle="Hệ thống hỗ trợ học toán"
        logoPath="/student/dashboard"
        navItems={studentNavItems}
        topNavItems={studentTopNavItems}
        userName={displayName}
        userRoleLabel={ROLE_LABEL}
        userInitials={initials}
        userAvatarUrl={profile?.avatarUrl || null}
        profilePath={PROFILE_PATH}
        primaryAction={{
          label: "🚀 Luyện tập ngay",
          icon: "rocket_launch",
          to: "/student/test",
        }}
        onLogout={logout}
      >
        {children}
      </DashboardLayout>
      
      {isMissingGrade && <GradeOnboardingModal profile={profile} />}
    </>
  );
}
