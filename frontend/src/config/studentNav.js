// "Mục tiêu" and "Thành tích" have no page yet, so they land on the catch-all
// "Không tìm thấy trang" screen. That is intended — the catch-all no longer redirects to
// /login, so an unbuilt page reads as an unbuilt page rather than a logged-out session.
export const studentNavItems = [
  { label: "Tổng quan", path: "/student/dashboard", icon: "dashboard" },
  // App.jsx registers "/student/test" (singular).
  { label: "Làm bài", path: "/student/test", icon: "edit_document" },
  { label: "Lịch sử làm bài", path: "/student/history", icon: "history" },
  { label: "Bài giảng", path: "/student/lectures", icon: "menu_book" },
  { label: "Mục tiêu", path: "/student/targets", icon: "ads_click" },
  { label: "Thành tích", path: "/student/achievements", icon: "military_tech" },
  { label: "Năng lực", path: "/student/competency", icon: "psychology" },
];

export const studentTopNavItems = [
  { label: "Trang chủ", to: "/student/dashboard" },
  { label: "Thống kê", to: "/student/history" },
  { label: "Cài đặt", to: "/student/settings", disabled: true },
];
