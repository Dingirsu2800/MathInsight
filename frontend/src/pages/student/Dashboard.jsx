import { useEffect, useState } from 'react';
import api from '../../services/api';
import StudentLayout from './StudentLayout';

const fallbackCompetency = {
  point: 0,
  currentStreak: 0,
  weakTags: ['Chưa có dữ liệu'],
};

export default function StudentDashboard() {
  const [competency, setCompetency] = useState(fallbackCompetency);
  const [recommendations, setRecommendations] = useState([]);
  const [isApiAvailable, setIsApiAvailable] = useState(true);

  useEffect(() => {
    api.get('/reports/competency-summary')
      .then((res) => setCompetency(res.data))
      .catch(() => setIsApiAvailable(false));

    api.get('/recommender/practice-suggestions')
      .then((res) => setRecommendations(res.data))
      .catch(() => setRecommendations([]));
  }, []);

  return (
    <StudentLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        <div>
          <h1 className="text-[32px] font-semibold text-on-surface">Bảng điều khiển học tập</h1>
          <p className="text-on-surface-variant mt-2">Tổng quan về tiến trình học tập của bạn.</p>
        </div>

        {!isApiAvailable && (
          <div className="bg-amber-50 text-amber-800 p-4 rounded-lg border border-amber-200">
            Backend chưa trả dữ liệu cho màn này, nên frontend đang hiển thị dữ liệu mẫu.
          </div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="bg-pure-surface border border-outline-variant rounded-xl p-6 shadow-sm">
            <h2 className="text-[20px] font-semibold text-on-surface mb-4">Năng lực hiện tại</h2>
            <div className="space-y-3">
              <p className="flex justify-between border-b border-whisper-border pb-2">
                <span className="text-on-surface-variant">Điểm năng lực</span>
                <span className="font-semibold text-primary">{competency.point}</span>
              </p>
              <p className="flex justify-between border-b border-whisper-border pb-2">
                <span className="text-on-surface-variant">Streak học tập</span>
                <span className="font-semibold text-[#f59e0b]">{competency.currentStreak} ngày</span>
              </p>
              <div>
                <span className="text-on-surface-variant block mb-2">Tag kiến thức yếu</span>
                <div className="flex flex-wrap gap-2">
                  {competency.weakTags?.map((tag, idx) => (
                    <span key={idx} className="px-2 py-1 bg-error-container text-on-error-container text-[12px] font-medium rounded-full">
                      {tag}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          </div>

          <div className="bg-pure-surface border border-outline-variant rounded-xl p-6 shadow-sm">
            <h2 className="text-[20px] font-semibold text-on-surface mb-4">Đề xuất học tập</h2>
            {recommendations.length === 0 ? (
              <p className="text-on-surface-variant text-[14px]">Chưa có đề xuất từ hệ thống recommender.</p>
            ) : (
              <ul className="space-y-3">
                {recommendations.map((item) => (
                  <li key={item.id ?? item.title} className="flex gap-3 items-start">
                    <span className="material-symbols-outlined text-primary mt-0.5">lightbulb</span>
                    <div>
                      <p className="font-medium text-on-surface">{item.title}</p>
                      <p className="text-[13px] text-on-surface-variant">Cải thiện: {item.targetWeakTag}</p>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </StudentLayout>
  );
}
