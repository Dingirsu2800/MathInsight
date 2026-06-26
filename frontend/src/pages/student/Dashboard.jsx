import { useEffect, useState } from 'react';
import api from '../../services/api';

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
    <main className="page">
      <p className="eyebrow">Student</p>
      <h1>Bảng điều khiển học tập</h1>
      {!isApiAvailable && (
        <p className="muted">
          Backend chưa trả dữ liệu cho màn này, nên frontend đang hiển thị dữ liệu mẫu.
        </p>
      )}

      <section className="dashboard-grid">
        <article className="card panel">
          <h2>Năng lực hiện tại</h2>
          <p>Điểm năng lực: {competency.point}</p>
          <p>Streak học tập: {competency.currentStreak} ngày</p>
          <p>Tag kiến thức yếu: {competency.weakTags?.join(', ')}</p>
        </article>

        <article className="card panel">
          <h2>Đề xuất học tập</h2>
          {recommendations.length === 0 ? (
            <p className="muted">Chưa có đề xuất từ hệ thống recommender.</p>
          ) : (
            <ul>
              {recommendations.map((item) => (
                <li key={item.id ?? item.title}>
                  {item.title} - cải thiện {item.targetWeakTag}
                </li>
              ))}
            </ul>
          )}
        </article>
      </section>
    </main>
  );
}
