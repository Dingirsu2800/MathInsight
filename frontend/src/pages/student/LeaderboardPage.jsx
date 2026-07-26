import { useEffect, useState } from 'react';
import StudentLayout from '../../components/layout/StudentLayout';
import MaterialIcon from '../../components/ui/MaterialIcon';
import useCurrentUser from '../../hooks/useCurrentUser';
import { getAccountId } from '../../services/authStorage';
import { getLeaderboard } from '../../services/reportApi';

const GRADES = [10, 11, 12];

export default function LeaderboardPage() {
  const { profile } = useCurrentUser();
  const [grade, setGrade] = useState(null);
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const accountId = getAccountId();

  // Default to the student's own grade once the profile loads; fall back to 10.
  useEffect(() => {
    if (grade === null) {
      setGrade(profile?.student?.currentGrade || 10);
    }
  }, [profile, grade]);

  useEffect(() => {
    if (grade === null) return;
    let isMounted = true;
    setLoading(true);

    getLeaderboard(grade)
      .then((data) => {
        if (isMounted) setEntries(data || []);
      })
      .catch(() => {
        if (isMounted) setEntries([]);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [grade]);

  return (
    <StudentLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-on-surface flex items-center gap-2">
            <MaterialIcon name="leaderboard" className="text-primary" />
            Bảng xếp hạng
          </h1>
          <p className="text-on-surface-variant text-sm mt-1">
            Xếp hạng học sinh theo điểm năng lực, cập nhật hàng ngày.
          </p>
        </div>

        <div className="flex gap-2">
          {GRADES.map((g) => (
            <button
              key={g}
              onClick={() => setGrade(g)}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                grade === g
                  ? 'bg-primary text-white'
                  : 'bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
              }`}
            >
              Khối {g}
            </button>
          ))}
        </div>

        <div className="bg-pure-surface border border-whisper-border rounded-2xl overflow-hidden shadow-sm">
          {loading ? (
            <p className="text-sm text-on-surface-variant p-6">Đang tải...</p>
          ) : entries.length === 0 ? (
            <p className="text-sm text-on-surface-variant p-6">Chưa có dữ liệu xếp hạng cho khối này.</p>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-container-low text-on-surface-variant text-xs uppercase">
                <tr>
                  <th className="text-left px-6 py-3 w-16">#</th>
                  <th className="text-left px-6 py-3">Học sinh</th>
                  <th className="text-right px-6 py-3">Điểm năng lực</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((entry) => {
                  const isMe = entry.studentId === accountId;
                  return (
                    <tr
                      key={entry.studentId}
                      className={`border-t border-whisper-border ${isMe ? 'bg-primary/5' : ''}`}
                    >
                      <td className="px-6 py-3 font-semibold text-on-surface">
                        {entry.rank <= 3 ? (
                          <MaterialIcon
                            name="military_tech"
                            className={
                              entry.rank === 1
                                ? 'text-amber-warning'
                                : entry.rank === 2
                                  ? 'text-outline'
                                  : 'text-deep-rose'
                            }
                          />
                        ) : (
                          entry.rank
                        )}
                      </td>
                      <td className="px-6 py-3 text-on-surface">
                        {entry.studentName}
                        {isMe && <span className="ml-2 text-xs text-primary font-medium">(Bạn)</span>}
                      </td>
                      <td className="px-6 py-3 text-right font-semibold text-primary">
                        {entry.point}/10
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </StudentLayout>
  );
}
