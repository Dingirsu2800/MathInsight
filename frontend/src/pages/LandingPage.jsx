import * as React from "react";
import { Link } from "react-router-dom";

// Marketing landing page for MathInsight. Reproduces the static mockup from
// landing-preview.html using the app's Tailwind + Material Symbols conventions.
// Font is inherited from the app body (Geist), which renders Vietnamese well,
// so no extra font dependency is loaded.

// Feature cards. Emoji icons from the mockup are replaced with Material Symbols,
// matching how the rest of the app renders icons.
const FEATURES = [
  {
    icon: "edit_document",
    title: "Luyện đề thi",
    desc: "Ngân hàng câu hỏi theo chủ đề, bám sát chương trình lớp 10–12.",
  },
  {
    icon: "target",
    title: "Điểm mục tiêu",
    desc: "Đặt mục tiêu 0–10 cho từng chủ đề và theo dõi khoảng cách còn lại.",
  },
  {
    icon: "menu_book",
    title: "Bài giảng",
    desc: "Học lại lý thuyết với bài giảng gắn liền tài liệu từng chủ đề.",
  },
  {
    icon: "smart_toy",
    title: "AI hỗ trợ giải thích",
    desc: "AI giải thích lời giải từng bước, giúp bạn hiểu sai ở đâu và vì sao.",
  },
];

// Signature target-score card rows: current score vs. target on a 0–10 scale.
const TOPICS = [
  { name: "Hàm số", current: 7.5, target: 8.0 },
  { name: "Hình học không gian", current: 6.0, target: 7.5 },
  { name: "Tích phân", current: 8.8, target: 9.0 },
  { name: "Xác suất", current: 5.5, target: 8.0 },
];

const STEPS = [
  {
    num: 1,
    title: "Đăng ký tài khoản",
    desc: "Tạo tài khoản học sinh và xác thực qua email — mất chưa tới một phút.",
  },
  {
    num: 2,
    title: "Luyện tập theo chủ đề",
    desc: "Chọn chủ đề, đặt điểm mục tiêu, rồi luyện đề để lấp khoảng trống kiến thức.",
  },
  {
    num: 3,
    title: "Theo dõi tiến bộ",
    desc: "Xem điểm từng chủ đề tiến gần mục tiêu ra sao sau mỗi buổi luyện.",
  },
];

function Logo({ className = "" }) {
  return (
    <span
      className={`w-[38px] h-[38px] rounded-[10px] bg-[#2f5fa8] flex items-center justify-center text-white font-bold text-xl ${className}`}
    >
      Σ
    </span>
  );
}

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-[#eef2f7] text-[#1e2a3a] antialiased transition duration-300">
      {/* NAV */}
      <nav className="sticky top-0 z-50 bg-white/80 backdrop-blur-md border-b border-[#e2e8f0]">
        <div className="max-w-[1160px] mx-auto px-6 h-[68px] flex items-center justify-between">
          <Link to="/" className="flex items-center gap-2.5 font-bold text-[19px] text-[#1e2a3a]">
            <Logo />
            MathInsight
          </Link>
          <div className="flex items-center gap-2">
            <Link
              to="/login"
              className="font-semibold text-[15px] rounded-[10px] px-5 py-2.5 text-[#2f5fa8] hover:bg-[#f2f7fc] transition-colors"
            >
              Đăng nhập
            </Link>
            <Link
              to="/register"
              className="font-semibold text-[15px] rounded-[10px] px-5 py-2.5 bg-[#2f5fa8] text-white hover:bg-[#244a83] transition-colors"
            >
              Đăng ký
            </Link>
          </div>
        </div>
      </nav>

      {/* HERO */}
      <header className="max-w-[1160px] mx-auto px-6 pt-20 pb-[72px]">
        <div className="grid grid-cols-1 md:grid-cols-[1.05fr_.95fr] gap-14 items-center">
          <div>
            <span className="inline-flex items-center gap-2 bg-[#e7effa] text-[#244a83] text-[13.5px] font-semibold px-3.5 py-1.5 rounded-full mb-[22px]">
              <span className="w-[7px] h-[7px] rounded-full bg-[#1d9e75]" />
              Dành cho học sinh lớp 10–12
            </span>
            <h1 className="text-[38px] md:text-[50px] leading-[1.12] font-bold tracking-[-0.02em] mb-5">
              Chinh phục Toán,
              <br />
              Đạt <span className="text-[#2f5fa8]">điểm mục tiêu</span> của bạn.
            </h1>
            <p className="text-[18px] text-[#5b6b80] mb-8 max-w-[520px]">
              Luyện đề theo từng chủ đề, đặt điểm mục tiêu 0–10 cho mọi phần kiến thức, và
              theo dõi khoảng cách tới mục tiêu sau từng buổi học.
            </p>
            <div className="flex gap-3.5 flex-wrap">
              <Link
                to="/register"
                className="font-semibold text-base rounded-[10px] px-7 py-3.5 bg-[#2f5fa8] text-white hover:bg-[#244a83] transition-colors"
              >
                Bắt đầu miễn phí
              </Link>
              <Link
                to="/login"
                className="font-semibold text-base rounded-[10px] px-7 py-3.5 bg-white text-[#2f5fa8] border border-[#e2e8f0] hover:bg-[#f2f7fc] transition-colors"
              >
                Đăng nhập
              </Link>
            </div>
            <div className="mt-[18px] text-sm text-[#5b6b80] flex items-center gap-2">
              <span className="text-[#1d9e75] font-bold">✓</span>
              Miễn phí đăng ký · Xác thực qua email
            </div>
          </div>

          {/* SIGNATURE: target-score card */}
          <div className="bg-white rounded-[20px] border border-[#e2e8f0] shadow-[0_18px_50px_-22px_rgba(36,74,131,0.35)] p-[26px]">
            <div className="flex items-center justify-between mb-1.5">
              <span className="font-bold text-base">Tiến độ theo chủ đề</span>
              <span className="text-[12.5px] font-semibold text-[#1d9e75] bg-[#e6f5ef] px-2.5 py-1 rounded-lg">
                Đang tiến bộ
              </span>
            </div>
            <p className="text-[13px] text-[#5b6b80] mb-5">
              Điểm hiện tại so với điểm mục tiêu bạn đặt
            </p>

            {TOPICS.map((topic) => (
              <div key={topic.name} className="mb-4 last:mb-0">
                <div className="flex justify-between text-sm mb-[7px]">
                  <span className="font-medium">{topic.name}</span>
                  <span className="text-[#5b6b80]">
                    <b className="text-[#1e2a3a]">{topic.current.toFixed(1)}</b> /{" "}
                    {topic.target.toFixed(1)}
                  </span>
                </div>
                <div className="h-[9px] rounded-full bg-[#eef2f7] overflow-hidden relative">
                  <div
                    className="h-full rounded-full bg-[#2f5fa8]"
                    style={{ width: `${topic.current * 10}%` }}
                  />
                  <div
                    className="absolute top-[-3px] w-0.5 h-[15px] bg-[#f0a020] rounded-sm"
                    style={{ left: `${topic.target * 10}%` }}
                  />
                </div>
              </div>
            ))}

            <div className="mt-5 pt-4 border-t border-[#e2e8f0] flex items-center gap-2.5 text-[13.5px] text-[#5b6b80]">
              <span className="inline-flex items-center gap-1.5">
                <span className="w-[11px] h-[11px] rounded-[3px] bg-[#2f5fa8]" />
                Điểm hiện tại
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="w-[11px] h-[11px] rounded-[3px] bg-[#f0a020]" />
                Điểm mục tiêu
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* FEATURES */}
      <section className="py-[76px]">
        <div className="max-w-[1160px] mx-auto px-6">
          <div className="text-center max-w-[620px] mx-auto mb-12">
            <div className="text-[#2f5fa8] font-semibold text-sm tracking-[0.04em] uppercase mb-3">
              Tính năng
            </div>
            <h2 className="text-[34px] font-bold tracking-[-0.01em] mb-3.5">
              Mọi thứ bạn cần để tiến bộ
            </h2>
            <p className="text-[#5b6b80] text-[17px]">
              Từ luyện đề đến chấm điểm — tập trung vào đúng phần kiến thức còn yếu.
            </p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
            {FEATURES.map((feature) => (
              <div
                key={feature.title}
                className="bg-white border border-[#e2e8f0] rounded-2xl p-[26px_22px] transition-all hover:-translate-y-1 hover:shadow-[0_16px_40px_-24px_rgba(36,74,131,0.4)] hover:border-[#cdd9e8]"
              >
                <div className="w-12 h-12 rounded-xl bg-[#e7effa] text-[#2f5fa8] flex items-center justify-center mb-4">
                  <span className="material-symbols-outlined text-2xl">{feature.icon}</span>
                </div>
                <h3 className="text-[17px] font-bold mb-2">{feature.title}</h3>
                <p className="text-[14.5px] text-[#5b6b80]">{feature.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* STEPS */}
      <section className="py-[76px] bg-white border-t border-b border-[#e2e8f0]">
        <div className="max-w-[1160px] mx-auto px-6">
          <div className="text-center max-w-[620px] mx-auto mb-12">
            <div className="text-[#2f5fa8] font-semibold text-sm tracking-[0.04em] uppercase mb-3">
              Cách hoạt động
            </div>
            <h2 className="text-[34px] font-bold tracking-[-0.01em]">Bắt đầu trong ba bước</h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-10 mt-2">
            {STEPS.map((step, index) => (
              <div key={step.num} className="text-center relative">
                <div className="w-[54px] h-[54px] rounded-full bg-[#2f5fa8] text-white font-bold text-[22px] flex items-center justify-center mx-auto mb-[18px]">
                  {step.num}
                </div>
                <h3 className="text-[18px] font-bold mb-2">{step.title}</h3>
                <p className="text-[#5b6b80] text-[15px] max-w-[280px] mx-auto">{step.desc}</p>
                {index < STEPS.length - 1 && (
                  <span className="hidden md:block absolute top-[27px] right-[-20px] w-10 h-0.5 bg-gradient-to-r from-[#2f5fa8] to-transparent" />
                )}
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA band */}
      <section className="py-[70px]">
        <div className="max-w-[1160px] mx-auto px-6">
          <div className="bg-[#2f5fa8] rounded-3xl px-6 py-14 sm:px-12 text-center text-white bg-[radial-gradient(circle_at_15%_20%,rgba(255,255,255,0.10),transparent_45%),radial-gradient(circle_at_85%_80%,rgba(255,255,255,0.08),transparent_45%)]">
            <h2 className="text-white text-[32px] font-bold mb-3">Sẵn sàng đạt điểm mục tiêu?</h2>
            <p className="text-[#dbe6f5] text-[17px] mb-7">
              Tạo tài khoản miễn phí và bắt đầu luyện tập ngay hôm nay.
            </p>
            <Link
              to="/register"
              style={{ backgroundColor: "#ffffff", color: "#2f5fa8", opacity: 1 }}
              className="inline-block font-semibold text-base rounded-[10px] px-7 py-3.5 border border-[#e2e8f0] hover:bg-[#f2f7fc] transition-colors"
            >
              Đăng ký ngay
            </Link>
          </div>
        </div>
      </section>

      {/* FOOTER */}
      <footer className="bg-[#0f2440] text-[#c4d0e0] pt-[52px] pb-[30px]">
        <div className="max-w-[1160px] mx-auto px-6">
          <div className="flex flex-col md:flex-row justify-between items-start flex-wrap gap-8 mb-9">
            <div className="max-w-[320px]">
              <Link to="/" className="flex items-center gap-2.5 font-bold text-[19px] text-white mb-3.5">
                <Logo className="!bg-white !text-[#2f5fa8]" />
                MathInsight
              </Link>
              <p className="text-[#8ea3bf] text-[14.5px]">
                Nền tảng luyện Toán theo chủ đề dành cho học sinh trung học phổ thông.
              </p>
            </div>
            <div className="flex gap-14">
              <div>
                <h4 className="text-white text-sm font-semibold mb-3.5">Sản phẩm</h4>
                <a href="#" className="block text-[#8ea3bf] hover:text-white text-[14.5px] mb-2.5">Luyện đề</a>
                <a href="#" className="block text-[#8ea3bf] hover:text-white text-[14.5px] mb-2.5">Bài giảng</a>
                <a href="#" className="block text-[#8ea3bf] hover:text-white text-[14.5px] mb-2.5">Điểm mục tiêu</a>
              </div>
              <div>
                <h4 className="text-white text-sm font-semibold mb-3.5">Thông tin</h4>
                <a href="#" className="block text-[#8ea3bf] hover:text-white text-[14.5px] mb-2.5">Về chúng tôi</a>
                <a href="#" className="block text-[#8ea3bf] hover:text-white text-[14.5px] mb-2.5">Liên hệ</a>
              </div>
            </div>
          </div>
          <div className="border-t border-[#1c375c] pt-[22px] text-[13.5px] text-[#7d92b0] text-center">
            © 2026 MathInsight. Bảo lưu mọi quyền.
          </div>
        </div>
      </footer>
    </div>
  );
}
