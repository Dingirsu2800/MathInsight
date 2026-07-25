import * as React from "react";
import { useNavigate } from "react-router-dom";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { teacherNavItems } from "../../config/dashboardNav";
import client from "../../services/questionBankApiClient";

export default function TeacherLayout({ children }) {
  const navigate = useNavigate();

  const handleLogout = async () => {
    try {
      await client.post("/api/v1/auth/logout");
    } catch (err) {
      console.error("Lỗi đăng xuất hệ thống:", err);
    } finally {
      localStorage.removeItem("token");
      localStorage.removeItem("access_token");
      localStorage.removeItem("AccountId");
      localStorage.removeItem("RoleName");
      localStorage.removeItem("UserName");
      navigate("/login");
    }
  };

  const topNavItems = [
    { label: "Tổng quan", to: "/teacher/lectures" },
    { label: "Thống kê", to: "/teacher/stats", disabled: true }
  ];

  const userName = localStorage.getItem("UserName") || "Giáo viên";
  const userInitials = userName.substring(0, 2).toUpperCase() || "GV";

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Giáo viên"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={teacherNavItems}
      topNavItems={topNavItems}
      userName={userName}
      userRoleLabel="Teacher"
      userInitials={userInitials}
      profilePath="/profile"
      primaryAction={{
        label: "Tạo bài giảng mới",
        icon: "add",
        to: "/teacher/lectures/new"
      }}
      onLogout={handleLogout}
    >
      {children}
    </DashboardLayout>
  );
}
