import * as React from "react";
import ExpertLayout from "./ExpertLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";

export default function ExpertProfilePage() {
  return (
    <ExpertLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-xl mx-auto">
        <DashboardPageHeader
          title="Hồ sơ cá nhân"
          subtitle="Xem thông tin tài khoản và vai trò hiện tại của bạn trên hệ thống."
        />

        <section className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm p-6 space-y-6">
          <div className="flex items-center gap-4 pb-6 border-b border-whisper-border">
            <div className="w-16 h-16 rounded-full bg-primary/10 border border-primary/20 text-primary flex items-center justify-center font-bold text-xl select-none">
              CG
            </div>
            <div>
              <h3 className="text-lg font-bold text-on-surface">Chuyên gia nội dung</h3>
              <p className="text-xs text-on-surface-variant font-semibold uppercase tracking-wider font-mono">Expert / Content Creator</p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-[14px]">
            <div>
              <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Tên hiển thị:</h4>
              <p className="font-semibold text-on-surface">Chuyên gia nội dung</p>
            </div>
            <div>
              <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Vai trò hệ thống:</h4>
              <p className="font-semibold text-on-surface">Chuyên gia kiểm duyệt đề bài</p>
            </div>
            <div>
              <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Địa chỉ Email:</h4>
              <p className="font-semibold text-on-surface-variant italic">Chưa thiết lập</p>
            </div>
            <div>
              <h4 className="text-xs font-bold text-on-surface-variant uppercase tracking-wider mb-1">Trạng thái tài khoản:</h4>
              <span className="inline-flex items-center gap-1 font-bold text-[10px] uppercase tracking-wider bg-emerald-success/10 border border-emerald-success/20 text-emerald-success px-2.5 py-0.5 rounded-full mt-0.5">
                Đang hoạt động
              </span>
            </div>
          </div>
        </section>
      </div>
    </ExpertLayout>
  );
}
