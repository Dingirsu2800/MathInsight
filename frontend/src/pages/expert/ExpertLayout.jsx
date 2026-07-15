import * as React from "react";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { expertNavItems } from "../../config/dashboardNav";
import { logout } from "../../services/auth";

export default function ExpertLayout({ children }) {
  const topNavItems = [
    { label: "Tổng quan", to: "/expert/questions" },
    { label: "Phê duyệt", to: "/expert/reviews", disabled: true },
    { label: "Báo cáo", to: "/expert/reports", disabled: true }
  ];

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Chuyên gia nội dung"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={expertNavItems}
      topNavItems={topNavItems}
      userName="Chuyên gia nội dung"
      userRoleLabel="Expert"
      userInitials="CG"
      profilePath="/expert/profile"
      primaryAction={{
        label: "Tạo câu hỏi mới",
        icon: "add",
        to: "/expert/questions/new"
      }}
      onLogout={logout}
    >
      {children}
    </DashboardLayout>
  );
}
