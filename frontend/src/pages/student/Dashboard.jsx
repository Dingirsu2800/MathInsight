import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../../services/api';
import StudentLayout from '../../components/layout/StudentLayout';
import WelcomeBanner from './dashboard/WelcomeBanner';
import StatCards from './dashboard/StatCards';
import WeakTopicsCard from './dashboard/WeakTopicsCard';
import RecentActivityCard from './dashboard/RecentActivityCard';
import RecommendedLecturesCard from './dashboard/RecommendedLecturesCard';
import WeeklyTargetsCard from './dashboard/WeeklyTargetsCard';
import StudyHeatmapCard from './dashboard/StudyHeatmapCard';
import BadgeCarouselCard from './dashboard/BadgeCarouselCard';
import useCurrentUser from '../../hooks/useCurrentUser';
import { Dialog, DialogHeader, DialogTitle, DialogContent, DialogFooter } from '../../components/ui/dialog';
import { Button } from '../../components/ui/button';

export default function StudentDashboard() {
  const navigate = useNavigate();
  const { profile } = useCurrentUser();
  const [isApiAvailable, setIsApiAvailable] = useState(true);
  const [showWelcomeDialog, setShowWelcomeDialog] = useState(false);

  useEffect(() => {
    // Pre-check API availability — individual cards handle their own data
    api.get('/reports/competency-summary')
      .catch(() => setIsApiAvailable(false));
  }, []);

  useEffect(() => {
    if (profile && profile.roleName === 'Student') {
      const createdDate = new Date(profile.createdTime);
      const now = new Date();
      // Check if account created in the last 7 days
      const isNewAccount = (now - createdDate) < 7 * 24 * 60 * 60 * 1000;
      const dismissed = localStorage.getItem(`welcome_dismissed_${profile.username}`) === 'true';

      if (isNewAccount && !dismissed) {
        setShowWelcomeDialog(true);
      }
    }
  }, [profile]);

  const handleCloseWelcomeDialog = () => {
    if (profile?.username) {
      localStorage.setItem(`welcome_dismissed_${profile.username}`, 'true');
    }
    setShowWelcomeDialog(false);
  };

  const handleStartPractice = () => {
    if (profile?.username) {
      localStorage.setItem(`welcome_dismissed_${profile.username}`, 'true');
    }
    setShowWelcomeDialog(false);
    navigate('/student/test');
  };

  return (
    <StudentLayout>
      <div className="space-y-8">
        {/* Hero welcome banner */}
        <WelcomeBanner />

        {/* Metric stat cards row */}
        <StatCards />

        {/* Two-column layout: weak topics + recent activity */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div className="lg:col-span-5">
            <WeakTopicsCard />
          </div>
          <div className="lg:col-span-7">
            <RecentActivityCard />
          </div>
        </div>

        {/* Two-column layout: recommended lectures + weekly targets */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div className="lg:col-span-7">
            <RecommendedLecturesCard />
          </div>
          <div className="lg:col-span-5">
            <WeeklyTargetsCard />
          </div>
        </div>

        {/* Two-column layout: heatmap + badges */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div className="lg:col-span-7">
            <StudyHeatmapCard />
          </div>
          <div className="lg:col-span-5">
            <BadgeCarouselCard />
          </div>
        </div>
      </div>

      {/* Onboarding Welcome Dialog for new Student accounts */}
      <Dialog 
        isOpen={showWelcomeDialog} 
        onClose={handleCloseWelcomeDialog} 
        className="max-w-md p-6 rounded-2xl border border-whisper-border bg-pure-surface shadow-2xl animate-in fade-in zoom-in-95 duration-200"
      >
        <div className="flex flex-col items-center text-center select-none py-4">
          <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center text-primary mb-5">
            <span className="material-symbols-outlined text-[36px] animate-bounce">celebration</span>
          </div>

          <DialogHeader className="border-b-0 pb-0 pr-0 mb-3 flex flex-col items-center">
            <DialogTitle className="text-xl font-extrabold text-on-background tracking-tight">
              Chào mừng bạn đến với MathInsight! 🎉
            </DialogTitle>
          </DialogHeader>

          <DialogContent className="py-2">
            <div className="text-sm text-on-surface-variant leading-relaxed select-text space-y-4">
              <p>
                Chúng tôi rất vui mừng được đồng hành cùng bạn trên con đường chinh phục Toán học!
              </p>
              <div className="bg-surface-container-low border border-whisper-border rounded-xl p-4 text-left space-y-3">
                <h4 className="text-xs font-bold uppercase tracking-wider text-primary">Hướng dẫn nhanh:</h4>
                <div className="flex items-start gap-2.5">
                  <span className="material-symbols-outlined text-primary text-[18px] mt-0.5">ads_click</span>
                  <span className="text-xs font-medium text-on-surface">Click <strong>Bắt đầu luyện tập</strong> bên dưới để vào khu vực làm bài.</span>
                </div>
                <div className="flex items-start gap-2.5">
                  <span className="material-symbols-outlined text-primary text-[18px] mt-0.5">quiz</span>
                  <span className="text-xs font-medium text-on-surface">Lựa chọn <strong>Đề thi</strong> hoặc <strong>Luyện theo chủ đề</strong> để bắt đầu làm bài.</span>
                </div>
                <div className="flex items-start gap-2.5">
                  <span className="material-symbols-outlined text-primary text-[18px] mt-0.5">insights</span>
                  <span className="text-xs font-medium text-on-surface">Sau khi làm xong, xem báo cáo năng lực và các khuyến nghị học tập để tiến bộ nhanh nhất!</span>
                </div>
              </div>
            </div>
          </DialogContent>

          <DialogFooter className="border-t-0 pt-0 w-full mt-6 flex flex-col sm:flex-row gap-2 justify-center">
            <Button 
              type="button" 
              variant="outline" 
              onClick={handleCloseWelcomeDialog}
              className="w-full sm:w-auto min-h-[44px] px-6 text-xs text-on-surface hover:bg-surface-container"
            >
              Bỏ qua
            </Button>
            <Button 
              type="button" 
              variant="primary" 
              onClick={handleStartPractice}
              className="w-full sm:w-auto min-h-[44px] px-6 text-xs font-bold flex items-center justify-center gap-1.5 shadow-md shadow-primary/20 hover:shadow-lg transition-all"
            >
              <span>Bắt đầu luyện tập</span>
              <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
            </Button>
          </DialogFooter>
        </div>
      </Dialog>
    </StudentLayout>
  );
}
