import * as React from "react";
import { Link, useNavigate } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { setAuthSession } from "../services/authStorage";
import { mapAuthError } from "../services/authErrors";

const LOGIN_FALLBACK_ERROR = "Đăng nhập thất bại. Vui lòng thử lại sau.";

const REMEMBERED_EMAIL_KEY = "remembered_email";

// Landing page per role after a successful login.
function resolveHomePath(roleName) {
  switch (String(roleName || "").toLowerCase()) {
    case "student":
      return "/student";
    case "teacher":
      return "/teacher";
    case "expert":
      return "/expert/questions";
    case "admin":
      return "/admin";
    default:
      return "/";
  }
}

function GoogleLogo() {
  return (
    <svg className="w-[18px] h-[18px]" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
      <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
      <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
      <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
    </svg>
  );
}

export default function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [showPassword, setShowPassword] = React.useState(false);
  const [rememberMe, setRememberMe] = React.useState(false);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  // Prefill the email if the user asked to be remembered previously.
  React.useEffect(() => {
    const remembered = localStorage.getItem(REMEMBERED_EMAIL_KEY);
    if (remembered) {
      setEmail(remembered);
      setRememberMe(true);
    }
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!email.trim() || !password.trim()) {
      setError("Vui lòng nhập đầy đủ email và mật khẩu.");
      return;
    }

    setLoading(true);
    setError("");

    try {
      const response = await client.post("/api/v1/auth/login", {
        usernameOrEmail: email.trim(),
        password,
      });

      const data = response.data || {};
      const accessToken = data.accessToken || data.AccessToken;
      const refreshToken = data.refreshToken || data.RefreshToken;
      const roleName = data.roleName || data.RoleName || "";
      const accountId = data.accountId || data.AccountId || "";

      if (!accessToken) {
        throw new Error("missing-token");
      }

      setAuthSession({ accessToken, refreshToken, roleName, accountId });

      if (rememberMe) {
        localStorage.setItem(REMEMBERED_EMAIL_KEY, email.trim());
      } else {
        localStorage.removeItem(REMEMBERED_EMAIL_KEY);
      }

      navigate(resolveHomePath(roleName), { replace: true });
    } catch (err) {
      console.error(err);
      // A network/parse error (no response) still gets the generic fallback.
      setError(mapAuthError(err, LOGIN_FALLBACK_ERROR));
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
      <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
        {/* Logo + heading */}
        <div className="flex flex-col items-center text-center space-y-3">
          <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center shadow-sm">
            <span className="material-symbols-outlined text-white text-[26px]">functions</span>
          </div>
          <div className="space-y-1.5">
            <h1 className="text-2xl font-bold text-[#1e2a4a]">Đăng nhập tài khoản!</h1>
            <p className="text-sm text-slate-500 leading-relaxed max-w-xs">
              Nhập email đã đăng ký và mật khẩu để truy cập không gian học tập MathInsight.
            </p>
          </div>
        </div>

        {error && (
          <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Email */}
          <div className="space-y-1.5">
            <label htmlFor="email" className="block text-sm font-semibold text-[#1e2a4a]">
              Email
            </label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">mail</span>
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full pl-11 pr-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400"
                placeholder="email@fpt.edu.vn"
                autoComplete="email"
                disabled={loading}
              />
            </div>
          </div>

          {/* Password */}
          <div className="space-y-1.5">
            <label htmlFor="password" className="block text-sm font-semibold text-[#1e2a4a]">
              Mật khẩu
            </label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">lock</span>
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full pl-11 pr-11 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400"
                placeholder="••••••••"
                autoComplete="current-password"
                disabled={loading}
              />
              <button
                type="button"
                onClick={() => setShowPassword((v) => !v)}
                className="absolute right-3 text-slate-400 hover:text-[#2f5fa8] transition-colors cursor-pointer"
                aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                tabIndex={-1}
              >
                <span className="material-symbols-outlined text-[20px]">
                  {showPassword ? "visibility_off" : "visibility"}
                </span>
              </button>
            </div>
          </div>

          {/* Remember me + forgot password */}
          <div className="flex items-center justify-between pt-1">
            <label className="flex items-center gap-2 text-sm text-slate-600 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
                className="w-4 h-4 rounded border-slate-300 text-[#2f5fa8] focus:ring-[#2f5fa8]/30 cursor-pointer accent-[#2f5fa8]"
                disabled={loading}
              />
              Ghi nhớ đăng nhập
            </label>
            <Link to="/forgot-password" className="text-sm font-semibold text-[#2f5fa8] hover:underline">
              Quên mật khẩu?
            </Link>
          </div>

          {/* Submit */}
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed mt-2"
          >
            {loading ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                Đang đăng nhập...
              </>
            ) : (
              "Đăng nhập"
            )}
          </button>
        </form>

        {/* Divider */}
        <div className="flex items-center gap-3">
          <div className="flex-1 h-px bg-slate-200"></div>
          <span className="text-xs text-slate-400 font-medium">Hoặc đăng nhập với</span>
          <div className="flex-1 h-px bg-slate-200"></div>
        </div>

        {/* Google (not implemented yet) */}
        <button
          type="button"
          disabled
          title="Sắp có"
          className="w-full flex items-center justify-center gap-2.5 py-3 rounded-xl border border-slate-200 bg-white text-sm font-semibold text-slate-600 cursor-not-allowed opacity-70"
        >
          <GoogleLogo />
          Google
          <span className="text-[10px] font-bold uppercase tracking-wide text-slate-400 bg-slate-100 px-1.5 py-0.5 rounded">
            Sắp có
          </span>
        </button>

        {/* Register */}
        <p className="text-center text-sm text-slate-500">
          Chưa có tài khoản?{" "}
          <Link to="/register" className="font-bold text-[#2f5fa8] hover:underline">
            Đăng ký ngay
          </Link>
        </p>
      </section>
    </main>
  );
}
