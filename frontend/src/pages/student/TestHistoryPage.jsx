import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import StudentLayout from '../../components/layout/StudentLayout';
import HistoryStatsRow from './test-history/HistoryStatsRow';
import HistoryFilters from './test-history/HistoryFilters';
import HistoryTable from './test-history/HistoryTable';

export default function TestHistoryPage() {
  const navigate = useNavigate();
  const [filters, setFilters] = useState({
    testFormat: '',
    fromDate: '',
    toDate: '',
  });

  const handleFilter = (newFilters) => {
    setFilters(newFilters);
  };

  const handleViewDetail = (sessionId) => {
    navigate(`/student/test-result/${sessionId}`);
  };

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
        <HistoryFilters onFilter={handleFilter} />

        {/* Data table with pagination */}
        <HistoryTable filters={filters} onViewDetail={handleViewDetail} />
      </div>
    </StudentLayout>
  );
}

