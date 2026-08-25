import * as React from "react";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { expertNavItems } from "../../config/dashboardNav";
import { logout } from "../../services/auth";
import useCurrentUser from "../../hooks/useCurrentUser";

export default function ExpertLayout({ children }) {
  const { displayName: profileName, initials: profileInitials, profile } = useCurrentUser("Expert");

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Chuyên gia nội dung"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={expertNavItems}
      userName={profileName}
      userRoleLabel="Expert"
      userInitials={profileInitials}
      userAvatarUrl={profile?.avatarUrl || null}
      profilePath="/expert/profile"
      primaryAction={{
        label: "Tạo câu hỏi mới",
        icon: "add",
        to: "/expert/questions/new"
      }}
      onLogout={logout}
      showSidebarLogout
      showThemeToggle={false}
      showNotifications={false}
      hideTopbar={true}
    >
      {children}
    </DashboardLayout>
  );
}
