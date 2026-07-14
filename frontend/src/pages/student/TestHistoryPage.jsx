import StudentLayout from '../../components/layout/StudentLayout';
import HistoryStatsRow from './test-history/HistoryStatsRow';
import HistoryFilters from './test-history/HistoryFilters';
import HistoryTable from './test-history/HistoryTable';

export default function TestHistoryPage() {
  return (
    <StudentLayout>
      <div className="space-y-8">
        {/* Page header */}
        <div>
          <h2 className="text-2xl font-semibold text-on-surface mb-1">Lịch sử làm bài</h2>
          <p className="text-on-surface-variant text-sm">
            Theo dõi tiến độ và xem lại chi tiết kết quả các bài kiểm tra của bạn.
          </p>
        </div>

        {/* Stats row */}
        <HistoryStatsRow />

        {/* Filters */}
        <HistoryFilters />

        {/* Data table with pagination */}
        <HistoryTable />
      </div>
    </StudentLayout>
  );
}
