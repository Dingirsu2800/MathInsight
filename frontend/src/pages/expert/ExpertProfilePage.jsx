import * as React from "react";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import useCurrentUser from "../../hooks/useCurrentUser";

export default function ExpertProfilePage() {
  const { profile, displayName, initials, loading } = useCurrentUser("Expert");
  const roleName = profile?.roleName || "Expert";
  const specialty = profile?.expert?.specialty || "Chưa cập nhật";

  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-xl mx-auto">
        <DashboardPageHeader
          title="Hồ sơ cá nhân"
          subtitle="Thông tin tài khoản và vai trò hiện tại của bạn trên hệ thống."
        />

        {loading ? (
          <section className="bg-pure-surface border border-whisper-border rounded-xl p-8 text-sm text-on-surface-variant">
            Đang tải thông tin tài khoản...
          </section>
        ) : (
          <section className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm p-6 space-y-6">
            <div className="flex items-center gap-4 pb-6 border-b border-whisper-border">
              {profile?.avatarUrl ? (
                <img src={profile.avatarUrl} alt="Ảnh đại diện" className="w-16 h-16 rounded-full object-cover border border-primary/20" />
              ) : (
                <div className="w-16 h-16 rounded-full bg-primary/10 border border-primary/20 text-primary flex items-center justify-center font-bold text-xl select-none">
                  {initials}
                </div>
              )}
              <div>
                <h3 className="text-lg font-bold text-on-surface">{displayName}</h3>
                <p className="text-xs text-on-surface-variant font-semibold uppercase tracking-wider font-mono">{roleName}</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-[14px]">
              <div>
                <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Tên hiển thị</h4>
                <p className="font-semibold text-on-surface">{displayName}</p>
              </div>
              <div>
                <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Tên đăng nhập</h4>
                <p className="font-semibold text-on-surface">{profile?.username || "Chưa cập nhật"}</p>
              </div>
              <div>
                <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Địa chỉ Email</h4>
                <p className="font-semibold text-on-surface">{profile?.email || "Chưa cập nhật"}</p>
              </div>
              <div>
                <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Chuyên môn</h4>
                <p className="font-semibold text-on-surface">{specialty}</p>
              </div>
              <div>
                <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Trạng thái tài khoản</h4>
                <span className="inline-flex items-center gap-1 font-bold text-[10px] uppercase tracking-wider bg-emerald-success/10 border border-emerald-success/20 text-emerald-success px-2.5 py-0.5 rounded-full mt-0.5">
                  Đang hoạt động
                </span>
              </div>
            </div>
          </section>
        )}
      </div>
    </ExpertLayout>
  );
}
