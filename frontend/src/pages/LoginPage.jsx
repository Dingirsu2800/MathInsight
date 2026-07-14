import * as React from "react";
import { useNavigate } from "react-router-dom";
import client from "../services/questionBankApiClient";

export default function LoginPage() {
  const navigate = useNavigate();
  const [usernameOrEmail, setUsernameOrEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!usernameOrEmail.trim() || !password.trim()) {
      setError("Vui lòng điền đầy đủ tài khoản và mật khẩu.");
      return;
    }

    setLoading(true);
    setError("");
    localStorage.removeItem("token");
    localStorage.removeItem("access_token");

    try {
      const response = await client.post("/api/v1/auth/login", {
        usernameOrEmail,
        password,
      });

      const data = response.data || {};
      const token = data.accessToken || data.AccessToken;
      const roleName = data.roleName || data.RoleName || "";
      const accountId = data.accountId || data.AccountId || data.id || data.Id || "";

      if (!token) {
        throw new Error("Không nhận được mã xác thực (Token) từ hệ thống.");
      }

      const roleHomePaths = {
        Admin: "/admin/accounts",
        Expert: "/expert/questions",
      };

      const homePath = roleHomePaths[roleName];
      if (!homePath) {
        setError(`Tài khoản với vai trò "${roleName}" chưa được hỗ trợ trên cổng này.`);
        setLoading(false);
        return;
      }

      localStorage.setItem("token", token);
      localStorage.setItem("AccountId", accountId);
      localStorage.setItem("RoleName", roleName);
      navigate(homePath);
    } catch (err) {
      console.error(err);
      const errMsg = err.response?.data?.message || err.message || "Đăng nhập thất bại. Vui lòng kiểm tra lại.";
      setError(errMsg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="page page-centered bg-surface-container-lowest min-h-screen flex items-center justify-center p-4">
      <section className="card auth-card max-w-md w-full bg-pure-surface border border-whisper-border p-8 rounded-2xl diffused-shadow space-y-6">
        <div className="text-center space-y-2">
          <p className="text-primary text-[11px] font-black tracking-widest uppercase">MathInsight Portal</p>
          <h1 className="text-2xl font-bold text-on-surface">Đăng nhập Chuyên gia</h1>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Nhập tài khoản và mật khẩu của bạn để truy cập hệ thống Quản lý và Soạn thảo Ngân hàng câu hỏi.
          </p>
        </div>

        {error && (
          <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2 animate-in fade-in duration-200">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Tên đăng nhập hoặc Email</label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3 text-on-surface-variant text-[18px]">person</span>
              <input
                type="text"
                value={usernameOrEmail}
                onChange={(e) => setUsernameOrEmail(e.target.value)}
                className="w-full pl-10 pr-4 py-2.5 text-sm bg-transparent border border-outline-variant rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                placeholder="expert@mathinsight.vn"
                disabled={loading}
              />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Mật khẩu</label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3 text-on-surface-variant text-[18px]">lock</span>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full pl-10 pr-4 py-2.5 text-sm bg-transparent border border-outline-variant rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                placeholder="••••••••"
                disabled={loading}
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-primary text-white py-3 rounded-xl font-bold text-xs uppercase tracking-wider hover:bg-primary/95 transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50 mt-6"
          >
            {loading ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                Đang đăng nhập...
              </>
            ) : (
              <>
                <span className="material-symbols-outlined text-[16px]">login</span>
                Đăng nhập
              </>
            )}
          </button>
        </form>

        <div className="text-center pt-2 border-t border-whisper-border">
          <p className="text-[10px] text-on-surface-variant font-medium">
            MathInsight Portal &copy; 2026. All rights reserved.
          </p>
        </div>
      </section>
    </main>
  );
}
