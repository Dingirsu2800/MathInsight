// Landing page per role after a successful login (password login and Google OAuth both
// use this). Kept here so every entry point routes roles identically.

// BR-06. A Teacher whose application is still Pending or was Rejected may sign in, but the only
// screen they can use is their own application — every real teacher endpoint is refused by the
// backend's "TeacherApproved" policy. "approved" and "none" (an Admin-created teacher, UC-11)
// take the normal route.
export const TEACHER_APPLICATION_PATH = "/teacher/application";

export function IsApplicantStatus(applicationStatus) {
  const status = String(applicationStatus || "").toLowerCase();
  return status === "pending" || status === "rejected";
}

export function resolveHomePath(roleName, applicationStatus) {
  const role = String(roleName || "").toLowerCase();

  if (role === "teacher" && IsApplicantStatus(applicationStatus)) {
    return TEACHER_APPLICATION_PATH;
  }

  switch (role) {
    case "student":
      return "/student/dashboard";
    case "teacher":
      return "/teacher/lectures";
    case "expert":
      return "/expert/questions";
    case "admin":
      return "/admin/accounts";
    default:
      return "/";
  }
}
