import { useCallback, useEffect, useState } from 'react';
import MaterialIcon from '../../../components/ui/MaterialIcon';
import ScoreBadge from './ScoreBadge';
import { getSessionHistory } from '../../../services/gradingApi';

/** Derive chip style from testFormat */
function formatChip(testFormat) {
  if (testFormat === 'Exam')
    return { label: 'K.Tra', bg: 'bg-tertiary-fixed text-tertiary' };
  return { label: 'Luyện tập', bg: 'bg-primary-fixed text-primary' };
}

/** Derive submit chip style from submissionType */
function submitChip(submissionType) {
  if (submissionType === 'StudentSubmit')
    return { label: 'Online', cls: 'border-emerald-success text-emerald-success' };
  if (submissionType === 'TimeoutSubmit')
    return { label: 'Hết giờ', cls: 'border-amber-warning text-amber-warning' };
  if (submissionType === 'SystemSubmit')
    return { label: 'Hệ thống', cls: 'border-outline text-outline' };
  return { label: '—', cls: 'border-outline-variant text-outline' };
}

/** Format ISO date string to "DD/MM/YYYY\nHH:MM" */
function formatDate(isoString) {
  if (!isoString) return { date: '—', time: '' };
  const d = new Date(isoString);
  const date = d.toLocaleDateString('vi-VN');
  const time = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  return { date, time };
}

/**
 * HistoryTable: paginated list of graded sessions from GET /api/v1/grading/student/history.
 * Accepts filter state from parent (TestHistoryPage) via props.
 *
 * @param {{ testFormat?: string, fromDate?: string, toDate?: string }} filters
 * @param {(sessionId: string) => void} onViewDetail - callback when user clicks "Chi tiết"
 */
export default function HistoryTable({ filters = {}, onViewDetail }) {
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  const PAGE_SIZE = 20;

  const fetchPage = useCallback(
    async (page) => {
      setLoading(true);
      setError(false);
      try {
        const data = await getSessionHistory({
          page,
          pageSize: PAGE_SIZE,
          testFormat: filters.testFormat || undefined,
          fromDate: filters.fromDate || undefined,
          toDate: filters.toDate || undefined,
        });
        setRows(data.items ?? []);
        setTotalCount(data.totalCount ?? 0);
        setTotalPages(data.totalPages ?? 1);
        setCurrentPage(data.page ?? page);
      } catch {
        setError(true);
      } finally {
        setLoading(false);
      }
    },
    [filters.testFormat, filters.fromDate, filters.toDate]
  );

  // Re-fetch when filters change (reset to page 1)
  useEffect(() => {
    setCurrentPage(1);
    fetchPage(1);
  }, [fetchPage]);

  const handlePage = (p) => {
    if (p < 1 || p > totalPages || p === currentPage) return;
    fetchPage(p);
  };

  // Skeleton rows
  const skeletonRow = (i) => (
    <tr key={i} className="animate-pulse">
      {[...Array(10)].map((_, j) => (
        <td key={j} className="px-4 py-4">
          <div className="h-3 bg-surface-container rounded w-full" />
        </td>
      ))}
    </tr>
  );

  return (
    <div className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-surface-container-low border-b border-whisper-border">
            <tr>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider w-12 text-center">#</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Tên bài thi</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Hình thức</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">Ngày thực hiện</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider">T.Gian</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Đ/S/B</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Điểm số</th>
              <th className="px-6 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-center">Nộp bài</th>
              <th className="px-4 py-4 text-[12px] font-bold text-on-surface-variant uppercase tracking-wider text-right">Chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-whisper-border">
            {/* Loading skeletons */}
            {loading && [...Array(5)].map((_, i) => skeletonRow(i))}

            {/* Error state */}
            {!loading && error && (
              <tr>
                <td colSpan={9} className="px-6 py-10 text-center text-sm text-outline">
                  Không thể tải lịch sử bài làm. Vui lòng thử lại.
                </td>
              </tr>
            )}

            {/* Empty state */}
            {!loading && !error && rows.length === 0 && (
              <tr>
                <td colSpan={9} className="px-6 py-10 text-center text-sm text-outline">
                  Chưa có bài làm nào.
                </td>
              </tr>
            )}

            {/* Data rows */}
            {!loading && !error && rows.map((row, i) => {
              const chip = formatChip(row.testFormat);
              const submit = submitChip(row.submissionType);
              const { date, time } = formatDate(row.submittedAt);
              const offset = (currentPage - 1) * PAGE_SIZE;

              return (
                <tr
                  key={row.sessionId}
                  className="hover:bg-surface-container-low/50 transition-colors"
                >
                  <td className="px-4 py-4 font-mono text-xs text-outline text-center">
                    {String(offset + i + 1).padStart(2, '0')}
                  </td>
                  <td className="px-6 py-4">
                    <p className="text-sm font-bold text-on-surface truncate max-w-[240px]">
                      {row.testFormat === 'Exam' ? 'Kiểm tra' : 'Luyện tập'}
                    </p>
                    <p className="text-[12px] text-outline font-mono">
                      #{row.sessionId.substring(0, 8).toUpperCase()}
                    </p>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`px-2.5 py-1 rounded-full text-[11px] font-bold uppercase tracking-tight ${chip.bg}`}>
                      {chip.label}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <p className="text-sm text-on-surface-variant">{date}</p>
                    <p className="text-[11px] text-outline">{time}</p>
                  </td>
                  <td className="px-4 py-4 font-mono text-xs text-on-surface-variant text-center">
                    {row.durationMinutes != null ? `${row.durationMinutes}m` : '—'}
                  </td>
                  <td className="px-4 py-4 text-center">
                    <div className="flex justify-center gap-1 text-sm">
                      <span className="text-emerald-success font-bold">{row.numCorrect}</span>
                      <span className="text-outline">/</span>
                      <span className="text-error font-bold">{row.numIncorrect}</span>
                      <span className="text-outline">/</span>
                      <span className="text-on-surface-variant font-bold">{row.numAbandoned}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-center">
                    <ScoreBadge score={row.score} />
                  </td>
                  <td className="px-6 py-4 text-center">
                    <span className={`px-2 py-0.5 rounded border text-[10px] font-bold uppercase ${submit.cls}`}>
                      {submit.label}
                    </span>
                  </td>
                  <td className="px-4 py-4 text-right">
                    <button
                      className="w-8 h-8 rounded-full hover:bg-surface-container flex items-center justify-center text-primary transition-all"
                      onClick={() => onViewDetail?.(row.sessionId)}
                      title="Xem chi tiết"
                    >
                      <MaterialIcon name="visibility" />
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="px-6 py-4 bg-surface border-t border-whisper-border flex items-center justify-between">
        <p className="text-on-surface-variant text-sm">
          {loading ? (
            <span className="animate-pulse">Đang tải...</span>
          ) : (
            <>
              Hiển thị{' '}
              <span className="font-bold">
                {rows.length === 0 ? 0 : (currentPage - 1) * PAGE_SIZE + 1}–
                {Math.min(currentPage * PAGE_SIZE, totalCount)}
              </span>{' '}
              trong <span className="font-bold">{totalCount}</span> kết quả
            </>
          )}
        </p>
        <div className="flex items-center gap-2">
          <PagButton disabled={currentPage <= 1} onClick={() => handlePage(currentPage - 1)}>
            {'‹'}
          </PagButton>
          {buildPageNumbers(currentPage, totalPages).map((p, idx) =>
            p === '...' ? (
              <span key={`ellipsis-${idx}`} className="px-1 text-outline">...</span>
            ) : (
              <PagButton key={p} active={p === currentPage} onClick={() => handlePage(p)}>
                {p}
              </PagButton>
            )
          )}
          <PagButton disabled={currentPage >= totalPages} onClick={() => handlePage(currentPage + 1)}>
            {'›'}
          </PagButton>
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

/** Build compact page number list: 1 ... X-1 X X+1 ... N */
function buildPageNumbers(current, total) {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages = [];
  pages.push(1);
  if (current > 3) pages.push('...');
  for (let p = Math.max(2, current - 1); p <= Math.min(total - 1, current + 1); p++) {
    pages.push(p);
  }
  if (current < total - 2) pages.push('...');
  pages.push(total);
  return pages;
}
