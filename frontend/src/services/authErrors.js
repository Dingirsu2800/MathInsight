// Shared auth error-code → Vietnamese message map (BR-11).
//
// The backend returns stable, machine-readable codes in an { code, message }
// body (see AuthErrorCodes.cs); developer-facing messages stay in English and
// the frontend localizes by code here. Extend THIS map instead of duplicating
// it in individual pages.

export const AUTH_ERROR_MESSAGES = {
  AUTH_INVALID_CREDENTIALS: "Email hoặc mật khẩu không đúng.",
  AUTH_ACCOUNT_DEACTIVATED: "Tài khoản đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.",
  AUTH_APPLICATION_PENDING: "Hồ sơ giáo viên đang chờ duyệt.",
  AUTH_APPLICATION_REJECTED: "Hồ sơ giáo viên đã bị từ chối.",
  AUTH_ACCOUNT_LOCKED: "Tài khoản tạm khóa do sai nhiều lần. Thử lại sau 15 phút.",
  AUTH_TOKEN_EXPIRED: "Liên kết đã hết hạn. Vui lòng thử lại.",
  AUTH_TOKEN_INVALID: "Liên kết không hợp lệ.",
  AUTH_EMAIL_ALREADY_CONFIRMED: "Email này đã được xác nhận. Vui lòng đăng nhập.",
  AUTH_CERTIFICATE_INVALID: "Chứng chỉ không hợp lệ.",
};

export const GENERIC_AUTH_ERROR = "Đã có lỗi xảy ra. Vui lòng thử lại sau.";

// Pull the backend error code out of an axios error, tolerating camel/Pascal casing.
export function getAuthErrorCode(err) {
  return err?.response?.data?.code || err?.response?.data?.Code || null;
}

// Localize an axios error by its backend code, falling back to `fallback`.
export function mapAuthError(err, fallback = GENERIC_AUTH_ERROR) {
  const code = getAuthErrorCode(err);
  if (code && AUTH_ERROR_MESSAGES[code]) {
    return AUTH_ERROR_MESSAGES[code];
  }
  return fallback;
}
