import * as React from 'react';

/**
 * ExamLayout — a distraction-free, full-screen wrapper used exclusively during
 * an active test / exam session.  There is intentionally no sidebar, topbar, or
 * any navigation chrome so the student can focus entirely on the questions.
 *
 * The dark-mode state is kept alive here (reading from localStorage) so that the
 * student's colour-scheme preference is still respected even without the normal
 * DashboardLayout being mounted.
 */
export default function ExamLayout({ children }) {
  React.useEffect(() => {
    const stored = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (stored === 'dark' || (!stored && prefersDark)) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }, []);

  return (
    <div className="min-h-screen w-screen bg-canvas-white text-on-background font-body overflow-y-auto">
      {children}
    </div>
  );
}
