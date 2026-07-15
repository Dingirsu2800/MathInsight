import { Navigate, Route, Routes } from 'react-router-dom';
import StudentDashboard from './pages/student/Dashboard.jsx';
import TestSession from './pages/student/TestSession.jsx';
import QuestionBankListPage from './pages/expert/QuestionBankListPage.jsx';
import QuestionEditorPage from './pages/expert/QuestionEditorPage.jsx';
import ExpertProfilePage from './pages/expert/ExpertProfilePage.jsx';
import TagManagementPage from './pages/expert/TagManagementPage.jsx';
import ReportedQuestionsPage from './pages/expert/ReportedQuestionsPage.jsx';
import BlueprintListPage from './pages/expert/BlueprintListPage.jsx';
import BlueprintEditorPage from './pages/expert/BlueprintEditorPage.jsx';
import BlueprintDetailPage from './pages/expert/BlueprintDetailPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import ProtectedRoute from './routes/ProtectedRoute.jsx';

// Teacher Pages
import LectureListPage from './pages/teacher/LectureListPage.jsx';
import LectureEditorPage from './pages/teacher/LectureEditorPage.jsx';
import LectureDetailPage from './pages/teacher/LectureDetailPage.jsx';
import MaterialListPage from './pages/teacher/MaterialListPage.jsx';
import ModerationPage from './pages/teacher/ModerationPage.jsx';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPage />} />
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
      {/* Protected Routes */}
      <Route element={<ProtectedRoute />}>
        {/* Expert Routes */}
        <Route path="/expert/questions" element={<QuestionBankListPage />} />
        <Route path="/expert/questions/reported" element={<ReportedQuestionsPage />} />
        <Route path="/expert/questions/new" element={<QuestionEditorPage />} />
        <Route path="/expert/questions/:id/edit" element={<QuestionEditorPage />} />
        <Route path="/expert/tags" element={<TagManagementPage />} />
        <Route path="/expert/profile" element={<ExpertProfilePage />} />
        <Route path="/expert/blueprints" element={<BlueprintListPage />} />
        <Route path="/expert/blueprints/new" element={<BlueprintEditorPage />} />
        <Route path="/expert/blueprints/:blueprintId" element={<BlueprintDetailPage />} />
        <Route path="/expert/blueprints/:blueprintId/edit" element={<BlueprintEditorPage />} />

        {/* Teacher Routes */}
        <Route path="/teacher/lectures" element={<LectureListPage />} />
        <Route path="/teacher/lectures/new" element={<LectureEditorPage />} />
        <Route path="/teacher/lectures/:id/edit" element={<LectureEditorPage />} />
        <Route path="/teacher/lectures/:id" element={<LectureDetailPage />} />
        <Route path="/teacher/materials" element={<MaterialListPage />} />
        <Route path="/teacher/moderation" element={<ModerationPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

