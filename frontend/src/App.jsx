import { Navigate, Route, Routes } from 'react-router-dom';
import StudentDashboard from './pages/student/Dashboard.jsx';
import StudentLectureListPage from './pages/student/StudentLectureListPage.jsx';
import StudentLectureDetailPage from './pages/student/StudentLectureDetailPage.jsx';
import TestSession from './pages/student/TestSession.jsx';
import TestHistoryPage from './pages/student/TestHistoryPage.jsx';
import CompetencyPage from './pages/student/CompetencyPage.jsx';
import TestResultPage from './pages/student/TestResultPage.jsx';
import QuestionBankListPage from './pages/expert/QuestionBankListPage.jsx';
import QuestionEditorPage from './pages/expert/QuestionEditorPage.jsx';
import ExpertProfilePage from './pages/expert/ExpertProfilePage.jsx';
import TagManagementPage from './pages/expert/TagManagementPage.jsx';
import ReportedQuestionsPage from './pages/expert/ReportedQuestionsPage.jsx';
import LandingPage from './pages/LandingPage.jsx';
import BlueprintListPage from './pages/expert/BlueprintListPage.jsx';
import BlueprintEditorPage from './pages/expert/BlueprintEditorPage.jsx';
import BlueprintDetailPage from './pages/expert/BlueprintDetailPage.jsx';
import LoginPage from './pages/LoginPage.jsx';
import RegisterStudentPage from './pages/RegisterStudentPage.jsx';
import RegisterTeacherPage from './pages/RegisterTeacherPage.jsx';
import ConfirmEmailPage from './pages/ConfirmEmailPage.jsx';
import ForgotPasswordPage from './pages/ForgotPasswordPage.jsx';
import ResetPasswordPage from './pages/ResetPasswordPage.jsx';
import GoogleSuccessPage from './pages/GoogleSuccessPage.jsx';
import PlaceholderPage from './pages/PlaceholderPage.jsx';
import ProfilePage from './pages/ProfilePage.jsx';
import ProtectedRoute from './routes/ProtectedRoute.jsx';
import AccountManagementPage from './pages/admin/AccountManagementPage.jsx';
import TeacherApplicationsPage from './pages/admin/TeacherApplicationsPage.jsx';
import RolesPermissionsPage from './pages/admin/RolesPermissionsPage.jsx';
import { getAccessToken, getRoleName } from './services/authStorage.js';
import { resolveHomePath } from './utils/roleRoutes.js';

// Teacher Pages
import LectureListPage from './pages/teacher/LectureListPage.jsx';
import LectureEditorPage from './pages/teacher/LectureEditorPage.jsx';
import LectureDetailPage from './pages/teacher/LectureDetailPage.jsx';
import MaterialListPage from './pages/teacher/MaterialListPage.jsx';
import ModerationPage from './pages/teacher/ModerationPage.jsx';

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
      <Route path="/register/teacher" element={<RegisterTeacherPage />} />
      <Route path="/confirm-email" element={<ConfirmEmailPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/auth/google/success" element={<GoogleSuccessPage />} />
      {/* Student Routes — authenticated only, same wrapper as expert/teacher/admin below. */}
      <Route element={<ProtectedRoute />}>
        <Route path="/student/dashboard" element={<StudentDashboard />} />
        <Route path="/student/history" element={<TestHistoryPage />} />
        <Route path="/student/competency" element={<CompetencyPage />} />
        <Route path="/student/test-result/:sessionId" element={<TestResultPage />} />
        <Route path="/student/test-result" element={<TestResultPage />} />
        <Route path="/student/test/:testId" element={<TestSession />} />
        <Route path="/student/lectures" element={<StudentLectureListPage />} />
        <Route path="/student/lectures/:id" element={<StudentLectureDetailPage />} />
      </Route>
      {/* Role landing pages (placeholders until their dashboards are built) */}
      <Route element={<ProtectedRoute />}>
        {/* UC-04 / UC-05 — role-agnostic, any authenticated user. */}
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/student" element={<PlaceholderPage showLogout title="Không gian học tập" description="Trang tổng quan học sinh đang được phát triển." />} />
        <Route path="/teacher" element={<PlaceholderPage showLogout title="Không gian giáo viên" description="Trang tổng quan giáo viên đang được phát triển." />} />
        <Route path="/admin" element={<PlaceholderPage showLogout title="Quản trị hệ thống" description="Trang quản trị đang được phát triển." />} />
      </Route>
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
      {/* Admin Routes */}
      <Route element={<ProtectedRoute />}>
        <Route path="/admin/accounts" element={<AccountManagementPage />} />
        <Route path="/admin/applications" element={<TeacherApplicationsPage />} />
        <Route path="/admin/roles" element={<RolesPermissionsPage />} />
      </Route>
      {/* Unknown URL — NOT an auth failure. This used to redirect to /login, which made every
          nav link pointing at an unregistered path look exactly like a logged-out session
          (tokens still in localStorage, nothing cleared). Show a not-found page instead so a
          routing gap is visible as a routing gap. */}
      <Route
        path="*"
        element={
          <PlaceholderPage
            showLogout
            title="Không tìm thấy trang"
            description="Đường dẫn này không tồn tại. Vui lòng kiểm tra lại hoặc quay về trang chủ."
          />
        }
      />
    </Routes>
  );
}

