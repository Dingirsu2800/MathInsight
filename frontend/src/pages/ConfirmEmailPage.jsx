import * as React from "react";
import { Link, useSearchParams } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { mapAuthError } from "../services/authErrors";

const CONFIRM_FALLBACK_ERROR = "Xác nhận tài khoản thất bại. Vui lòng thử lại sau.";

// --- Module-scoped de-duplication ----------------------------------------
// These maps live at MODULE scope on purpose: they survive both React 18
// StrictMode's dev double-mount AND a full unmount/remount of this page within
// the same page load. A component-local `useRef` is recreated on a real
// remount (and reset by StrictMode in some setups), which is exactly why the
// previous guard let a second POST reach the server.
//
// `confirmRequests` caches the single in-flight/settled POST promise per token.
// Any mount that sees the same token subscribes to that same promise instead of
// issuing another request, so the token is POSTed to the backend exactly once.
const confirmRequests = new Map();

// Tokens that returned 200 during this page load. Safety net for requirement 2:
// if a duplicate POST ever slips past the cache, its 410/409 is the echo of our
// own already-successful confirm — treat it as success, not as a real failure.
// Only ever populated on a real 200, so a genuinely expired token (which never
// succeeded here) is never masked.
const confirmedTokens = new Set();

function confirmEmailOnce(token) {
  if (!confirmRequests.has(token)) {
    const request = client
      .post("/api/v1/auth/confirm-email", { token })
      .then((response) => {
        confirmedTokens.add(token);
        return response;
      });
    confirmRequests.set(token, request);
  }
  return confirmRequests.get(token);
}

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

  // "loading" | "success" | "error". `error` holds { message, linkTo, linkLabel }.
  const [status, setStatus] = React.useState(token ? "loading" : "error");
  const [error, setError] = React.useState(
    token ? null : { message: "Liên kết không hợp lệ.", linkTo: "/login", linkLabel: "Về trang đăng nhập" },
  );

  React.useEffect(() => {
    if (!token) {
      return;
    }

    // `active` only gates state updates for THIS mount; it never cancels the
    // shared request. Whichever mount is alive when the promise settles reaches
    // a terminal state, so the spinner can never hang.
    let active = true;

    confirmEmailOnce(token)
      .then(() => {
        if (active) setStatus("success");
      })
      .catch((err) => {
        if (!active) return;
        console.error(err);
        const httpStatus = err?.response?.status;

        // Requirement 2 safety net: a 410/409 for a token that already succeeded
        // in this page load is our own duplicate submit echoing back, not a real
        // failure. (With the cache above this should not occur, but it guarantees
        // correctness if a duplicate ever races through.)
        if ((httpStatus === 410 || httpStatus === 409) && confirmedTokens.has(token)) {
          setStatus("success");
          return;
        }

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
      });

    return () => {
      active = false;
    };
  }, [token]);

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
