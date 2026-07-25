import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';

/**
 * Filter row for test history page.
 * Calls onFilter({ testFormat, fromDate, toDate }) on submit.
 * testFormat: '' = all | 'Practice' | 'Exam'
 *
 * @param {{ onFilter?: (filters: object) => void }} props
 */
export default function HistoryFilters({ onFilter }) {
  const [testFormat, setTestFormat] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  const handleApply = () => {
    onFilter?.({ testFormat, fromDate, toDate });
  };

  return (
    <div className="bg-pure-surface border border-whisper-border p-4 rounded-xl shadow-sm flex flex-wrap items-end gap-4">
      {/* Hình thức */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[12px] font-bold text-on-surface-variant px-1">Hình thức</label>
        <select
          className="border border-outline-variant rounded-lg px-3 py-2 bg-surface text-sm focus:ring-2 focus:ring-primary focus:border-primary min-w-[160px] outline-none cursor-pointer"
          value={testFormat}
          onChange={(e) => setTestFormat(e.target.value)}
        >
          <option value="">Tất cả hình thức</option>
          <option value="Practice">Luyện tập</option>
          <option value="Exam">Kiểm tra / Thi thử</option>
        </select>
      </div>

      {/* Khoảng thời gian */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[12px] font-bold text-on-surface-variant px-1">Khoảng thời gian</label>
        <div className="flex items-center bg-surface border border-outline-variant rounded-lg px-3 py-2">
          <input
            className="bg-transparent border-none p-0 text-sm focus:ring-0 outline-none w-32"
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
          <span className="mx-2 text-outline">→</span>
          <input
            className="bg-transparent border-none p-0 text-sm focus:ring-0 outline-none w-32"
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>
      </div>

      {/* Filter button */}
      <div className="ml-auto">
        <button
          className="bg-primary text-white font-bold py-2.5 px-6 rounded-lg hover:opacity-90 active:scale-95 transition-all flex items-center gap-2"
          onClick={handleApply}
        >
          <MaterialIcon name="filter_list" size={20} />
          Áp dụng lọc
        </button>
      </div>
    </div>
  );
}


