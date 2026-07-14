import MaterialIcon from '../../../components/ui/MaterialIcon';

/**
 * Filter row for test history page with form/topic/date range selectors.
 * @param {{ onFilter?: Function }} props
 */
export default function HistoryFilters({ onFilter }) {
  return (
    <div className="bg-pure-surface border border-whisper-border p-4 rounded-xl shadow-sm flex flex-wrap items-end gap-4">
      {/* Hình thức */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[12px] font-bold text-on-surface-variant px-1">Hình thức</label>
        <select className="border border-outline-variant rounded-lg px-3 py-2 bg-surface text-sm focus:ring-2 focus:ring-primary focus:border-primary min-w-[160px] outline-none cursor-pointer">
          <option>Tất cả hình thức</option>
          <option>Luyện tập</option>
          <option>Kiểm tra 15p</option>
          <option>Kiểm tra 1 tiết</option>
          <option>Thi thử THPT QG</option>
        </select>
      </div>

      {/* Chủ đề */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[12px] font-bold text-on-surface-variant px-1">Chủ đề</label>
        <select className="border border-outline-variant rounded-lg px-3 py-2 bg-surface text-sm focus:ring-2 focus:ring-primary focus:border-primary min-w-[200px] outline-none cursor-pointer">
          <option>Tất cả chủ đề</option>
          <option>Hàm số & Đồ thị</option>
          <option>Nguyên hàm - Tích phân</option>
          <option>Số phức</option>
          <option>Hình học không gian</option>
          <option>Tọa độ trong không gian</option>
        </select>
      </div>

      {/* Khoảng thời gian */}
      <div className="flex flex-col gap-1.5">
        <label className="text-[12px] font-bold text-on-surface-variant px-1">Khoảng thời gian</label>
        <div className="flex items-center bg-surface border border-outline-variant rounded-lg px-3 py-2">
          <input
            className="bg-transparent border-none p-0 text-sm focus:ring-0 outline-none w-32"
            type="date"
          />
          <span className="mx-2 text-outline">→</span>
          <input
            className="bg-transparent border-none p-0 text-sm focus:ring-0 outline-none w-32"
            type="date"
          />
        </div>
      </div>

      {/* Filter button */}
      <div className="ml-auto">
        <button
          className="bg-primary text-white font-bold py-2.5 px-6 rounded-lg hover:opacity-90 active:scale-95 transition-all flex items-center gap-2"
          onClick={() => onFilter?.()}
        >
          <MaterialIcon name="filter_list" size={20} />
          Áp dụng lọc
        </button>
      </div>
    </div>
  );
}
