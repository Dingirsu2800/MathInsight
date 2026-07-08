import { Navigate, Route, Routes } from 'react-router-dom';
import StudentDashboard from './pages/student/Dashboard.jsx';
import TestSession from './pages/student/TestSession.jsx';
import QuestionBankListPage from './pages/expert/QuestionBankListPage.jsx';
import QuestionEditorPage from './pages/expert/QuestionEditorPage.jsx';

function LoginPlaceholder() {
  return (
    <main className="page page-centered">
      <section className="card auth-card">
        <p className="eyebrow">MathInsight MVP</p>
        <h1>Đăng nhập</h1>
        <p className="muted">
          Màn hình này là placeholder để team nối API Identity sau. Hiện tại frontend đã có
          router, cấu hình API base URL và layout tối thiểu để bắt đầu chia màn.
        </p>
        <div className="button-row flex-col gap-2 mt-4">
          <div className="flex gap-2">
            <a className="button" href="/student/dashboard">Vào dashboard học sinh mẫu</a>
            <a className="button button-secondary" href="/student/test">Vào bài test mẫu</a>
          </div>
          <a className="button bg-primary border-none mt-2 w-full text-center py-2" href="/expert/questions">
            Vào cổng Quản trị Chuyên gia (Expert Portal)
          </a>
        </div>
      </section>
    </main>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPlaceholder />} />
      <Route path="/student/dashboard" element={<StudentDashboard />} />
      <Route
        path="/student/test"
        element={
          <TestSession
            sessionId="local-session"
            testId="3fa85f64-5717-4562-b3fc-2c963f66afa6"
          />
        }
      />
      {/* Expert Routes */}
      <Route path="/expert/questions" element={<QuestionBankListPage />} />
      <Route path="/expert/questions/new" element={<QuestionEditorPage />} />
      <Route path="/expert/questions/:id/edit" element={<QuestionEditorPage />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

