import { useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ScoreBadge from './ScoreBadge';

// TODO: Replace with API data from /grading/student/history
const MOCK_ROWS = [
  {
    id: 'MTH-10293',
    name: 'Đề ôn tập Chương 1: Hàm số',
    type: 'Luyện tập',
    typeBg: 'bg-primary-fixed text-primary',
    topic: 'Giải tích 12',
    date: '14/10/2023',
    time: '09:45 AM',
    duration: '45m',
    correct: 42,
    wrong: 6,
    skipped: 2,
    score: 8.4,
    submitType: 'Online',
    submitClass: 'border-emerald-success text-emerald-success',
  },
  {
    id: 'MTH-10288',
    name: 'Kiểm tra giữa kỳ I - Mã đề 04',
    type: 'K.Tra 1 tiết',
    typeBg: 'bg-tertiary-fixed text-tertiary',
    topic: 'Tổng hợp',
    date: '12/10/2023',
    time: '08:00 AM',
    duration: '90m',
    correct: 48,
    wrong: 2,
    skipped: 0,
    score: 9.6,
    submitType: 'Tại lớp',
    submitClass: 'border-primary text-primary',
  },
  {
    id: 'MTH-10250',
    name: 'Luyện đề thi THPT QG lần 1',
    type: 'Thi thử',
    typeBg: 'bg-error-container text-error',
    topic: 'Toàn bộ',
    date: '08/10/2023',
    time: '02:30 PM',
    duration: '90m',
    correct: 35,
    wrong: 12,
    skipped: 3,
    score: 7.0,
    submitType: 'Online',
    submitClass: 'border-emerald-success text-emerald-success',
  },
  {
    id: 'MTH-10242',
    name: 'Chuyên đề: Số phức nâng cao',
    type: 'Luyện tập',
    typeBg: 'bg-primary-fixed text-primary',
    topic: 'Giải tích 12',
    date: '05/10/2023',
    time: '04:15 PM',
    duration: '30m',
    correct: 28,
    wrong: 2,
    skipped: 0,
    score: 9.3,
    submitType: 'Online',
    submitClass: 'border-emerald-success text-emerald-success',
  },
  {
    id: 'MTH-10211',
    name: 'Kiểm tra 15p: Tọa độ Không gian',
    type: 'K.Tra 15p',
    typeBg: 'bg-surface-container-highest text-on-surface-variant',
    topic: 'Hình học 12',
    date: '01/10/2023',
    time: '10:30 AM',
    duration: '15m',
    correct: 9,
    wrong: 1,
    skipped: 0,
    score: 9.0,
    submitType: 'Tại lớp',
    submitClass: 'border-primary text-primary',
  },
];

export default function HistoryTable() {
  const [currentPage, setCurrentPage] = useState(1);
  const totalResults = 128;
  const totalPages = 26;

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-surface-container-low border-b border-whisper-border">
            <tr>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider w-12 text-center">#</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Tên bài thi</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Hình thức</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Chủ đề</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Ngày thực hiện</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">T.Gian</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Đ/S/B</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Điểm số</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Nộp bài</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-right">Chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-whisper-border">
            {MOCK_ROWS.map((row, i) => (
              <tr
                key={row.id}
                className="hover:bg-surface-container-low/50 transition-colors"
              >
                <td className="px-4 py-4 font-mono text-xs text-outline text-center">
                  {String(i + 1).padStart(2, '0')}
                </td>
                <td className="px-6 py-4">
                  <p className="text-sm font-bold text-on-surface truncate max-w-[240px]">{row.name}</p>
                  <p className="text-[12px] text-outline">ID: #{row.id}</p>
                </td>
                <td className="px-6 py-4">
                  <span className={`px-2.5 py-1 rounded-full text-[11px] font-bold uppercase tracking-tight ${row.typeBg}`}>
                    {row.type}
                  </span>
                </td>
                <td className="px-6 py-4 text-sm text-on-surface-variant">{row.topic}</td>
                <td className="px-6 py-4">
                  <p className="text-sm text-on-surface-variant">{row.date}</p>
                  <p className="text-[11px] text-outline">{row.time}</p>
                </td>
                <td className="px-4 py-4 font-mono text-xs text-on-surface-variant text-center">
                  {row.duration}
                </td>
                <td className="px-4 py-4 text-center">
                  <div className="flex justify-center gap-1 text-sm">
                    <span className="text-emerald-success font-bold">{row.correct}</span>
                    <span className="text-outline">/</span>
                    <span className="text-error font-bold">{row.wrong}</span>
                    <span className="text-outline">/</span>
                    <span className="text-on-surface-variant font-bold">{row.skipped}</span>
                  </div>
                </td>
                <td className="px-6 py-4 text-center">
                  <ScoreBadge score={row.score} />
                </td>
                <td className="px-6 py-4 text-center">
                  <span className={`px-2 py-0.5 rounded border text-[10px] font-bold uppercase ${row.submitClass}`}>
                    {row.submitType}
                  </span>
                </td>
                <td className="px-4 py-4 text-right">
                  <button className="w-8 h-8 rounded-full hover:bg-surface-container flex items-center justify-center text-primary transition-all">
                    <MaterialIcon name="visibility" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="px-6 py-4 bg-surface border-t border-whisper-border flex items-center justify-between">
        <p className="text-on-surface-variant text-sm">
          Hiển thị <span className="font-bold">1 - 5</span> trong <span className="font-bold">{totalResults}</span> kết quả
        </p>
        <div className="flex items-center gap-2">
          <PagButton disabled>{'\u2039'}</PagButton>
          {[1, 2, 3].map((p) => (
            <PagButton
              key={p}
              active={p === currentPage}
              onClick={() => setCurrentPage(p)}
            >
              {p}
            </PagButton>
          ))}
          <span className="px-1 text-outline">...</span>
          <PagButton onClick={() => setCurrentPage(totalPages)}>{totalPages}</PagButton>
          <PagButton>{'\u203A'}</PagButton>
        </div>
      </div>
    </div>
  );
}

function PagButton({ children, active, disabled, onClick }) {
  return (
    <button
      className={`w-9 h-9 rounded-lg flex items-center justify-center font-bold text-sm transition-colors ${
        active
          ? 'bg-primary text-white'
          : disabled
            ? 'border border-outline-variant text-outline opacity-50 cursor-not-allowed'
            : 'border border-outline-variant text-on-surface-variant hover:bg-surface-container-low'
      }`}
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  );
}
