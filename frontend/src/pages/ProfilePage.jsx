import * as React from "react";
import { Link } from "react-router-dom";
import { logout } from "../services/auth";
import { resolveHomePath } from "../utils/roleRoutes";
import { getRoleName } from "../services/authStorage";
import UserProfileForm from "../components/profile/UserProfileForm";

export default function ProfilePage() {
  const homePath = resolveHomePath(getRoleName());

  return (
    <div className="min-h-screen bg-[#eef2f7] flex flex-col">
      <header className="w-full flex items-center justify-between px-6 py-4">
        <Link to={homePath} className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg bg-[#2f5fa8] flex items-center justify-center">
            <span className="material-symbols-outlined text-white text-[18px]">functions</span>
          </div>
          <span className="font-bold text-[#1e2a4a]">MathInsight</span>
        </Link>
        <button
          type="button"
          onClick={() => logout()}
          className="flex items-center gap-1.5 text-sm font-semibold text-[#2f5fa8] hover:bg-[#2f5fa8]/10 px-3 py-2 rounded-lg transition-colors cursor-pointer"
        >
          <span className="material-symbols-outlined text-[18px]">logout</span>
          Đăng xuất
        </button>
      </header>

      <main className="flex-1 flex justify-center p-4 pb-10">
        <div className="w-full max-w-2xl space-y-4">
          <Link
            to={homePath}
            className="inline-flex items-center gap-1 text-sm font-semibold text-[#2f5fa8] hover:underline"
          >
            ← Về trang chủ
          </Link>

          <UserProfileForm />
        </div>
      </main>
    </div>
  );
}
