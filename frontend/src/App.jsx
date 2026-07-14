import { Navigate, Route, Routes } from 'react-router-dom';
import StudentDashboard from './pages/student/Dashboard.jsx';
import TestSession from './pages/student/TestSession.jsx';
import TestHistoryPage from './pages/student/TestHistoryPage.jsx';
import CompetencyPage from './pages/student/CompetencyPage.jsx';
import TestResultPage from './pages/student/TestResultPage.jsx';
import QuestionBankListPage from './pages/expert/QuestionBankListPage.jsx';
import QuestionEditorPage from './pages/expert/QuestionEditorPage.jsx';
import ExpertProfilePage from './pages/expert/ExpertProfilePage.jsx';
import TagManagementPage from './pages/expert/TagManagementPage.jsx';
import ReportedQuestionsPage from './pages/expert/ReportedQuestionsPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import ProtectedRoute from './routes/ProtectedRoute.jsx';



export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPage />} />
      {/* Student Routes */}
      <Route path="/student/dashboard" element={<StudentDashboard />} />
      <Route path="/student/history" element={<TestHistoryPage />} />
      <Route path="/student/competency" element={<CompetencyPage />} />
      <Route path="/student/test-result/:sessionId" element={<TestResultPage />} />
      <Route path="/student/test-result" element={<TestResultPage />} />
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
      <Route element={<ProtectedRoute />}>
        <Route path="/expert/questions" element={<QuestionBankListPage />} />
        <Route path="/expert/questions/reported" element={<ReportedQuestionsPage />} />
        <Route path="/expert/questions/new" element={<QuestionEditorPage />} />
        <Route path="/expert/questions/:id/edit" element={<QuestionEditorPage />} />
        <Route path="/expert/tags" element={<TagManagementPage />} />
        <Route path="/expert/profile" element={<ExpertProfilePage />} />
      </Route>
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

