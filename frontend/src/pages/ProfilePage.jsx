import * as React from "react";
import { Link } from "react-router-dom";
import client from "../services/questionBankApiClient";
import { logout } from "../services/auth";
import {
  mapAuthError,
  mapValidationErrors,
  PASSWORD_POLICY_HINT,
} from "../services/authErrors";
import { resolveHomePath } from "../utils/roleRoutes";
import { getRoleName, setAuthSession } from "../services/authStorage";

const PROFILE_URL = "/api/v1/accounts/profile";
const CHANGE_PASSWORD_URL = "/api/v1/accounts/change-password";

// BR-08, mirroring AuthValidation.PasswordPattern on the backend so most policy 400s are
// caught client-side. Same regex as ResetPasswordPage.
const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,128}$/;

const PASSWORD_ERROR = "Đổi mật khẩu thất bại. Vui lòng thử lại.";
const PASSWORD_SUCCESS = "Đổi mật khẩu thành công.";

// Every user-facing string on this page is Vietnamese. Backend messages are developer-facing
// English (BR-11) and are never rendered — they are mapped by code/field in authErrors.js.
const LOAD_ERROR = "Không tải được hồ sơ. Vui lòng thử lại.";
const SAVE_ERROR = "Có lỗi xảy ra, vui lòng thử lại.";
const SAVE_SUCCESS = "Đã lưu thay đổi.";
const CHECK_FIELDS_ERROR = "Vui lòng kiểm tra lại các thông tin được đánh dấu.";
const EMPTY_VALUE = "Chưa cập nhật";

// Backend value ← → Vietnamese label. The DB stores the English value (Student.Gender).
const GENDER_OPTIONS = [
  { value: "Male", label: "Nam" },
  { value: "Female", label: "Nữ" },
];

const ROLE_LABELS = {
  student: "Học sinh",
  teacher: "Giáo viên",
  expert: "Chuyên gia",
  admin: "Quản trị viên",
};

const EMPTY_FORM = {
  firstName: "",
  lastName: "",
  phoneNumber: "",
  dateOfBirth: "",
  gender: "",
  school: "",
  currentGrade: "",
  biography: "",
  specialty: "",
};

// Inputs only ever hold strings; the API sends nulls. Normalizing to "" on load keeps React
// inputs controlled and lets the diff compare like with like. A null dateOfBirth becomes ""
// so <input type="date"> renders its native empty state — never today's date.
function toFormState(profile) {
  return {
    firstName: profile.firstName ?? "",
    lastName: profile.lastName ?? "",
    phoneNumber: profile.phoneNumber ?? "",
    // DateOnly serializes as "yyyy-MM-dd", which is what <input type="date"> expects.
    dateOfBirth: profile.dateOfBirth ?? "",
    gender: profile.student?.gender ?? "",
    school: profile.student?.school ?? "",
    currentGrade:
      profile.student?.currentGrade == null ? "" : String(profile.student.currentGrade),
    biography: profile.teacher?.biography ?? "",
    specialty: profile.expert?.specialty ?? "",
  };
}

// "yyyy-MM-dd" → "dd/MM/yyyy" for the read-only view. Parsed by hand rather than via Date so
// no timezone shift can move the date by a day.
function formatDate(isoDate) {
  if (!isoDate) return "";
  const [year, month, day] = isoDate.split("-");
  if (!year || !month || !day) return isoDate;
  return `${day}/${month}/${year}`;
}

function genderLabel(value) {
  return GENDER_OPTIONS.find((option) => option.value === value)?.label || "";
}

// PUT is a partial update: only changed fields travel, and the backend treats a missing field
// as "keep the stored value".
//
// Because null means "keep", an optional field the user blanks cannot be cleared through this
// endpoint. Text fields send "" (stored as an empty string, which reads as cleared).
// dateOfBirth and currentGrade have no valid empty representation, so a blanked value is
// omitted rather than sent as an invalid date/number that would 400.
function buildDiff(form, originals, visibleFields) {
  const diff = {};

  visibleFields.forEach((field) => {
    const current = typeof form[field] === "string" ? form[field].trim() : form[field];
    if (current === originals[field]) return;

    if (field === "currentGrade") {
      if (current === "") return; // cannot clear a grade through a partial update
      diff.currentGrade = Number(current);
      return;
    }

    if (field === "dateOfBirth") {
      if (current === "") return; // only send a date the user actually picked
      diff.dateOfBirth = current;
      return;
    }

    diff[field] = current;
  });

  // The two names travel together: if either changed, send both. Keeps the pair internally
  // consistent and guarantees a non-empty firstName reaches the server whenever names move.
  if ("firstName" in diff || "lastName" in diff) {
    diff.firstName = form.firstName.trim();
    diff.lastName = form.lastName.trim();
  }

  return diff;
}

const inputClass =
  "w-full px-4 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl " +
  "focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all " +
  "placeholder:text-slate-400 disabled:bg-slate-50 disabled:text-slate-400";

function FieldError({ message }) {
  if (!message) return null;
  return (
    <p className="text-xs font-semibold text-deep-rose flex items-start gap-1">
      <span className="material-symbols-outlined text-[14px] shrink-0 mt-px">error</span>
      <span>{message}</span>
    </p>
  );
}

// One row of the form. Renders plain text in view mode and an input in edit mode, so the two
// modes can never drift apart in labelling or ordering.
function Field({ id, label, error, editing, displayValue, children }) {
  return (
    <div className="space-y-1.5">
      <label
        htmlFor={editing ? id : undefined}
        className="block text-sm font-semibold text-[#1e2a4a]"
      >
        {label}
      </label>
      {editing ? (
        children
      ) : (
        <p
          className={`px-4 py-3 text-sm rounded-xl border ${
            displayValue
              ? "text-[#1e2a4a] bg-white border-slate-200"
              : "text-slate-400 italic bg-slate-50 border-slate-200"
          }`}
        >
          {displayValue || EMPTY_VALUE}
        </p>
      )}
      {editing && <FieldError message={error} />}
    </div>
  );
}

function ReadOnlyRow({ label, value }) {
  return (
    <div className="space-y-1.5">
      <p className="text-sm font-semibold text-[#1e2a4a]">{label}</p>
      <p className="px-4 py-3 text-sm text-slate-500 bg-slate-50 border border-slate-200 rounded-xl break-all">
        {value || EMPTY_VALUE}
      </p>
    </div>
  );
}

function Badge({ children, tone = "blue" }) {
  const tones = {
    blue: "bg-[#2f5fa8]/10 border-[#2f5fa8]/20 text-[#2f5fa8]",
    green: "bg-emerald-success/10 border-emerald-success/20 text-emerald-success",
    slate: "bg-slate-100 border-slate-200 text-slate-500",
  };
  return (
    <span
      className={`inline-flex items-center gap-1 font-bold text-[10px] uppercase tracking-wider px-2.5 py-0.5 rounded-full border ${tones[tone]}`}
    >
      {children}
    </span>
  );
}

const passwordInputClass =
  "w-full pl-11 pr-11 py-3 text-sm text-[#1e2a4a] bg-white border border-slate-200 rounded-xl " +
  "focus:ring-2 focus:ring-[#2f5fa8]/20 focus:border-[#2f5fa8] outline-none transition-all " +
  "placeholder:text-slate-400 disabled:bg-slate-50 disabled:text-slate-400";

// Password input with the lock affordance and an eye toggle, matching ResetPasswordPage.
// tabIndex={-1} on the toggle keeps Tab moving between the fields, not into the button.
function PasswordField({ id, label, value, onChange, error, hint, disabled, autoComplete }) {
  const [visible, setVisible] = React.useState(false);

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-semibold text-[#1e2a4a]">
        {label}
      </label>
      <div className="relative flex items-center">
        <span className="material-symbols-outlined absolute left-3.5 text-slate-400 text-[20px]">
          lock
        </span>
        <input
          id={id}
          type={visible ? "text" : "password"}
          value={value}
          onChange={onChange}
          className={passwordInputClass}
          placeholder="••••••••"
          autoComplete={autoComplete}
          disabled={disabled}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          className="absolute right-3 text-slate-400 hover:text-[#2f5fa8] transition-colors cursor-pointer"
          aria-label={visible ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          tabIndex={-1}
        >
          <span className="material-symbols-outlined text-[20px]">
            {visible ? "visibility_off" : "visibility"}
          </span>
        </button>
      </div>
      {error ? (
        <p className="text-xs text-deep-rose font-medium">{error}</p>
      ) : (
        hint && <p className="text-xs text-slate-400">{hint}</p>
      )}
    </div>
  );
}

/**
 * UC-03. Collapsed to a single button until opened, mirroring the view-then-edit pattern above.
 *
 * BR-15: a successful change revokes EVERY session for this account server-side, so the tokens
 * this tab sent are dead by the time the 200 arrives. The response carries a freshly issued
 * replacement pair — storing it is what keeps the user signed in here while every other device
 * stays logged out. Skipping that store would leave this tab holding revoked tokens and it would
 * be bounced to /login on its next request.
 */
function ChangePasswordSection() {
  const [open, setOpen] = React.useState(false);
  const [form, setForm] = React.useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });
  const [errors, setErrors] = React.useState({});
  const [formError, setFormError] = React.useState("");
  const [success, setSuccess] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const updateField = (field) => (e) => {
    const { value } = e.target;
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => (prev[field] ? { ...prev, [field]: undefined } : prev));
  };

  const reset = () => {
    setForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
    setErrors({});
    setFormError("");
  };

  const close = () => {
    setOpen(false);
    reset();
  };

  const validate = () => {
    const next = {};

    if (!form.currentPassword) {
      next.currentPassword = "Vui lòng nhập mật khẩu hiện tại.";
    }

    if (!form.newPassword) {
      next.newPassword = "Vui lòng nhập mật khẩu mới.";
    } else if (!PASSWORD_PATTERN.test(form.newPassword)) {
      next.newPassword = PASSWORD_POLICY_HINT;
    }

    // Client-side only — confirmPassword is never sent to the backend.
    if (!form.confirmPassword) {
      next.confirmPassword = "Vui lòng xác nhận mật khẩu mới.";
    } else if (form.confirmPassword !== form.newPassword) {
      next.confirmPassword = "Mật khẩu xác nhận không khớp.";
    }

    return next;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return;

    setFormError("");
    const localErrors = validate();
    if (Object.keys(localErrors).length > 0) {
      setErrors(localErrors);
      return;
    }

    setSaving(true);
    setErrors({});

    try {
      const response = await client.post(CHANGE_PASSWORD_URL, {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
      });

      // Swap in the new session BEFORE anything else can fire a request — the tokens we
      // authenticated this call with are already revoked. Same keys as login.
      const data = response.data || {};
      setAuthSession({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        roleName: data.roleName,
        accountId: data.accountId,
      });

      // Clear the typed passwords and collapse the form; the user stays on this page.
      setForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
      setErrors({});
      setOpen(false);
      setSuccess(PASSWORD_SUCCESS);
    } catch (err) {
      console.error(err);
      // A 400 ValidationProblemDetails (BR-08 policy on NewPassword) maps to the field;
      // AUTH_INVALID_CURRENT_PASSWORD / AUTH_SAME_PASSWORD / AUTH_NO_PASSWORD_SET arrive as
      // { code, message } with no errors dict and fall through to the shared code map.
      const validationErrors = mapValidationErrors(err);
      if (validationErrors) {
        setErrors(validationErrors);
      } else {
        setFormError(mapAuthError(err, PASSWORD_ERROR));
      }
    } finally {
      // Runs on both paths now: the form stays mounted after a success instead of being
      // torn down by a redirect.
      setSaving(false);
    }
  };

  return (
    <section className="bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-[#2f5fa8]/10 flex items-center justify-center shrink-0">
            <span className="material-symbols-outlined text-[#2f5fa8] text-[20px]">lock</span>
          </div>
          <div>
            <h2 className="text-base font-bold text-[#1e2a4a]">Đổi mật khẩu</h2>
            <p className="text-xs text-slate-500">
              Sau khi đổi, bạn sẽ được đăng xuất khỏi mọi thiết bị.
            </p>
          </div>
        </div>

        {!open && (
          <button
            type="button"
            onClick={() => {
              setSuccess("");
              setOpen(true);
            }}
            className="flex items-center gap-1.5 shrink-0 text-sm font-semibold text-[#2f5fa8] border border-[#2f5fa8]/20 hover:bg-[#2f5fa8]/10 px-3 py-2 rounded-xl transition-colors cursor-pointer"
          >
            <span className="material-symbols-outlined text-[18px]">key</span>
            Đổi mật khẩu
          </button>
        )}
      </div>

      {success && (
        <div className="p-3 text-xs font-semibold text-emerald-success bg-emerald-success/5 border border-emerald-success/15 rounded-xl leading-relaxed flex items-start gap-2">
          <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">check_circle</span>
          <span>{success}</span>
        </div>
      )}

      {open && (
        <form onSubmit={handleSubmit} className="space-y-4 pt-2 border-t border-slate-200" noValidate>
          {formError && (
            <div className="p-3 mt-4 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
              <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
              <span>{formError}</span>
            </div>
          )}

          <PasswordField
            id="currentPassword"
            label="Mật khẩu hiện tại"
            value={form.currentPassword}
            onChange={updateField("currentPassword")}
            error={errors.currentPassword}
            disabled={saving}
            autoComplete="current-password"
          />

          <PasswordField
            id="newPassword"
            label="Mật khẩu mới"
            value={form.newPassword}
            onChange={updateField("newPassword")}
            error={errors.newPassword}
            hint={PASSWORD_POLICY_HINT}
            disabled={saving}
            autoComplete="new-password"
          />

          <PasswordField
            id="confirmPassword"
            label="Xác nhận mật khẩu mới"
            value={form.confirmPassword}
            onChange={updateField("confirmPassword")}
            error={errors.confirmPassword}
            disabled={saving}
            autoComplete="new-password"
          />

          <div className="flex flex-col-reverse sm:flex-row items-stretch sm:items-center gap-3 pt-2">
            <button
              type="button"
              onClick={close}
              disabled={saving}
              className="flex-1 py-3 rounded-xl border border-slate-200 bg-white text-sm font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
            >
              Huỷ
            </button>
            <button
              type="submit"
              disabled={saving}
              className="flex-1 bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
            >
              {saving ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  Đang đổi mật khẩu...
                </>
              ) : (
                "Xác nhận đổi mật khẩu"
              )}
            </button>
          </div>
        </form>
      )}
    </section>
  );
}

export default function ProfilePage() {
  const [profile, setProfile] = React.useState(null);
  const [form, setForm] = React.useState(EMPTY_FORM);
  // The values as last loaded from the server — the baseline the diff is computed against and
  // the snapshot "Huỷ" restores.
  const [originals, setOriginals] = React.useState(EMPTY_FORM);

  const [editing, setEditing] = React.useState(false);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState("");
  const [saving, setSaving] = React.useState(false);
  const [formError, setFormError] = React.useState("");
  const [fieldErrors, setFieldErrors] = React.useState({});
  const [successMessage, setSuccessMessage] = React.useState("");

  const fetchProfile = React.useCallback(async () => {
    const response = await client.get(PROFILE_URL);
    const data = response.data || {};
    const next = toFormState(data);

    setProfile(data);
    setForm(next);
    setOriginals(next);
    return data;
  }, []);

  React.useEffect(() => {
    let cancelled = false;

    (async () => {
      setLoading(true);
      setLoadError("");
      try {
        await fetchProfile();
      } catch (err) {
        console.error(err);
        // A 401 is handled by the client's refresh/redirect interceptor; anything reaching
        // here is a real load failure.
        if (!cancelled) setLoadError(mapAuthError(err, LOAD_ERROR));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [fetchProfile]);

  const role = String(profile?.roleName || "").toLowerCase();
  const isStudent = role === "student" && profile?.student;
  const isTeacher = role === "teacher" && profile?.teacher;
  const isExpert = role === "expert" && profile?.expert;

  // Only fields actually rendered for this role take part in the diff, so a hidden field can
  // never be transmitted.
  const visibleFields = React.useMemo(() => {
    const base = ["firstName", "lastName", "phoneNumber", "dateOfBirth"];
    if (isStudent) return [...base, "gender", "school", "currentGrade"];
    if (isTeacher) return [...base, "biography"];
    if (isExpert) return [...base, "specialty"];
    return base;
  }, [isStudent, isTeacher, isExpert]);

  const diff = React.useMemo(
    () => buildDiff(form, originals, visibleFields),
    [form, originals, visibleFields],
  );
  const hasChanges = Object.keys(diff).length > 0;

  const updateField = (field) => (e) => {
    const { value } = e.target;
    setForm((prev) => ({ ...prev, [field]: value }));
    setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
  };

  const startEditing = () => {
    setEditing(true);
    setSuccessMessage("");
    setFormError("");
    setFieldErrors({});
  };

  // Discard every edit and return to the read-only view.
  const cancelEditing = () => {
    setForm(originals);
    setEditing(false);
    setFormError("");
    setFieldErrors({});
    setSuccessMessage("");
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!hasChanges || saving) return;

    // FirstName/LastName are NOT NULL in the schema and required in practice; blanking one
    // would store an empty name rather than fail, so block it here with Vietnamese copy.
    const localErrors = {};
    if (!form.firstName.trim()) localErrors.firstName = "Vui lòng nhập tên.";
    if (!form.lastName.trim()) localErrors.lastName = "Vui lòng nhập họ và tên đệm.";

    if (Object.keys(localErrors).length > 0) {
      setFieldErrors(localErrors);
      setFormError(CHECK_FIELDS_ERROR);
      return;
    }

    setSaving(true);
    setFormError("");
    setFieldErrors({});
    setSuccessMessage("");

    try {
      await client.put(PROFILE_URL, diff);
      // Re-read so the view and the diff baseline reflect what the server actually stored.
      await fetchProfile();
      setEditing(false);
      setSuccessMessage(SAVE_SUCCESS);
    } catch (err) {
      console.error(err);
      const validationErrors = mapValidationErrors(err);
      if (validationErrors) {
        setFieldErrors(validationErrors);
        setFormError(CHECK_FIELDS_ERROR);
      } else {
        setFormError(mapAuthError(err, SAVE_ERROR));
      }
    } finally {
      setSaving(false);
    }
  };

  // Role home for the back link. Prefer the profile's own role; fall back to stored session
  // role while the profile is still loading.
  const homePath = resolveHomePath(profile?.roleName || getRoleName());

  return (
    <div className="min-h-screen bg-[#eef2f7] flex flex-col">
      <header className="w-full flex items-center justify-between px-6 py-4">
        <Link to={homePath} className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg bg-[#2f5fa8] flex items-center justify-center">
            <span className="material-symbols-outlined text-white text-[18px]">functions</span>
          </div>
          <span className="font-bold text-[#1e2a4a]">MathInsight</span>
        </Link>
        <button
          type="button"
          onClick={() => logout()}
          className="flex items-center gap-1.5 text-sm font-semibold text-[#2f5fa8] hover:bg-[#2f5fa8]/10 px-3 py-2 rounded-lg transition-colors cursor-pointer"
        >
          <span className="material-symbols-outlined text-[18px]">logout</span>
          Đăng xuất
        </button>
      </header>

      <main className="flex-1 flex justify-center p-4 pb-10">
        <div className="w-full max-w-2xl space-y-4">
          <Link
            to={homePath}
            className="inline-flex items-center gap-1 text-sm font-semibold text-[#2f5fa8] hover:underline"
          >
            ← Về trang chủ
          </Link>

          <section className="bg-white rounded-2xl shadow-[0_10px_40px_rgba(30,58,95,0.10)] p-8 space-y-6">
            {loading && (
              <div className="flex flex-col items-center justify-center py-12 gap-3">
                <div className="w-8 h-8 border-2 border-[#2f5fa8] border-t-transparent rounded-full animate-spin" />
                <p className="text-sm text-slate-500">Đang tải hồ sơ...</p>
              </div>
            )}

            {!loading && loadError && (
              <div className="flex flex-col items-center justify-center py-12 gap-4 text-center">
                <div className="w-12 h-12 rounded-xl bg-deep-rose/10 flex items-center justify-center">
                  <span className="material-symbols-outlined text-deep-rose text-[26px]">error</span>
                </div>
                <p className="text-sm text-slate-500 max-w-xs leading-relaxed">{loadError}</p>
                <button
                  type="button"
                  onClick={() => window.location.reload()}
                  className="text-sm font-bold text-[#2f5fa8] hover:underline cursor-pointer"
                >
                  Thử lại
                </button>
              </div>
            )}

            {!loading && !loadError && profile && (
              <>
                {/* Identity: never editable here */}
                <div className="flex items-center justify-between gap-4 pb-6 border-b border-slate-200">
                  <div className="flex items-center gap-4">
                    {profile.avatarUrl ? (
                      <img
                        src={profile.avatarUrl}
                        alt=""
                        className="w-16 h-16 rounded-full object-cover border border-slate-200"
                      />
                    ) : (
                      <div className="w-16 h-16 rounded-full bg-[#2f5fa8]/10 border border-[#2f5fa8]/20 text-[#2f5fa8] flex items-center justify-center font-bold text-xl select-none">
                        {(profile.firstName?.[0] || profile.username?.[0] || "?").toUpperCase()}
                      </div>
                    )}
                    <div className="space-y-1.5">
                      <h1 className="text-xl font-bold text-[#1e2a4a]">
                        {[profile.lastName, profile.firstName].filter(Boolean).join(" ") ||
                          profile.username}
                      </h1>
                      <div className="flex flex-wrap items-center gap-1.5">
                        <Badge>{ROLE_LABELS[role] || profile.roleName}</Badge>
                        {isTeacher && (
                          <Badge tone={profile.teacher.isVerified ? "green" : "slate"}>
                            {profile.teacher.isVerified ? "Đã xác minh" : "Chưa xác minh"}
                          </Badge>
                        )}
                      </div>
                    </div>
                  </div>

                  {!editing && (
                    <button
                      type="button"
                      onClick={startEditing}
                      className="flex items-center gap-1.5 shrink-0 text-sm font-semibold text-[#2f5fa8] border border-[#2f5fa8]/20 hover:bg-[#2f5fa8]/10 px-3 py-2 rounded-xl transition-colors cursor-pointer"
                    >
                      <span className="material-symbols-outlined text-[18px]">edit</span>
                      Chỉnh sửa
                    </button>
                  )}
                </div>

                {formError && (
                  <div className="p-3 text-xs font-semibold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                    <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                    <span>{formError}</span>
                  </div>
                )}

                {successMessage && (
                  <div className="p-3 text-xs font-semibold text-emerald-success bg-emerald-success/5 border border-emerald-success/15 rounded-xl leading-relaxed flex items-start gap-2">
                    <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">
                      check_circle
                    </span>
                    <span>{successMessage}</span>
                  </div>
                )}

                <form onSubmit={handleSubmit} className="space-y-4">
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <ReadOnlyRow label="Tên đăng nhập" value={profile.username} />
                    <ReadOnlyRow label="Email" value={profile.email} />
                  </div>

                  {/* Họ → lastName, Tên → firstName: the same mapping RegisterStudentPage uses. */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <Field
                      id="lastName"
                      label="Họ và tên đệm"
                      error={fieldErrors.lastName}
                      editing={editing}
                      displayValue={form.lastName}
                    >
                      <input
                        id="lastName"
                        type="text"
                        value={form.lastName}
                        onChange={updateField("lastName")}
                        className={inputClass}
                        placeholder="Nhập họ và tên đệm"
                        maxLength={50}
                        disabled={saving}
                      />
                    </Field>
                    <Field
                      id="firstName"
                      label="Tên"
                      error={fieldErrors.firstName}
                      editing={editing}
                      displayValue={form.firstName}
                    >
                      <input
                        id="firstName"
                        type="text"
                        value={form.firstName}
                        onChange={updateField("firstName")}
                        className={inputClass}
                        placeholder="Nhập tên"
                        maxLength={50}
                        disabled={saving}
                      />
                    </Field>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <Field
                      id="phoneNumber"
                      label="Số điện thoại"
                      error={fieldErrors.phoneNumber}
                      editing={editing}
                      displayValue={form.phoneNumber}
                    >
                      <input
                        id="phoneNumber"
                        type="tel"
                        value={form.phoneNumber}
                        onChange={updateField("phoneNumber")}
                        className={inputClass}
                        placeholder="Nhập số điện thoại"
                        maxLength={20}
                        disabled={saving}
                      />
                    </Field>
                    <Field
                      id="dateOfBirth"
                      label="Ngày sinh"
                      error={fieldErrors.dateOfBirth}
                      editing={editing}
                      displayValue={formatDate(form.dateOfBirth)}
                    >
                      {/* value="" renders the browser's native empty date state (dd/mm/yyyy),
                          never today's date. */}
                      <input
                        id="dateOfBirth"
                        type="date"
                        value={form.dateOfBirth}
                        onChange={updateField("dateOfBirth")}
                        className={inputClass}
                        disabled={saving}
                      />
                    </Field>
                  </div>

                  {/* --- Student --- */}
                  {isStudent && (
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <Field
                        id="gender"
                        label="Giới tính"
                        error={fieldErrors.gender}
                        editing={editing}
                        displayValue={genderLabel(form.gender)}
                      >
                        <select
                          id="gender"
                          value={form.gender}
                          onChange={updateField("gender")}
                          className={`${inputClass} cursor-pointer ${
                            form.gender ? "" : "text-slate-400"
                          }`}
                          disabled={saving}
                        >
                          <option value="">Chọn giới tính</option>
                          {GENDER_OPTIONS.map((option) => (
                            <option key={option.value} value={option.value} className="text-[#1e2a4a]">
                              {option.label}
                            </option>
                          ))}
                        </select>
                      </Field>
                      <Field
                        id="currentGrade"
                        label="Khối lớp"
                        error={fieldErrors.currentGrade}
                        editing={editing}
                        displayValue={form.currentGrade}
                      >
                        <input
                          id="currentGrade"
                          type="number"
                          min={10}
                          max={12}
                          value={form.currentGrade}
                          onChange={updateField("currentGrade")}
                          className={inputClass}
                          placeholder="Nhập khối lớp (10 - 12)"
                          disabled={saving}
                        />
                      </Field>
                      <div className="sm:col-span-2">
                        <Field
                          id="school"
                          label="Trường"
                          error={fieldErrors.school}
                          editing={editing}
                          displayValue={form.school}
                        >
                          <input
                            id="school"
                            type="text"
                            value={form.school}
                            onChange={updateField("school")}
                            className={inputClass}
                            placeholder="Nhập tên trường"
                            maxLength={100}
                            disabled={saving}
                          />
                        </Field>
                      </div>
                    </div>
                  )}

                  {/* --- Teacher --- */}
                  {isTeacher && (
                    <Field
                      id="biography"
                      label="Giới thiệu bản thân"
                      error={fieldErrors.biography}
                      editing={editing}
                      displayValue={form.biography}
                    >
                      <textarea
                        id="biography"
                        rows={5}
                        value={form.biography}
                        onChange={updateField("biography")}
                        className={`${inputClass} resize-y`}
                        placeholder="Nhập giới thiệu về kinh nghiệm giảng dạy, chuyên môn..."
                        disabled={saving}
                      />
                    </Field>
                  )}

                  {/* --- Expert --- */}
                  {isExpert && (
                    <Field
                      id="specialty"
                      label="Chuyên môn"
                      error={fieldErrors.specialty}
                      editing={editing}
                      displayValue={form.specialty}
                    >
                      <input
                        id="specialty"
                        type="text"
                        value={form.specialty}
                        onChange={updateField("specialty")}
                        className={inputClass}
                        placeholder="Nhập chuyên môn"
                        maxLength={100}
                        disabled={saving}
                      />
                    </Field>
                  )}

                  {editing && (
                    <div className="flex flex-col-reverse sm:flex-row items-stretch sm:items-center gap-3 pt-2">
                      <button
                        type="button"
                        onClick={cancelEditing}
                        disabled={saving}
                        className="flex-1 py-3 rounded-xl border border-slate-200 bg-white text-sm font-semibold text-slate-600 hover:bg-slate-50 transition-colors cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                      >
                        Huỷ
                      </button>
                      <button
                        type="submit"
                        disabled={!hasChanges || saving}
                        className="flex-1 bg-[#2f5fa8] text-white py-3 rounded-xl font-semibold text-sm hover:bg-[#294f8f] transition-all active:translate-y-px flex items-center justify-center gap-2 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                      >
                        {saving ? (
                          <>
                            <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                            Đang lưu...
                          </>
                        ) : hasChanges ? (
                          "Lưu thay đổi"
                        ) : (
                          "Không có thay đổi"
                        )}
                      </button>
                    </div>
                  )}
                </form>
              </>
            )}
          </section>

          {/* UC-03. Only once the profile is known to load — an unreachable/expired session
              should not offer a password change it cannot complete. */}
          {!loading && !loadError && profile && <ChangePasswordSection />}
        </div>
      </main>
    </div>
  );
}
