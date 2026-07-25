import StudentLayout from '../../components/layout/StudentLayout';
import CompetencySummaryCard from './competency/CompetencySummaryCard';
import RadarChartCard from './competency/RadarChartCard';
import TopicMasteryGrid from './competency/TopicMasteryGrid';
import HistoricalProgressChart from './competency/HistoricalProgressChart';
import ImprovementCTACard from './competency/ImprovementCTACard';

export default function CompetencyPage() {
  return (
    <StudentLayout>
      <div className="space-y-8">
        {/* Top row: Summary gauge + Radar chart */}
        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-12 lg:col-span-4">
            <CompetencySummaryCard />
          </div>
          <div className="col-span-12 lg:col-span-8">
            <RadarChartCard />
          </div>
        </div>

        {/* Topic mastery grid */}
        <TopicMasteryGrid />

        {/* Historical progress chart */}
        <HistoricalProgressChart />

        {/* Improvement CTA + Suggestions */}
        <ImprovementCTACard />
      </div>
    </StudentLayout>
  );
}
