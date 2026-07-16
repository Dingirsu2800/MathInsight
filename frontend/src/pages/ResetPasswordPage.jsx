import * as React from "react";
import { Link, useSearchParams } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { mapAuthError } from "../services/authErrors";

// BR-08: 8–128 chars incl. uppercase, lowercase, number, special char. Mirrors
// AuthValidation.PasswordPattern on the backend so most 400s are caught client-side.
const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,128}$/;
const PASSWORD_HINT =
  "Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";

const RESET_FALLBACK_ERROR = "Đặt lại mật khẩu thất bại. Vui lòng thử lại sau.";

// ASP.NET ValidationProblemDetails keys the errors dict by PascalCase property name
// (e.g. "NewPassword"); our field state uses camelCase. Lowercasing the first char lines
// them up.
function toFieldKey(pascalKey) {
  if (!pascalKey) return pascalKey;
  return pascalKey.charAt(0).toLowerCase() + pascalKey.slice(1);
}

const passwordInputClass =
  "w-full pl-11 pr-11 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400";

function CardShell({ children }) {
  return (
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
      <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
        {children}
      </section>
    </main>
  );
}

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");

  // "form" | "success" | "expired". A missing token short-circuits to the invalid view.
  const [view, setView] = React.useState(token ? "form" : "invalid");

  const [newPassword, setNewPassword] = React.useState("");
  const [confirmPassword, setConfirmPassword] = React.useState("");
  const [showNew, setShowNew] = React.useState(false);
  const [showConfirm, setShowConfirm] = React.useState(false);
  const [errors, setErrors] = React.useState({});
  const [formError, setFormError] = React.useState("");
  const [loading, setLoading] = React.useState(false);

  function validate() {
    const next = {};

    if (!newPassword) {
      next.newPassword = "Vui lòng nhập mật khẩu mới.";
    } else if (!PASSWORD_PATTERN.test(newPassword)) {
      next.newPassword = PASSWORD_HINT;
    }

    if (!confirmPassword) {
      next.confirmPassword = "Vui lòng xác nhận mật khẩu mới.";
    } else if (confirmPassword !== newPassword) {
      next.confirmPassword = "Mật khẩu xác nhận không khớp.";
    }

    return next;
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    setFormError("");

    const validationErrors = validate();
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }
    setErrors({});
    setLoading(true);

    try {
      // confirmPassword is client-side only and is never sent to the API.
      await client.post("/api/v1/auth/confirm-reset-password", {
        token,
        newPassword,
      });
      setView("success");
    } catch (err) {
      console.error(err);
      const status = err?.response?.status;

      if (status === 410) {
        setView("expired");
      } else if (status === 400 && err.response?.data?.errors) {
        // Surface backend password-policy errors under the field.
        const backendErrors = err.response.data.errors;
        const mapped = {};
        Object.keys(backendErrors).forEach((key) => {
          const messages = backendErrors[key];
          mapped[toFieldKey(key)] = Array.isArray(messages) ? messages[0] : String(messages);
        });
        setErrors(mapped);
        setFormError("Vui lòng kiểm tra lại mật khẩu.");
      } else {
        setFormError(mapAuthError(err, RESET_FALLBACK_ERROR));
      }
    } finally {
      setLoading(false);
    }
  };

  if (view === "invalid") {
    return (
      <CardShell>
        <div className="text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-deep-rose/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-deep-rose text-[32px]">error</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">Liên kết không hợp lệ.</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Liên kết đặt lại mật khẩu bị thiếu hoặc không đúng.
          </p>
          <Link
            to="/forgot-password"
            className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
          >
            Yêu cầu liên kết mới
          </Link>
        </div>
      </CardShell>
    );
  }

  if (view === "expired") {
    return (
      <CardShell>
        <div className="text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-amber-warning/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-amber-warning text-[32px]">schedule</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">Liên kết đã hết hạn</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Liên kết đã hết hạn. Vui lòng yêu cầu đặt lại mật khẩu mới.
          </p>
          <Link
            to="/forgot-password"
            className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
          >
            Yêu cầu liên kết mới
          </Link>
        </div>
      </CardShell>
    );
  }

  if (view === "success") {
    return (
      <CardShell>
        <div className="text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-emerald-success/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-emerald-success text-[32px]">check_circle</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">Đổi mật khẩu thành công!</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Bạn có thể đăng nhập bằng mật khẩu mới.
          </p>
          <Link
            to="/login"
            className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
          >
            Đăng nhập
          </Link>
        </div>
      </CardShell>
    );
  }

  // view === "form"
  return (
    <CardShell>
      <div className="flex flex-col items-center text-center space-y-3">
        <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center shadow-sm">
          <span className="material-symbols-outlined text-white text-[26px]">functions</span>
        </div>
        <h1 className="text-2xl font-bold text-[#1e2a4a]">Đặt lại mật khẩu</h1>
      </div>

      {formError && (
        <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
          <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
          <span>{formError}</span>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4" noValidate>
        {/* Mật khẩu mới */}
        <div className="space-y-1.5">
          <label htmlFor="newPassword" className="block text-sm font-semibold text-[#1e2a4a]">
            Mật khẩu mới
          </label>
          <div className="relative flex items-center">
            <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">lock</span>
            <input
              id="newPassword"
              type={showNew ? "text" : "password"}
              value={newPassword}
              onChange={(e) => {
                setNewPassword(e.target.value);
                setErrors((prev) => (prev.newPassword ? { ...prev, newPassword: undefined } : prev));
              }}
              className={passwordInputClass}
              placeholder="••••••••"
              autoComplete="new-password"
              disabled={loading}
            />
            <button
              type="button"
              onClick={() => setShowNew((v) => !v)}
              className="absolute right-3 text-slate-400 hover:text-[#2f5fa8] transition-colors cursor-pointer"
              aria-label={showNew ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
              tabIndex={-1}
            >
              <span className="material-symbols-outlined text-[20px]">
                {showNew ? "visibility_off" : "visibility"}
              </span>
            </button>
          </div>
          {errors.newPassword ? (
            <p className="text-xs text-deep-rose font-medium">{errors.newPassword}</p>
          ) : (
            <p className="text-xs text-slate-400">{PASSWORD_HINT}</p>
          )}
        </div>

        {/* Xác nhận mật khẩu mới */}
        <div className="space-y-1.5">
          <label htmlFor="confirmPassword" className="block text-sm font-semibold text-[#1e2a4a]">
            Xác nhận mật khẩu mới
          </label>
          <div className="relative flex items-center">
            <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">lock</span>
            <input
              id="confirmPassword"
              type={showConfirm ? "text" : "password"}
              value={confirmPassword}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                setErrors((prev) => (prev.confirmPassword ? { ...prev, confirmPassword: undefined } : prev));
              }}
              className={passwordInputClass}
              placeholder="••••••••"
              autoComplete="new-password"
              disabled={loading}
            />
            <button
              type="button"
              onClick={() => setShowConfirm((v) => !v)}
              className="absolute right-3 text-slate-400 hover:text-[#2f5fa8] transition-colors cursor-pointer"
              aria-label={showConfirm ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
              tabIndex={-1}
            >
              <span className="material-symbols-outlined text-[20px]">
                {showConfirm ? "visibility_off" : "visibility"}
              </span>
            </button>
          </div>
          {errors.confirmPassword && (
            <p className="text-xs text-deep-rose font-medium">{errors.confirmPassword}</p>
          )}
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed mt-2"
        >
          {loading ? (
            <>
              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
              Đang xử lý...
            </>
          ) : (
            "Đặt lại mật khẩu"
          )}
        </button>
      </form>
    </CardShell>
  );
}
