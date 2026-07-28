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
  AUTH_GOOGLE_FAILED: "Đăng nhập bằng Google thất bại. Vui lòng thử lại.",
  // UC-03 (đổi mật khẩu)
  AUTH_INVALID_CURRENT_PASSWORD: "Mật khẩu hiện tại không đúng.",
  AUTH_SAME_PASSWORD: "Mật khẩu mới phải khác mật khẩu hiện tại.",
  AUTH_NO_PASSWORD_SET: "Tài khoản này đăng nhập bằng Google, không có mật khẩu để đổi.",
};

// BR-08 in Vietnamese. Shown as the newPassword hint, as its client-side validation message,
// and as the copy for a backend 400 on that field — one string so the three can never drift.
export const PASSWORD_POLICY_HINT =
  "Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";

export const GENERIC_AUTH_ERROR = "Đã có lỗi xảy ra. Vui lòng thử lại sau.";

// The Google OAuth flow is a browser redirect, so a failure arrives as a ?error= query value
// on /login (not an axios { code } body). Map those values onto the shared message map.
const OAUTH_URL_ERROR_CODES = {
  google_failed: "AUTH_GOOGLE_FAILED",
};

export function mapOAuthUrlError(errorParam) {
  const code = OAUTH_URL_ERROR_CODES[errorParam];
  return (code && AUTH_ERROR_MESSAGES[code]) || GENERIC_AUTH_ERROR;
}

// Pull the backend error code out of an axios error, tolerating camel/Pascal casing.
export function getAuthErrorCode(err) {
  return err?.response?.data?.code || err?.response?.data?.Code || null;
}

// ASP.NET ValidationProblemDetails keys its errors dict by PascalCase property name;
// our form state uses camelCase. Lowercasing the first char lines them up.
export function toFieldKey(pascalKey) {
  if (!pascalKey) return pascalKey;
  return pascalKey.charAt(0).toLowerCase() + pascalKey.slice(1);
}

// Vietnamese copy for backend field-validation failures, keyed by camelCase field name.
//
// ASP.NET's ValidationProblemDetails messages are English and developer-facing ("The field
// CurrentGrade must be between 10 and 12.") — they must never reach the user. We translate by
// FIELD rather than by message text, because the English wording is an implementation detail
// of DataAnnotations and would break any string matching the moment it changes.
export const VALIDATION_FIELD_MESSAGES = {
  firstName: "Vui lòng nhập tên (tối đa 50 ký tự).",
  lastName: "Vui lòng nhập họ và tên đệm (tối đa 50 ký tự).",
  phoneNumber: "Số điện thoại không hợp lệ (tối đa 20 ký tự).",
  dateOfBirth: "Ngày sinh không hợp lệ.",
  gender: "Giới tính không hợp lệ.",
  school: "Tên trường không hợp lệ (tối đa 100 ký tự).",
  currentGrade: "Khối lớp phải từ 10 đến 12.",
  biography: "Giới thiệu bản thân không hợp lệ.",
  specialty: "Chuyên môn không hợp lệ (tối đa 100 ký tự).",
  // UC-03. A backend 400 on NewPassword can only be the BR-08 policy (the DTO's only rule
  // beyond [Required]), so the hint doubles as the error copy.
  currentPassword: "Vui lòng nhập mật khẩu hiện tại.",
  newPassword: PASSWORD_POLICY_HINT,
};

// Shown when the backend rejects a field we have no specific Vietnamese copy for. Never
// falls through to the backend's English text.
export const GENERIC_VALIDATION_ERROR = "Thông tin không hợp lệ.";

// Turn a 400 ValidationProblemDetails into { fieldName: "<Vietnamese message>" }, ready to
// render under the matching input. Returns null when the error is not a field-validation 400,
// so callers can fall back to mapAuthError. Lives here rather than in each page — the same
// inline loop is currently repeated in the register/reset pages and can adopt this later.
export function mapValidationErrors(err) {
  const backendErrors = err?.response?.data?.errors;
  if (err?.response?.status !== 400 || !backendErrors) {
    return null;
  }

  const mapped = {};
  Object.keys(backendErrors).forEach((key) => {
    const field = toFieldKey(key);
    mapped[field] = VALIDATION_FIELD_MESSAGES[field] || GENERIC_VALIDATION_ERROR;
  });
  return mapped;
}

// Localize an axios error by its backend code, falling back to `fallback`.
export function mapAuthError(err, fallback = GENERIC_AUTH_ERROR) {
  const code = getAuthErrorCode(err);
  if (code && AUTH_ERROR_MESSAGES[code]) {
    return AUTH_ERROR_MESSAGES[code];
  }
  return fallback;
}
