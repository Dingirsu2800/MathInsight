import { Navigate, Route, Routes } from 'react-router-dom';
import StudentDashboard from './pages/student/Dashboard.jsx';
import TestSession from './pages/student/TestSession.jsx';
import QuestionBankListPage from './pages/expert/QuestionBankListPage.jsx';
import QuestionEditorPage from './pages/expert/QuestionEditorPage.jsx';
import ExpertProfilePage from './pages/expert/ExpertProfilePage.jsx';
import TagManagementPage from './pages/expert/TagManagementPage.jsx';
import ReportedQuestionsPage from './pages/expert/ReportedQuestionsPage.jsx';
import LandingPage from './pages/LandingPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import RegisterStudentPage from './pages/RegisterStudentPage.jsx';
import ConfirmEmailPage from './pages/ConfirmEmailPage.jsx';
import ForgotPasswordPage from './pages/ForgotPasswordPage.jsx';
import ResetPasswordPage from './pages/ResetPasswordPage.jsx';
import GoogleSuccessPage from './pages/GoogleSuccessPage.jsx';
import PlaceholderPage from './pages/PlaceholderPage.jsx';
import ProtectedRoute from './routes/ProtectedRoute.jsx';
import AccountManagementPage from './pages/admin/AccountManagementPage.jsx';
import TeacherApplicationsPage from './pages/admin/TeacherApplicationsPage.jsx';
import RolesPermissionsPage from './pages/admin/RolesPermissionsPage.jsx';
import { getAccessToken, getRoleName } from './services/authStorage.js';
import { resolveHomePath } from './utils/roleRoutes.js';

// "/" shows the marketing landing page for visitors, but sends an already
// authenticated user straight to their role home so they skip the marketing page.
function HomeRoute() {
  if (getAccessToken()) {
    const home = resolveHomePath(getRoleName());
    // resolveHomePath falls back to "/" for unknown roles — guard against a redirect loop.
    if (home !== '/') {
      return <Navigate to={home} replace />;
    }
  }
  return <LandingPage />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeRoute />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterStudentPage />} />
      <Route path="/confirm-email" element={<ConfirmEmailPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/auth/google/success" element={<GoogleSuccessPage />} />
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
      {/* Role landing pages (placeholders until their dashboards are built) */}
      <Route element={<ProtectedRoute />}>
        <Route path="/student" element={<PlaceholderPage showLogout title="Không gian học tập" description="Trang tổng quan học sinh đang được phát triển." />} />
        <Route path="/teacher" element={<PlaceholderPage showLogout title="Không gian giáo viên" description="Trang tổng quan giáo viên đang được phát triển." />} />
        <Route path="/admin" element={<PlaceholderPage showLogout title="Quản trị hệ thống" description="Trang quản trị đang được phát triển." />} />
      </Route>
      {/* Expert Routes */}
      <Route element={<ProtectedRoute />}>
        <Route path="/expert/questions" element={<QuestionBankListPage />} />
        <Route path="/expert/questions/reported" element={<ReportedQuestionsPage />} />
        <Route path="/expert/questions/new" element={<QuestionEditorPage />} />
        <Route path="/expert/questions/:id/edit" element={<QuestionEditorPage />} />
        <Route path="/expert/tags" element={<TagManagementPage />} />
        <Route path="/expert/profile" element={<ExpertProfilePage />} />
      </Route>
      {/* Admin Routes */}
      <Route element={<ProtectedRoute />}>
        <Route path="/admin/accounts" element={<AccountManagementPage />} />
        <Route path="/admin/applications" element={<TeacherApplicationsPage />} />
        <Route path="/admin/roles" element={<RolesPermissionsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}

