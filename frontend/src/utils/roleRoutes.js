// Landing page per role after a successful login (password login and Google OAuth both
// use this). Kept here so every entry point routes roles identically.
export function resolveHomePath(roleName) {
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
