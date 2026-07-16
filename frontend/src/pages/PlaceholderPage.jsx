import { Link } from "react-router-dom";
import { logout } from "../services/auth";

// Lightweight stand-in for routes that exist as navigation targets but whose
// full pages are not built yet (role landings, register, forgot-password).
//
// `showLogout` adds a minimal top bar with a "Đăng xuất" control — used on the
// authenticated role-landing placeholders (/student, /teacher, /admin). Public
// placeholders (register, forgot-password) leave it off and keep the login link.
export default function PlaceholderPage({ title, description, showLogout = false }) {
  return (
    <div className="min-h-screen bg-[#eef2f7] flex flex-col">
      {showLogout && (
        <header className="w-full flex items-center justify-between px-6 py-4">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-[#2f5fa8] flex items-center justify-center">
              <span className="material-symbols-outlined text-white text-[18px]">functions</span>
            </div>
            <span className="font-bold text-[#1e2a4a]">MathInsight</span>
          </div>
          <button
            type="button"
            onClick={() => logout()}
            className="flex items-center gap-1.5 text-sm font-semibold text-[#2f5fa8] hover:bg-[#2f5fa8]/10 px-3 py-2 rounded-lg transition-colors cursor-pointer"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            Đăng xuất
          </button>
        </header>
      )}
      <main className="flex-1 flex items-center justify-center p-4">
        <div className="text-center space-y-4 max-w-md">
          <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center mx-auto shadow-sm">
            <span className="material-symbols-outlined text-white text-[26px]">construction</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">{title || "Trang đang được phát triển"}</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            {description || "Tính năng này sẽ sớm ra mắt."}
          </p>
          {!showLogout && (
            <Link to="/login" className="inline-block text-sm font-bold text-[#2f5fa8] hover:underline">
              ← Quay lại đăng nhập
            </Link>
          )}
        </div>
      </main>
    </div>
  );
}
