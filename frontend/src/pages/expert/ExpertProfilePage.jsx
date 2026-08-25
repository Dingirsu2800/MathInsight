import * as React from "react";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import UserProfileForm from "../../components/profile/UserProfileForm";

export default function ExpertProfilePage() {
  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-xl mx-auto">
        <DashboardPageHeader
          title="Hồ sơ cá nhân"
          subtitle="Quản lý thông tin tài khoản và đổi mật khẩu chuyên gia."
        />

        <div className="max-w-3xl">
          <UserProfileForm />
        </div>
      </div>
    </ExpertLayout>
  );
}
