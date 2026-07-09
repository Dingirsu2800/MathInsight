import * as React from "react";
import { useNavigate } from "react-router-dom";
import DashboardLayout from "../../components/layout/DashboardLayout";
import { expertNavItems } from "../../config/dashboardNav";
import client from "../../services/questionBankApiClient";

export default function ExpertLayout({ children }) {
  const navigate = useNavigate();

  const handleLogout = async () => {
    try {
      await client.post("/api/v1/auth/logout");
    } catch (err) {
      console.error("Lỗi đăng xuất hệ thống:", err);
    } finally {
      localStorage.removeItem("token");
      localStorage.removeItem("access_token");
      navigate("/login");
    }
  };

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
      onLogout={handleLogout}
    >
      {children}
    </DashboardLayout>
  );
}
