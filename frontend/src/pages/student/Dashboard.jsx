import { useEffect, useState } from 'react';
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

export default function StudentDashboard() {
  const [isApiAvailable, setIsApiAvailable] = useState(true);

  useEffect(() => {
    // Pre-check API availability — individual cards handle their own data
    api.get('/reports/competency-summary')
      .catch(() => setIsApiAvailable(false));
  }, []);

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
    </StudentLayout>
  );
}
