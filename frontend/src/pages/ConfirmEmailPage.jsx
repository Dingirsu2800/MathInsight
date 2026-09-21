import * as React from "react";
import { Link, useSearchParams } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { mapAuthError } from "../services/authErrors";

const CONFIRM_FALLBACK_ERROR = "Xác nhận tài khoản thất bại. Vui lòng thử lại sau.";

function CardShell({ children }) {
  return (
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
      <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 text-center space-y-5">
        <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center mx-auto shadow-sm">
          <span className="material-symbols-outlined text-white text-[26px]">functions</span>
        </div>
        {children}
      </section>
    </main>
  );
}

export default function ConfirmEmailPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");

  const [status, setStatus] = React.useState(token ? "idle" : "error");
  const [error, setError] = React.useState(
    token ? null : { message: "Liên kết không hợp lệ.", linkTo: "/login", linkLabel: "Về trang đăng nhập" },
  );
  const submittingRef = React.useRef(false);

  const handleConfirm = async () => {
    if (!token || submittingRef.current) {
      return;
    }

    submittingRef.current = true;
    setStatus("loading");

    try {
      await client.post("/api/v1/auth/confirm-email", { token });
      setStatus("success");
    } catch (err) {
      console.error(err);
      const httpStatus = err?.response?.status;

      if (httpStatus === 410) {
        setError({
          message: "Liên kết đã hết hạn. Vui lòng đăng ký lại.",
          linkTo: "/register",
          linkLabel: "Đăng ký lại",
        });
      } else if (httpStatus === 409) {
        setError({
          message: "Email này đã được xác nhận. Vui lòng đăng nhập.",
          linkTo: "/login",
          linkLabel: "Đăng nhập",
        });
      } else {
        setError({
          message: mapAuthError(err, CONFIRM_FALLBACK_ERROR),
          linkTo: "/login",
          linkLabel: "Về trang đăng nhập",
        });
      }
      setStatus("error");
    } finally {
      submittingRef.current = false;
    }
  };

  if (status === "loading") {
    return (
      <CardShell>
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-[3px] border-[#2f5fa8] border-t-transparent rounded-full animate-spin"></div>
          <p className="text-sm font-semibold text-[#1e2a4a]">Đang xác nhận tài khoản...</p>
        </div>
      </CardShell>
    );
  }

  if (status === "idle") {
    return (
      <CardShell>
        <h1 className="text-2xl font-bold text-[#1e2a4a]">Xác nhận email</h1>
        <p className="text-sm text-slate-500 leading-relaxed">
          Vui lòng nhấn nút bên dưới để xác nhận địa chỉ email và kích hoạt tài khoản.
        </p>
        <button
          type="button"
          onClick={handleConfirm}
          className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
        >
          Xác nhận email
        </button>
      </CardShell>
    );
  }

  if (status === "success") {
    return (
      <CardShell>
        <div className="w-14 h-14 rounded-full bg-emerald-success/10 flex items-center justify-center mx-auto">
          <span className="material-symbols-outlined text-emerald-success text-[32px]">check_circle</span>
        </div>
        <h1 className="text-2xl font-bold text-[#1e2a4a]">Xác nhận thành công!</h1>
        <p className="text-sm text-slate-500 leading-relaxed">
          Tài khoản của bạn đã được kích hoạt. Bạn có thể đăng nhập ngay.
        </p>
        <Link
          to="/login"
          className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
        >
          Đăng nhập
        </Link>
      </CardShell>
    );
  }

  // status === "error"
  return (
    <CardShell>
      <div className="w-14 h-14 rounded-full bg-deep-rose/10 flex items-center justify-center mx-auto">
        <span className="material-symbols-outlined text-deep-rose text-[32px]">error</span>
      </div>
      <h1 className="text-2xl font-bold text-[#1e2a4a]">Không thể xác nhận</h1>
      <p className="text-sm text-slate-500 leading-relaxed">{error?.message}</p>
      <Link
        to={error?.linkTo || "/login"}
        className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
      >
        {error?.linkLabel || "Về trang đăng nhập"}
      </Link>
    </CardShell>
  );
}
