import * as React from "react";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { expertNavItems } from "../../config/dashboardNav";
import { logout } from "../../services/auth";
import useCurrentUser from "../../hooks/useCurrentUser";

export default function ExpertLayout({ children }) {
  const { displayName: profileName, initials: profileInitials, profile } = useCurrentUser("Expert");
  const topNavItems = [
    { label: "Tổng quan", to: "/expert/questions" },
    { label: "Phê duyệt", to: "/expert/reviews", disabled: true },
    { label: "Báo cáo", to: "/expert/reports", disabled: true }
  ];

  const userName = localStorage.getItem("UserName") || "Chuyên gia nội dung";
  const userInitials = userName.substring(0, 2).toUpperCase() || "CG";

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Chuyên gia nội dung"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={expertNavItems}
      topNavItems={topNavItems}
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
    >
      {children}
    </DashboardLayout>
  );
}
