import * as React from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { setAuthSession } from "../services/authStorage";
import { resolveHomePath } from "../utils/roleRoutes";

// Landing target for the backend's Google OAuth redirect:
//   {FrontendBaseUrl}/auth/google/success?accessToken=...&refreshToken=...&role=...
// Reads the tokens from the query string, stores them (same key set as normal login),
// and routes by role. The URL carries tokens, so every navigation uses { replace: true }
// to keep it out of browser history.
export default function GoogleSuccessPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  React.useEffect(() => {
    const accessToken = searchParams.get("accessToken");
    const refreshToken = searchParams.get("refreshToken");
    const role = searchParams.get("role");

    if (!accessToken || !refreshToken || !role) {
      navigate("/login?error=google_failed", { replace: true });
      return;
    }

    setAuthSession({ accessToken, refreshToken, roleName: role });
    navigate(resolveHomePath(role), { replace: true });
  }, [navigate, searchParams]);

  return (
    <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
      <div className="flex flex-col items-center gap-3 text-center">
        <div className="w-8 h-8 border-[3px] border-[#2f5fa8] border-t-transparent rounded-full animate-spin"></div>
        <p className="text-sm font-semibold text-[#1e2a4a]">Đang đăng nhập...</p>
      </div>
    </main>
  );
}
