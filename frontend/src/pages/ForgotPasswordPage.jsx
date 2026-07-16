import * as React from "react";
import { Link } from "react-router-dom";
import client from "../services/questionBankApiClient";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const inputClass =
  "w-full pl-11 pr-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400";

export default function ForgotPasswordPage() {
  const [email, setEmail] = React.useState("");
  const [error, setError] = React.useState("");
  const [loading, setLoading] = React.useState(false);
  const [submitted, setSubmitted] = React.useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!email.trim()) {
      setError("Vui lòng nhập email.");
      return;
    }
    if (!EMAIL_PATTERN.test(email.trim())) {
      setError("Email không hợp lệ.");
      return;
    }

    setError("");
    setLoading(true);

    try {
      await client.post("/api/v1/auth/reset-password", { email: email.trim() });
      // Backend always returns 200 (enumeration protection) — show a neutral state that
      // does NOT confirm whether the email is registered.
      setSubmitted(true);
    } catch (err) {
      console.error(err);
      // Only reached on a network/server failure, which reveals nothing about the account.
      setError("Không thể gửi yêu cầu. Vui lòng thử lại sau.");
    } finally {
      setLoading(false);
    }
  };

  // Neutral success state (post-200): deliberately non-committal about account existence.
  if (submitted) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
        <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-[#2f5fa8]/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-[#2f5fa8] text-[32px]">mark_email_read</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">Kiểm tra email của bạn</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Nếu email này đã đăng ký, chúng tôi đã gửi một liên kết đặt lại mật khẩu. Vui lòng
            kiểm tra hộp thư (cả mục Spam).
          </p>
          <Link
            to="/login"
            className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
          >
            Về trang đăng nhập
          </Link>
        </section>
      </main>
    );
  }

  return (
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
      <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
        <div className="flex flex-col items-center text-center space-y-3">
          <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center shadow-sm">
            <span className="material-symbols-outlined text-white text-[26px]">functions</span>
          </div>
          <div className="space-y-1.5">
            <h1 className="text-2xl font-bold text-[#1e2a4a]">Quên mật khẩu?</h1>
            <p className="text-sm text-slate-500 leading-relaxed max-w-xs">
              Nhập email đã đăng ký. Chúng tôi sẽ gửi liên kết đặt lại mật khẩu.
            </p>
          </div>
        </div>

        {error && (
          <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
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
                className={inputClass}
                placeholder="email@fpt.edu.vn"
                autoComplete="email"
                disabled={loading}
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed mt-2"
          >
            {loading ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                Đang gửi...
              </>
            ) : (
              "Gửi liên kết"
            )}
          </button>
        </form>

        <p className="text-center text-sm text-slate-500">
          Nhớ mật khẩu rồi?{" "}
          <Link to="/login" className="font-bold text-[#2f5fa8] hover:underline">
            Đăng nhập
          </Link>
        </p>
      </section>
    </main>
  );
}
