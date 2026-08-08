import * as React from "react";
import { Link, useNavigate } from "react-router-dom";
import client from "../../services/questionBankApiClient";
import { mapAuthError, toFieldKey } from "../../services/authErrors";
import { logout } from "../../services/auth";
import { setApplicationStatus } from "../../services/authStorage";
import { resolveHomePath } from "../../utils/roleRoutes";
import { FormatVietnamDateTime } from "../../utils/dateTime";
import CertificateUploader from "../../components/teacher/CertificateUploader";
import {
  CERT_COUNT_ERROR,
  CERT_KEEP_REQUIRED_ERROR,
  CERT_MAX_FILES,
} from "../../utils/certificateFiles";

// Mirrors AuthValidation.PhoneNumberPattern on the backend (VN number, 10 digits from 0).
const PHONE_PATTERN = /^0\d{9}$/;

const LOAD_FALLBACK_ERROR = "Không tải được hồ sơ. Vui lòng thử lại sau.";
const SAVE_FALLBACK_ERROR = "Nộp lại hồ sơ thất bại. Vui lòng thử lại sau.";

const APPLICATION_URL = "/api/v1/teacher/application";

const inputClass =
  "w-full pl-11 pr-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400";

function IsStatus(status, expected) {
  return String(status || "").toLowerCase() === expected;
}

// Vietnam time (Asia/Ho_Chi_Minh), so the teacher and the reviewing Admin see the same clock
// reading for the same instant. See utils/dateTime.js for why toLocaleString alone is not enough.
function FormatDateTime(value) {
  return FormatVietnamDateTime(value);
}

function LabeledInput({ id, label, icon, error, children }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-semibold text-[#1e2a4a]">
        {label}
      </label>
      <div className="relative flex items-center">
        <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">
          {icon}
        </span>
        {children}
      </div>
      {error && <p className="text-xs text-deep-rose font-medium">{error}</p>}
    </div>
  );
}

function StatusBadge({ status }) {
  const isPending = IsStatus(status, "pending");
  const isRejected = IsStatus(status, "rejected");

  const className = isPending
    ? "bg-amber-50 text-amber-700 border-amber-200"
    : isRejected
      ? "bg-deep-rose/5 text-deep-rose border-deep-rose/20"
      : "bg-emerald-success/5 text-emerald-success border-emerald-success/20";

  const label = isPending ? "Đang chờ duyệt" : isRejected ? "Đã bị từ chối" : "Đã được duyệt";

  return (
    <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full border text-xs font-semibold ${className}`}>
      <span className="material-symbols-outlined text-[14px]">
        {isPending ? "hourglass_top" : isRejected ? "cancel" : "check_circle"}
      </span>
      {label}
    </span>
  );
}

export default function TeacherApplicationPage() {
  const navigate = useNavigate();

  const [application, setApplication] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState("");

  const [form, setForm] = React.useState({
    firstName: "",
    lastName: "",
    phoneNumber: "",
    biography: "",
  });
  // URLs already stored on the application that the teacher chose to keep.
  const [keptUrls, setKeptUrls] = React.useState([]);
  // Newly picked files: { id, file, previewUrl }.
  const [newFiles, setNewFiles] = React.useState([]);

  const [errors, setErrors] = React.useState({});
  const [formError, setFormError] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [resubmitted, setResubmitted] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;

    async function LoadApplication() {
      try {
        const response = await client.get(APPLICATION_URL);
        if (cancelled) return;

        const data = response.data || {};
        setApplication(data);
        setForm({
          firstName: data.firstName || "",
          lastName: data.lastName || "",
          phoneNumber: data.phoneNumber || "",
          biography: data.biography || "",
        });
        setKeptUrls(Array.isArray(data.documentsUrls) ? data.documentsUrls : []);
      } catch (err) {
        if (cancelled) return;
        console.error(err);
        // 404 means no application row (an Admin-created teacher) — not an error state to retry.
        setLoadError(
          err?.response?.status === 404
            ? "Tài khoản của bạn không có hồ sơ đăng ký giáo viên."
            : mapAuthError(err, LOAD_FALLBACK_ERROR),
        );
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    LoadApplication();
    return () => {
      cancelled = true;
    };
  }, []);

  const setField = (name) => (e) => {
    const value = e.target.value;
    setForm((prev) => ({ ...prev, [name]: value }));
    setErrors((prev) => (prev[name] ? { ...prev, [name]: undefined } : prev));
  };

  const handleFilesChange = (files, uploaderError) => {
    setNewFiles(files);
    setErrors((prev) => ({ ...prev, certificates: uploaderError }));
  };

  const handleRemoveExisting = (url) => {
    setKeptUrls((prev) => prev.filter((item) => item !== url));
    setErrors((prev) => (prev.certificates ? { ...prev, certificates: undefined } : prev));
  };

  function Validate() {
    const next = {};

    if (!form.lastName.trim()) next.lastName = "Vui lòng nhập họ.";
    if (!form.firstName.trim()) next.firstName = "Vui lòng nhập tên.";

    if (!form.phoneNumber.trim()) {
      next.phoneNumber = "Vui lòng nhập số điện thoại.";
    } else if (!PHONE_PATTERN.test(form.phoneNumber.trim())) {
      next.phoneNumber = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.";
    }

    const totalCertificates = keptUrls.length + newFiles.length;
    if (totalCertificates === 0) {
      next.certificates = CERT_KEEP_REQUIRED_ERROR;
    } else if (totalCertificates > CERT_MAX_FILES) {
      next.certificates = CERT_COUNT_ERROR;
    }

    return next;
  }

  const handleResubmit = async (e) => {
    e.preventDefault();
    setFormError("");

    const validationErrors = Validate();
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      return;
    }
    setErrors({});
    setSaving(true);

    // Field names match UpdateMyApplicationRequest. Email is intentionally absent: it is the
    // account's confirmed identity and cannot be changed here.
    const formData = new FormData();
    formData.append("FirstName", form.firstName.trim());
    formData.append("LastName", form.lastName.trim());
    formData.append("PhoneNumber", form.phoneNumber.trim());
    if (form.biography.trim()) {
      formData.append("Biography", form.biography.trim());
    }
    // Repeated entries bind to the DTO's List<string> / List<IFormFile>.
    keptUrls.forEach((url) => formData.append("KeptDocumentsUrls", url));
    newFiles.forEach((item) => formData.append("Certificates", item.file));

    try {
      // Two calls by design: PUT saves the edits, POST moves the application back into review.
      // A failed resubmit therefore never loses the teacher's changes.
      await client.put(`${APPLICATION_URL}/${application.applicationId}`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });

      const response = await client.post(`${APPLICATION_URL}/${application.applicationId}/resubmit`);

      setApplication(response.data || null);
      setNewFiles([]);
      setKeptUrls(Array.isArray(response.data?.documentsUrls) ? response.data.documentsUrls : []);
      // Keep the stored status in step so a reload does not show the rejected screen again.
      setApplicationStatus("pending");
      setResubmitted(true);
    } catch (err) {
      console.error(err);
      const status = err?.response?.status;
      const backendErrors = err?.response?.data?.errors;

      if (status === 400 && backendErrors) {
        const mapped = {};
        Object.keys(backendErrors).forEach((key) => {
          const messages = backendErrors[key];
          mapped[toFieldKey(key)] = Array.isArray(messages) ? messages[0] : String(messages);
        });
        setErrors(mapped);
        setFormError("Vui lòng kiểm tra lại các thông tin được đánh dấu.");
      } else if (status === 409) {
        // Either the phone number is taken or the application is no longer editable.
        setFormError(
          err?.response?.data?.code === "AUTH_PHONE_ALREADY_USED"
            ? "Số điện thoại này đã được sử dụng."
            : "Hồ sơ không còn ở trạng thái có thể chỉnh sửa. Vui lòng tải lại trang.",
        );
      } else if (status === 403) {
        setFormError("Bạn không có quyền chỉnh sửa hồ sơ này.");
      } else {
        setFormError(mapAuthError(err, SAVE_FALLBACK_ERROR));
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
        <div className="flex flex-col items-center gap-3 text-center">
          <div className="w-8 h-8 border-[3px] border-[#2f5fa8] border-t-transparent rounded-full animate-spin"></div>
          <p className="text-sm font-semibold text-[#1e2a4a]">Đang tải hồ sơ...</p>
        </div>
      </main>
    );
  }

  if (loadError || !application) {
    return (
      <main className="min-h-screen flex items-center justify-center bg-[#eef2f7] p-4">
        <section className="w-full max-w-md bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 text-center space-y-4">
          <div className="w-14 h-14 rounded-full bg-deep-rose/10 flex items-center justify-center mx-auto">
            <span className="material-symbols-outlined text-deep-rose text-[32px]">error</span>
          </div>
          <h1 className="text-xl font-bold text-[#1e2a4a]">Không tải được hồ sơ</h1>
          <p className="text-sm text-slate-500 leading-relaxed">{loadError || LOAD_FALLBACK_ERROR}</p>
          <Link
            to="/login"
            className="inline-block w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all"
          >
            Về trang đăng nhập
          </Link>
        </section>
      </main>
    );
  }

  const isRejected = IsStatus(application.status, "rejected") && application.canEdit && !resubmitted;
  const isPending = IsStatus(application.status, "pending") || resubmitted;
  const isApproved = IsStatus(application.status, "approved");

  return (
    <main className="min-h-screen bg-[#eef2f7] p-4 py-8">
      <section className="w-full max-w-2xl mx-auto bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
        {/* Header */}
        <div className="flex flex-col items-center text-center space-y-3">
          <div className="w-12 h-12 rounded-xl bg-[#2f5fa8] flex items-center justify-center shadow-sm">
            <span className="material-symbols-outlined text-white text-[26px]">assignment_ind</span>
          </div>
          <div className="space-y-2">
            <h1 className="text-2xl font-bold text-[#1e2a4a]">Hồ sơ đăng ký giáo viên</h1>
            <StatusBadge status={resubmitted ? "pending" : application.status} />
          </div>
        </div>

        {/* Pending / just-resubmitted: read-only */}
        {isPending && (
          <div className="p-4 text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-xl leading-relaxed flex items-start gap-2.5">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">hourglass_top</span>
            <span>
              {resubmitted
                ? "Hồ sơ đã được nộp lại và đang chờ quản trị viên duyệt. Bạn sẽ nhận được thông báo khi có kết quả."
                : "Hồ sơ của bạn đang chờ quản trị viên duyệt. Trong thời gian này bạn chưa thể sử dụng các tính năng giảng dạy."}
            </span>
          </div>
        )}

        {/* Approved: nothing to do here */}
        {isApproved && (
          <div className="p-4 text-sm text-emerald-success bg-emerald-success/5 border border-emerald-success/20 rounded-xl leading-relaxed flex items-start gap-2.5">
            <span className="material-symbols-outlined text-[18px] shrink-0 mt-0.5">check_circle</span>
            <span>Hồ sơ đã được duyệt. Bạn có thể sử dụng đầy đủ các tính năng giảng dạy.</span>
          </div>
        )}

        {/* Rejected: the reason, prominently */}
        {isRejected && (
          <div className="p-4 bg-deep-rose/5 border border-deep-rose/20 rounded-xl space-y-1.5">
            <div className="flex items-center gap-2 text-deep-rose font-bold text-sm">
              <span className="material-symbols-outlined text-[18px]">cancel</span>
              Lý do từ chối
            </div>
            <p className="text-sm text-[#1e2a4a] leading-relaxed whitespace-pre-line">
              {application.reviewComments || "Quản trị viên không để lại lý do cụ thể."}
            </p>
            <p className="text-xs text-slate-500 pt-1">
              Vui lòng cập nhật thông tin bên dưới và nộp lại hồ sơ.
            </p>
          </div>
        )}

        {formError && (
          <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
            <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
            <span>{formError}</span>
          </div>
        )}

        {/* Submission metadata */}
        <dl className="grid grid-cols-2 gap-4 p-4 bg-slate-50 rounded-xl text-sm">
          <div>
            <dt className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Ngày nộp</dt>
            <dd className="text-[#1e2a4a] font-medium mt-0.5">{FormatDateTime(application.appliedTime)}</dd>
          </div>
          <div>
            <dt className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Ngày duyệt</dt>
            <dd className="text-[#1e2a4a] font-medium mt-0.5">{FormatDateTime(application.reviewedTime)}</dd>
          </div>
        </dl>

        <form onSubmit={handleResubmit} className="space-y-4" noValidate>
          {/* Email — never editable (confirmed identity, DD-01) */}
          <div className="space-y-1.5">
            <label className="block text-sm font-semibold text-[#1e2a4a]">Email</label>
            <div className="relative flex items-center">
              <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">mail</span>
              <input
                type="email"
                value={application.email || ""}
                className={`${inputClass} bg-slate-50 text-slate-500 cursor-not-allowed`}
                disabled
                readOnly
              />
            </div>
            <p className="text-xs text-slate-400">Email đã xác nhận và không thể thay đổi.</p>
          </div>

          {/* Họ / Tên */}
          <div className="grid grid-cols-2 gap-3">
            <LabeledInput id="lastName" label="Họ" icon="badge" error={errors.lastName}>
              <input
                id="lastName"
                type="text"
                value={form.lastName}
                onChange={setField("lastName")}
                className={inputClass}
                placeholder="Nguyễn"
                autoComplete="family-name"
                disabled={!isRejected || saving}
              />
            </LabeledInput>
            <LabeledInput id="firstName" label="Tên" icon="badge" error={errors.firstName}>
              <input
                id="firstName"
                type="text"
                value={form.firstName}
                onChange={setField("firstName")}
                className={inputClass}
                placeholder="An"
                autoComplete="given-name"
                disabled={!isRejected || saving}
              />
            </LabeledInput>
          </div>

          {/* Số điện thoại */}
          <LabeledInput id="phoneNumber" label="Số điện thoại" icon="call" error={errors.phoneNumber}>
            <input
              id="phoneNumber"
              type="tel"
              value={form.phoneNumber}
              onChange={setField("phoneNumber")}
              className={inputClass}
              placeholder="0912345678"
              autoComplete="tel"
              maxLength={20}
              disabled={!isRejected || saving}
            />
          </LabeledInput>

          {/* Giới thiệu bản thân */}
          <div className="space-y-1.5">
            <label htmlFor="biography" className="block text-sm font-semibold text-[#1e2a4a]">
              Giới thiệu bản thân
            </label>
            <textarea
              id="biography"
              value={form.biography}
              onChange={setField("biography")}
              rows={3}
              className="w-full px-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all placeholder:text-slate-400 resize-y disabled:bg-slate-50 disabled:text-slate-500"
              placeholder="Kinh nghiệm giảng dạy, trường công tác, chuyên môn..."
              disabled={!isRejected || saving}
            />
            {errors.biography && <p className="text-xs text-deep-rose font-medium">{errors.biography}</p>}
          </div>

          {/* Chứng chỉ giảng dạy */}
          <CertificateUploader
            existingUrls={keptUrls}
            onRemoveExisting={isRejected ? handleRemoveExisting : undefined}
            files={newFiles}
            onFilesChange={handleFilesChange}
            error={errors.certificates}
            disabled={!isRejected || saving}
            inputId="applicationCertificates"
          />

          {isRejected && (
            <button
              type="submit"
              disabled={saving}
              className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed mt-2"
            >
              {saving ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
                  Đang nộp lại...
                </>
              ) : (
                "Nộp lại"
              )}
            </button>
          )}
        </form>

        {isApproved && (
          <button
            type="button"
            onClick={() => navigate(resolveHomePath("teacher", "approved"), { replace: true })}
            className="w-full bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all cursor-pointer"
          >
            Vào không gian giáo viên
          </button>
        )}

        {/* Signs out through the shared logout() (BR-10: revokes the session server-side, clears
            local storage, then hard-redirects to /login) rather than just navigating away, which
            would leave a valid token behind on a shared machine. */}
        <button
          type="button"
          onClick={() => logout()}
          disabled={saving}
          className="w-full flex items-center justify-center gap-2 py-3 rounded-xl border border-slate-200 bg-white text-sm font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
        >
          <span className="material-symbols-outlined text-[18px]">logout</span>
          Quay lại đăng nhập
        </button>

        <p className="text-center text-xs text-slate-400">
          Cần hỗ trợ? Liên hệ quản trị viên để được hướng dẫn thêm.
        </p>
      </section>
    </main>
  );
}
