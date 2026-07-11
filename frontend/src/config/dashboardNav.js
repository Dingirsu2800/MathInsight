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
