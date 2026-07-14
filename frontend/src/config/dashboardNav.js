export const expertNavItems = [
  {
    label: "Ngân hàng câu hỏi",
    path: "/expert/questions",
    icon: "database",
    children: [
      {
        label: "Tất cả câu hỏi",
        path: "/expert/questions",
      },
      {
        label: "Câu hỏi bị báo cáo",
        path: "/expert/questions/reported",
      }
    ]
  },
  {
    label: "Quản lý Tag",
    path: "/expert/tags",
    icon: "category"
  },
  {
    label: "Cài đặt hệ thống",
    path: "/expert/settings",
    icon: "settings",
    disabled: true
  }
];

export const adminNavItems = [
  {
    label: "Quản lý tài khoản",
    path: "/admin/accounts",
    icon: "group"
  },
  {
    label: "Đơn đăng ký giáo viên",
    path: "/admin/applications",
    icon: "fact_check"
  },
  {
    label: "Vai trò & Quyền",
    path: "/admin/roles",
    icon: "admin_panel_settings"
  }
];
