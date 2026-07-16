import client from "./questionBankApiClient";
import { getRefreshToken, clearAuthSession } from "./authStorage";

// Shared logout used by every authenticated surface.
//
// Kept in its own module (not authStorage.js) so that authStorage stays a
// dependency-free storage helper — this file is the one place that composes the
// axios client with the storage helpers.
//
// The request goes through `client`, whose 401 interceptor deliberately skips the
// token-refresh flow for /api/v1/auth/* endpoints (isAuthEndpoint), so a 401 here is
// never turned into a refresh attempt.
export async function logout() {
  // BR-10: the backend requires the refresh token in the body — it deletes that refresh
  // token from Redis and blacklists the access-token jti. The request interceptor attaches
  // the (still-present) access token as the Authorization header for the [Authorize] endpoint.
  const refreshToken = getRefreshToken();

  try {
    await client.post("/api/v1/auth/logout", { refreshToken });
  } catch (err) {
    // Even if the server call fails (network error, expired access token, missing token),
    // the local session must still be cleared so the user is logged out client-side.
    console.error("Đăng xuất phía máy chủ thất bại:", err);
  } finally {
    clearAuthSession();
  }

  // Hard redirect: fully resets in-memory app state and lands on the login page.
  window.location.href = "/login";
}
