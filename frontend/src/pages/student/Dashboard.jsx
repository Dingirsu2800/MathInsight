import React, { useEffect, useState } from 'react';
import api from '../../services/api';

export default function StudentDashboard() {
  const [competency, setCompetency] = useState(null);
  const [recommendations, setRecommendations] = useState([]);

  useEffect(() => {
    // Fetch individual analytics & competency report (FT-07)
    api.get('/reports/competency-summary')
      .then(res => setCompetency(res.data))
      .catch(err => console.error(err));

    // Fetch adaptive practice recommendations (FT-06)
    api.get('/recommender/practice-suggestions')
      .then(res => setRecommendations(res.data))
      .catch(err => console.error(err));
  }, []);

  return (
    <div style={{ padding: '30px', color: '#cbd5e1', background: '#080c14', minHeight: '100vh' }}>
      <h1>Bảng điều khiển học tập MathInsight</h1>
      {competency && (
        <div style={{ background: '#1e1b4b', padding: '20px', borderRadius: '8px', margin: '20px 0' }}>
          <h3>Điểm năng lực: {competency.point} điểm</h3>
          <p>Chuỗi streak học tập: 🔥 {competency.currentStreak} ngày liên tục</p>
          <div>Các nhãn kiến thức yếu: {competency.weakTags.join(', ')}</div>
        </div>
      )}

      <h3>Đề xuất học tập cá nhân hóa dành cho bạn</h3>
      <ul>
        {recommendations.map((item, index) => (
          <li key={index}>Bài giảng khuyên học: {item.title} (Hỗ trợ cải thiện phần {item.targetWeakTag})</li>
        ))}
      </ul>
    </div>
  );
}