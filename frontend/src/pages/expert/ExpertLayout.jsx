import * as React from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { cn } from "../../utils/cn";

export default function ExpertLayout({ children }) {
  const location = useLocation();
  const navigate = useNavigate();
  const [darkMode, setDarkMode] = React.useState(() => {
    return localStorage.getItem("theme") === "dark" || 
      (!localStorage.getItem("theme") && window.matchMedia("(prefers-color-scheme: dark)").matches);
  });

  React.useEffect(() => {
    if (darkMode) {
      document.documentElement.classList.add("dark");
      localStorage.setItem("theme", "dark");
    } else {
      document.documentElement.classList.remove("dark");
      localStorage.setItem("theme", "light");
    }
  }, [darkMode]);

  const navItems = [
    {
      name: "Ngân hàng câu hỏi",
      path: "/expert/questions",
      icon: "database"
    },
    {
      name: "Soạn thảo LaTeX",
      path: "/expert/latex-helper",
      icon: "functions"
    },
    {
      name: "Quản lý chủ đề",
      path: "/expert/topics",
      icon: "category"
    },
    {
      name: "Cài đặt hệ thống",
      path: "/expert/settings",
      icon: "settings"
    }
  ];

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-background text-on-background font-body select-none">
      {/* SideNavBar */}
      <aside className="w-sidebar-width h-screen bg-surface-container-low border-r border-whisper-border flex flex-col py-gutter px-4 z-20 shrink-0">
        {/* Brand */}
        <div className="flex items-center gap-3 mb-8 px-2">
          <div className="w-10 h-10 rounded-lg bg-primary-container text-on-primary-container flex items-center justify-center font-headline-md font-bold">
            M
          </div>
          <div>
            <h1 className="font-bold text-[18px] text-primary">MathPro Expert</h1>
            <p className="text-[12px] text-on-surface-variant">Quản trị nội dung</p>
          </div>
        </div>

        {/* CTA */}
        <button
          onClick={() => navigate("/expert/questions/new")}
          className="w-full mb-8 bg-primary text-on-primary py-3 px-4 rounded-lg font-bold text-xs uppercase tracking-wider flex items-center justify-center gap-2 hover:bg-primary/90 transition-all active:translate-y-px cursor-pointer"
        >
          <span className="material-symbols-outlined text-[18px]">add</span>
          Tạo câu hỏi mới
        </button>

        {/* Navigation Tabs */}
        <nav className="flex-1 flex flex-col gap-1">
          {navItems.map((item) => {
            const isActive = location.pathname.startsWith(item.path);
            return (
              <Link
                key={item.path}
                to={item.path}
                className={cn(
                  "flex items-center gap-3 px-3 py-3 rounded-lg transition-all active:-translate-y-px",
                  isActive
                    ? "text-primary font-bold bg-surface-container-high"
                    : "text-on-surface-variant hover:bg-surface-container"
                )}
              >
                <span 
                  className="material-symbols-outlined" 
                  style={{ fontVariationSettings: isActive ? "'FILL' 1" : "'FILL' 0" }}
                >
                  {item.icon}
                </span>
                <span className="text-[15px]">{item.name}</span>
              </Link>
            );
          })}
        </nav>

        {/* Footer Tabs */}
        <div className="mt-auto flex flex-col gap-1 border-t border-whisper-border pt-4">
          <a
            href="#"
            className="flex items-center gap-3 px-3 py-2 rounded-lg text-on-surface-variant hover:bg-surface-container transition-colors"
          >
            <span className="material-symbols-outlined">help</span>
            <span className="text-[14px]">Trợ giúp</span>
          </a>
          <Link
            to="/login"
            className="flex items-center gap-3 px-3 py-2 rounded-lg text-error hover:bg-error/10 transition-colors"
          >
            <span className="material-symbols-outlined">logout</span>
            <span className="text-[14px] font-bold">Đăng xuất</span>
          </Link>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 h-screen overflow-hidden">
        {/* TopAppBar */}
        <header className="h-header-height border-b border-whisper-border bg-surface flex justify-between items-center w-full px-gutter z-10 sticky top-0 shrink-0">
          <div className="flex items-center gap-8">
            <h2 className="text-[20px] font-bold text-primary">Hệ thống Quản lý Toán học</h2>
            <nav className="hidden md:flex gap-6 h-full items-center">
              <a href="#" className="text-[14px] text-primary border-b-2 border-primary pb-1 font-bold h-full flex items-center pt-1 hover:text-primary active:scale-95 transition-all">
                Tổng quan
              </a>
              <a href="#" className="text-[14px] text-on-surface-variant hover:text-primary transition-colors h-full flex items-center active:scale-95 transition-all">
                Phê duyệt
              </a>
              <a href="#" className="text-[14px] text-on-surface-variant hover:text-primary transition-colors h-full flex items-center active:scale-95 transition-all">
                Báo cáo
              </a>
            </nav>
          </div>

          {/* Right: Actions */}
          <div className="flex items-center gap-4">
            <button className="text-[13px] text-primary border border-primary px-4 py-2 rounded flex items-center gap-2 hover:bg-primary/5 transition-colors cursor-pointer font-bold">
              <span className="material-symbols-outlined text-[18px]">download</span>
              Xuất dữ liệu
            </button>
            <div className="flex items-center gap-2 border-l border-whisper-border pl-4">
              <button className="p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer">
                <span className="material-symbols-outlined">notifications</span>
              </button>
              <button
                onClick={() => setDarkMode(!darkMode)}
                className="p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined">
                  {darkMode ? "light_mode" : "dark_mode"}
                </span>
              </button>
              <img
                alt="Người dùng"
                className="w-8 h-8 rounded-full ml-2 object-cover border border-whisper-border"
                src="https://lh3.googleusercontent.com/aida-public/AB6AXuDmU2aXEUQTXtHNvhQ8tbY5My18gP1gpQo1ZG4RfIX5zUuQMHKB7cVzvtDd1gxvcRRTKCgoHnkotPjn28sXURMaf3ieGdLjOTt7Vxof-v2_QIZ9fTVA4YcZlPSk64VIwW6kU57F1IJXBfmfoXLDFCuHi87FQiGc094rPEs02KT4JX6i5KUIpHNFLDBTUqiKLfbEDU00rkJ-Jic3-leXguz3DJUqj8bCzYTJvWIEOAU3Vv0mWqfVkYHgR7Bk32wGffShJ1fDnfFLU0w"
              />
            </div>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 overflow-y-auto bg-canvas-white">
          {children}
        </main>
      </div>
    </div>
  );
}
