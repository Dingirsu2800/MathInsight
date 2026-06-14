import React, { useEffect, useState } from 'react';
import api from '../../services/api';

export default function TestSession({ sessionId, testId }) {
  const [answers, setAnswers] = useState({});
  const [tabSwitches, setTabSwitches] = useState(0);

  // Exam Security: detect tab switching to warn the student (SC-01 requirement)
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.hidden) {
        setTabSwitches((prev) => {
          const updated = prev + 1;
          
          // Log tab switch incident to backend via API Ingress
          api.post(`/testing/sessions/${sessionId}/incidents`, {
            incidentType: 'TabSwitch',
            details: `Student switched tabs. Total incidents: ${updated}`
          }).catch(err => console.error('Error logging incident', err));

          alert(`CẢNH BÁO AN TOÀN THI CỬ: Bạn vừa chuyển tab màn hình! Đây là lần vi phạm thứ ${updated}. Sự việc đã được lưu trữ để giám khảo hậu kiểm.`);
          return updated;
        });
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [sessionId]);

  const handleSelectAnswer = (questionId, optionValue) => {
    setAnswers(prev => ({
      ...prev,
      [questionId]: optionValue
    }));
  };

  const handleSubmit = async (format) => {
    try {
      const response = await api.post('/testing/submit', {
        studentId: "3fa85f64-5717-4562-b3fc-2c963f66afa6", // Mock user id
        testId: testId,
        testFormat: format, // "Practice" or "Exam"
        answers: answers
      });
      alert(`Bài làm đã được nộp thành công! Phản hồi từ Server: ${response.data.status}`);
    } catch (error) {
      alert('Nộp bài thi thất bại! Vui lòng thử lại.');
    }
  };

  return (
    <div style={{ padding: '20px', color: '#f8fafc', background: '#0b0f19', minHeight: '100vh' }}>
      <h2>Trình làm bài thi trực tuyến MathInsight</h2>
      <div style={{ color: '#ef4444' }}>Số lần rời tab làm bài: {tabSwitches} / 10</div>
      
      {/* Sample Question UI */}
      <div style={{ border: '1px solid #1e293b', padding: '15px', borderRadius: '8px', margin: '20px 0' }}>
        <p>Câu hỏi 1: Tìm họ nghiệm của phương trình sin(x) = sin(alpha)?</p>
        <button onClick={() => handleSelectAnswer('q1', 'a')}>A. x = alpha + k2pi hoặc x = pi - alpha + k2pi</button><br/>
        <button onClick={() => handleSelectAnswer('q1', 'b')}>B. x = alpha + kpi hoặc x = -alpha + kpi</button>
      </div>

      <button onClick={() => handleSubmit('Practice')} style={{ marginRight: '10px' }}>Nộp bài luyện tập (Chấm ngay)</button>
      <button onClick={() => handleSubmit('Exam')}>Nộp bài thi học kỳ (Xếp hàng chấm ngầm)</button>
    </div>
  );
}