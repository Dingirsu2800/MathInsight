import { useEffect, useState } from 'react';
import api from '../../services/api';

export default function TestSession({ sessionId, testId }) {
  const [answers, setAnswers] = useState({});
  const [tabSwitches, setTabSwitches] = useState(0);

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (!document.hidden) {
        return;
      }

      setTabSwitches((previousCount) => {
        const updatedCount = previousCount + 1;

        api.post(`/testing/sessions/${sessionId}/incidents`, {
          incidentType: 'TabSwitch',
          details: `Student switched tabs. Total incidents: ${updatedCount}`,
        }).catch((error) => {
          console.error('Error logging incident', error);
        });

        alert(
          `Cảnh báo an toàn thi cử: bạn vừa rời tab làm bài. Đây là lần vi phạm thứ ${updatedCount}.`
        );

        return updatedCount;
      });
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [sessionId]);

  const handleSelectAnswer = (questionId, optionValue) => {
    setAnswers((currentAnswers) => ({
      ...currentAnswers,
      [questionId]: optionValue,
    }));
  };

  const handleSubmit = async (format) => {
    try {
      const response = await api.post('/testing/submit', {
        studentId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        testId,
        testFormat: format,
        answers,
      });

      alert(`Bài làm đã được nộp. Trạng thái server: ${response.data.status}`);
    } catch {
      alert('Nộp bài thất bại. Kiểm tra backend API rồi thử lại.');
    }
  };

  return (
    <main className="page">
      <p className="eyebrow">Testing</p>
      <h1>Phiên làm bài MathInsight</h1>
      <p className="danger">Số lần rời tab: {tabSwitches} / 10</p>

      <section className="card panel">
        <h2>Câu hỏi mẫu</h2>
        <p>Tìm họ nghiệm của phương trình sin(x) = sin(α).</p>
        <div className="button-row">
          <button type="button" onClick={() => handleSelectAnswer('q1', 'a')}>
            A. x = α + k2π hoặc x = π - α + k2π
          </button>
          <button type="button" onClick={() => handleSelectAnswer('q1', 'b')}>
            B. x = α + kπ hoặc x = -α + kπ
          </button>
        </div>
      </section>

      <div className="button-row" style={{ marginTop: 16 }}>
        <button type="button" onClick={() => handleSubmit('Practice')}>
          Nộp bài luyện tập
        </button>
        <button type="button" className="button-secondary" onClick={() => handleSubmit('Exam')}>
          Nộp bài thi
        </button>
      </div>
    </main>
  );
}
