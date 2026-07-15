import * as React from "react";
import { useNavigate } from "react-router-dom";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { studentNavItems } from "../../config/dashboardNav";

export default function StudentLayout({ children }) {
  const navigate = useNavigate();

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("access_token");
    localStorage.removeItem("AccountId");
    localStorage.removeItem("RoleName");
    localStorage.removeItem("UserName");
    navigate("/login");
  };

  const topNavItems = [
    { label: "Trang chủ", to: "/student/dashboard" },
    { label: "Cộng đồng", to: "/student/community", disabled: true }
  ];

  const userName = localStorage.getItem("UserName") || "Học sinh";
  const userInitials = userName.substring(0, 2).toUpperCase() || "HS";

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Học sinh"
      appTitle="Hệ thống Học tập & Đánh giá"
      navItems={studentNavItems}
      topNavItems={topNavItems}
      userName={userName}
      userRoleLabel="Student"
      userInitials={userInitials}
      profilePath="/student/profile"
      primaryAction={{
        label: "Làm bài tập",
        icon: "play_arrow",
        to: "/student/test"
      }}
      onLogout={handleLogout}
    >
      {children}
    </DashboardLayout>
  );
}
