import * as React from "react";
import { useNavigate } from "react-router-dom";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { adminNavItems } from "../../config/dashboardNav";
import client from "../../services/questionBankApiClient";

export default function AdminLayout({ children }) {
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
      navigate("/login");
    }
  };

  const topNavItems = [
    { label: "Tài khoản", to: "/admin/accounts" },
    { label: "Đơn đăng ký giáo viên", to: "/admin/applications" },
    { label: "Vai trò & Quyền", to: "/admin/roles" }
  ];

  return (
    <DashboardLayout
      brandName="MathInsight"
      roleLabel="Quản trị viên"
      appTitle="Hệ thống Quản lý Toán học"
      navItems={adminNavItems}
      topNavItems={topNavItems}
      userName="Quản trị viên"
      userRoleLabel="Admin"
      userInitials="QT"
      profilePath="/profile"
      onLogout={handleLogout}
    >
      {children}
    </DashboardLayout>
  );
}
