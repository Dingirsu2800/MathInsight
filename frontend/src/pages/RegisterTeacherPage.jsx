import * as React from "react";
import { Link } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { mapAuthError, getAuthErrorCode } from "../services/authErrors";
import CertificateUploader from "../components/teacher/CertificateUploader";
import {
  CERT_COUNT_ERROR,
  CERT_MAX_FILES,
  CERT_REQUIRED_ERROR,
} from "../utils/certificateFiles";

// BR-08: 8–128 chars incl. uppercase, lowercase, number, special char. Mirrors
// AuthValidation.PasswordPattern on the backend so most 400s are caught client-side.
const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,128}$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
// Vietnamese phone number as dialled domestically: 10 digits starting with 0. Mirrors
// AuthValidation.PhoneNumberPattern on the backend.
const PHONE_PATTERN = /^0\d{9}$/;
const PASSWORD_HINT =
  "Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";

const REGISTER_FALLBACK_ERROR = "Đăng ký thất bại. Vui lòng thử lại sau.";

// Certificate rules (BR-05 — accepted types, size and count) live in utils/certificateFiles.js
// and are enforced by the shared CertificateUploader, so registration and resubmit agree.

// ASP.NET ValidationProblemDetails keys the errors dict by PascalCase property
// name; our field state uses camelCase. Lowercasing the first char lines them up.
function toFieldKey(pascalKey) {
  if (!pascalKey) return pascalKey;
  return pascalKey.charAt(0).toLowerCase() + pascalKey.slice(1);
}

function LabeledInput({ id, label, icon, error, children }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-semibold text-[#1e2a4a]">
        {label}
      </label>
      <div className="relative flex items-center">
        <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">
          {icon}
        </span>
        {children}
      </div>
      {error && <p className="text-xs text-deep-rose font-medium">{error}</p>}
    </div>
  );
}

const inputClass =
  "w-full pl-11 pr-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400";

export default function RegisterTeacherPage() {
  const [form, setForm] = React.useState({
    lastName: "",
    firstName: "",
    username: "",
    email: "",
    phoneNumber: "",
    password: "",
    confirmPassword: "",
    biography: "",
  });
  // One entry per selected file: { id, file, previewUrl } — previewUrl is null for PDF/Word.
  const [certificates, setCertificates] = React.useState([]);
  const [showPassword, setShowPassword] = React.useState(false);
  const [showConfirm, setShowConfirm] = React.useState(false);
  const [errors, setErrors] = React.useState({});
  const [formError, setFormError] = React.useState("");
  const [loading, setLoading] = React.useState(false);
  const [submittedEmail, setSubmittedEmail] = React.useState("");

  const setField = (name) => (e) => {
    const value = e.target.value;
    setForm((prev) => ({ ...prev, [name]: value }));
    // Clear the field's error as soon as the user edits it.
    setErrors((prev) => (prev[name] ? { ...prev, [name]: undefined } : prev));
  };

  // The uploader owns picking, per-file validation and preview lifetimes; the page only stores
  // the resulting list and surfaces the error it reports.
  const handleCertificatesChange = (files, uploaderError) => {
    setCertificates(files);
    setErrors((prev) => ({ ...prev, certificates: uploaderError }));
  };

  function validate() {
    const next = {};

    if (!form.lastName.trim()) next.lastName = "Vui lòng nhập họ.";
    if (!form.firstName.trim()) next.firstName = "Vui lòng nhập tên.";
    if (!form.username.trim()) next.username = "Vui lòng nhập tên đăng nhập.";

    if (!form.email.trim()) {
      next.email = "Vui lòng nhập email.";
    } else if (!EMAIL_PATTERN.test(form.email.trim())) {
      next.email = "Email không hợp lệ.";
    }

    if (!form.phoneNumber.trim()) {
      next.phoneNumber = "Vui lòng nhập số điện thoại.";
    } else if (!PHONE_PATTERN.test(form.phoneNumber.trim())) {
      next.phoneNumber = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
    }

    if (!form.password) {
      next.password = "Vui lòng nhập mật khẩu.";
    } else if (!PASSWORD_PATTERN.test(form.password)) {
      next.password = PASSWORD_HINT;
    }

    if (!form.confirmPassword) {
      next.confirmPassword = "Vui lòng xác nhận mật khẩu.";
    } else if (form.confirmPassword !== form.password) {
      next.confirmPassword = "Mật khẩu xác nhận không khớp.";
    }

    if (certificates.length === 0) {
      next.certificates = CERT_REQUIRED_ERROR;
    } else if (certificates.length > CERT_MAX_FILES) {
      next.certificates = CERT_COUNT_ERROR;
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

    // Multipart body — confirmPassword is client-side only and never sent.
    // Field names match TeacherRegisterRequest (PascalCase [FromForm] binding is
    // case-insensitive, but we match the DTO casing to be explicit). The backend
    // reads the file size from IFormFile.Length, so no separate size field is sent.
    const formData = new FormData();
    formData.append("Username", form.username.trim());
    formData.append("Email", form.email.trim());
    formData.append("PhoneNumber", form.phoneNumber.trim());
    formData.append("Password", form.password);
    formData.append("FirstName", form.firstName.trim());
    formData.append("LastName", form.lastName.trim());
    if (form.biography.trim()) {
      formData.append("Biography", form.biography.trim());
    }
    // Repeated "Certificates" entries bind to the DTO's List<IFormFile>.
    certificates.forEach((item) => formData.append("Certificates", item.file));

    try {
      // Override the client's default application/json content-type: with FormData,
      // axios would otherwise JSON-stringify the body and drop the file. Setting
      // multipart/form-data lets the browser XHR add the boundary automatically.
      await client.post("/api/v1/auth/register/teacher", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      // 202 Accepted: no account exists yet — do not log in / redirect.
      setSubmittedEmail(form.email.trim());
    } catch (err) {
      console.error(err);
      const status = err?.response?.status;
      const code = getAuthErrorCode(err);

      if (status === 400 && code === "AUTH_CERTIFICATE_INVALID") {
        // Certificate-specific rejection from the backend — surface under the file input.
        setErrors({ certificates: "Chứng chỉ không hợp lệ. Vui lòng tải lên ảnh JPG/PNG rõ ràng." });
        setFormError("Vui lòng kiểm tra lại chứng chỉ đã tải lên.");
      } else if (status === 400 && err.response?.data?.errors) {
        // Surface backend field errors under the matching inputs.
        const backendErrors = err.response.data.errors;
        const mapped = {};
        Object.keys(backendErrors).forEach((key) => {
          const messages = backendErrors[key];
          mapped[toFieldKey(key)] = Array.isArray(messages) ? messages[0] : String(messages);
        });
        setErrors(mapped);
        setFormError("Vui lòng kiểm tra lại các thông tin được đánh dấu.");
      } else if (status === 409 && code === "AUTH_PHONE_ALREADY_USED") {
        // Distinct 409 from the email/username one — surface it under the phone input.
        setErrors({ phoneNumber: "Số điện thoại này đã được sử dụng." });
        setFormError("Vui lòng kiểm tra lại các thông tin được đánh dấu.");
      } else if (status === 409) {
        setFormError("Email hoặc tên đăng nhập đã được sử dụng.");
      } else {
        setFormError(mapAuthError(err, REGISTER_FALLBACK_ERROR));
      }
    } finally {
      setLoading(false);
    }
  };

  // Success state (post-202): confirmation email sent, then admin approval pending.
  if (submittedEmail) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4 py-8">
        <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-emerald-success/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-emerald-success text-[32px]">check_circle</span>
          </div>
          <h1 className="text-2xl font-bold text-[#1e2a4a]">Đăng ký thành công!</h1>
          <p className="text-sm text-slate-500 leading-relaxed">
            Chúng tôi đã gửi email xác nhận tới <span className="font-semibold text-[#1e2a4a]">{submittedEmail}</span>.
            Sau khi xác nhận email, hồ sơ của bạn sẽ chờ quản trị viên duyệt trước khi bạn có thể đăng nhập.
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
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4 py-8">
      <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
        <div className="flex flex-col items-center text-center space-y-3">
          <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center shadow-sm">
            <span className="material-symbols-outlined text-white text-[26px]">functions</span>
          </div>
          <div className="space-y-1.5">
            <h1 className="text-2xl font-bold text-[#1e2a4a]">Đăng ký tài khoản giáo viên</h1>
            <p className="text-sm text-slate-500 leading-relaxed max-w-xs">
              Điền thông tin và tải lên chứng chỉ giảng dạy. Hồ sơ sẽ được quản trị viên duyệt trước khi kích hoạt.
            </p>
          </div>
        </div>

        {formError && (
          <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span>{formError}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          {/* Họ / Tên */}
          <div className="grid grid-cols-2 gap-3">
            <LabeledInput id="lastName" label="Họ" icon="badge" error={errors.lastName}>
              <input
                id="lastName"
                type="text"
                value={form.lastName}
                onChange={setField("lastName")}
                className={inputClass}
                placeholder="Nguyễn"
                autoComplete="family-name"
                disabled={loading}
              />
            </LabeledInput>
            <LabeledInput id="firstName" label="Tên" icon="badge" error={errors.firstName}>
              <input
                id="firstName"
                type="text"
                value={form.firstName}
                onChange={setField("firstName")}
                className={inputClass}
                placeholder="An"
                autoComplete="given-name"
                disabled={loading}
              />
            </LabeledInput>
          </div>

          {/* Tên đăng nhập */}
          <LabeledInput id="username" label="Tên đăng nhập" icon="account_circle" error={errors.username}>
            <input
              id="username"
              type="text"
              value={form.username}
              onChange={setField("username")}
              className={inputClass}
              placeholder="nguyenan"
              autoComplete="username"
              disabled={loading}
            />
          </LabeledInput>

          {/* Email */}
          <LabeledInput id="email" label="Email" icon="mail" error={errors.email}>
            <input
              id="email"
              type="email"
              value={form.email}
              onChange={setField("email")}
              className={inputClass}
              placeholder="email@gmail.com"
              autoComplete="email"
              disabled={loading}
            />
          </LabeledInput>

          {/* Số điện thoại */}
          <LabeledInput id="phoneNumber" label="Số điện thoại" icon="call" error={errors.phoneNumber}>
            <input
              id="phoneNumber"
              type="tel"
              value={form.phoneNumber}
              onChange={setField("phoneNumber")}
              className={inputClass}
              placeholder="0912345678"
              autoComplete="tel"
              maxLength={20}
              disabled={loading}
            />
          </LabeledInput>

          {/* Mật khẩu */}
          <div className="space-y-1.5">
            <label htmlFor="password" className="block text-sm font-semibold text-[#1e2a4a]">
              Mật khẩu
            </label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">lock</span>
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                value={form.password}
                onChange={setField("password")}
                className="w-full pl-11 pr-11 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400"
                placeholder="••••••••"
                autoComplete="new-password"
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
            {errors.password ? (
              <p className="text-xs text-deep-rose font-medium">{errors.password}</p>
            ) : (
              <p className="text-xs text-slate-400">{PASSWORD_HINT}</p>
            )}
          </div>

          {/* Xác nhận mật khẩu */}
          <div className="space-y-1.5">
            <label htmlFor="confirmPassword" className="block text-sm font-semibold text-[#1e2a4a]">
              Xác nhận mật khẩu
            </label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">lock</span>
              <input
                id="confirmPassword"
                type={showConfirm ? "text" : "password"}
                value={form.confirmPassword}
                onChange={setField("confirmPassword")}
                className="w-full pl-11 pr-11 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400"
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

          {/* Giới thiệu bản thân */}
          <div className="space-y-1.5">
            <label htmlFor="biography" className="block text-sm font-semibold text-[#1e2a4a]">
              Giới thiệu bản thân
            </label>
            <textarea
              id="biography"
              value={form.biography}
              onChange={setField("biography")}
              rows={3}
              className="w-full px-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400 resize-y"
              placeholder="Kinh nghiệm giảng dạy, trường công tác, chuyên môn..."
              disabled={loading}
            />
            {errors.biography && <p className="text-xs text-deep-rose font-medium">{errors.biography}</p>}
          </div>

          {/* Chứng chỉ giảng dạy */}
          <CertificateUploader
            files={certificates}
            onFilesChange={handleCertificatesChange}
            error={errors.certificates}
            disabled={loading}
            inputId="certificate"
          />

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed mt-2"
          >
            {loading ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                Đang đăng ký...
              </>
            ) : (
              "Đăng ký"
            )}
          </button>
        </form>

        <div className="text-center space-y-2">
          <p className="text-sm text-slate-500">
            Đã có tài khoản?{" "}
            <Link to="/login" className="font-bold text-[#2f5fa8] hover:underline">
              Đăng nhập
            </Link>
          </p>
          <p className="text-xs text-slate-400">
            Bạn là học sinh?{" "}
            <Link to="/register" className="font-semibold text-[#2f5fa8] hover:underline">
              Đăng ký học sinh
            </Link>
          </p>
        </div>
      </section>
    </main>
  );
}
