import * as React from "react";
import AdminLayout from "./AdminLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { CustomSelect } from "../../components/ui/custom-select";
import { adminApi } from "../../services/adminApi";

const CREATABLE_ROLES = [
  { value: "Student", label: "Học sinh" },
  { value: "Teacher", label: "Giáo viên" },
  { value: "Expert", label: "Chuyên gia" }
];

const ERROR_MESSAGES = {
  EMAIL_ALREADY_EXISTS: "Email này đã được sử dụng.",
  USERNAME_ALREADY_EXISTS: "Tên đăng nhập này đã được sử dụng.",
  PASSWORD_TOO_SHORT: "Mật khẩu phải có ít nhất 8 ký tự.",
  INVALID_ROLE: "Vai trò không hợp lệ. Chỉ chấp nhận Học sinh, Giáo viên, Chuyên gia.",
  ROLE_NOT_FOUND: "Không tìm thấy vai trò đã chọn.",
  ACCOUNT_NOT_FOUND: "Không tìm thấy tài khoản.",
  CANNOT_DEACTIVATE_SELF: "Bạn không thể tự vô hiệu hóa chính tài khoản của mình.",
  INVALID_EXCEL_FILE: "Tệp Excel không hợp lệ. Vui lòng dùng đúng file .xlsx theo mẫu."
};

function resolveErrorMessage(err, fallback) {
  const data = err?.response?.data;
  if (data?.code && ERROR_MESSAGES[data.code]) return ERROR_MESSAGES[data.code];
  return data?.message || err?.message || fallback;
}

function formatDate(value) {
  if (!value) return "-";
  try {
    return new Date(value).toLocaleDateString("vi-VN");
  } catch {
    return "-";
  }
}

const emptyCreateForm = {
  username: "",
  email: "",
  password: "",
  firstName: "",
  lastName: "",
  phoneNumber: "",
  dateOfBirth: "",
  roleName: "Student"
};

export default function AccountManagementPage() {
  const currentAccountId = localStorage.getItem("AccountId");

  // List state
  const [accounts, setAccounts] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  // Filters
  const [searchInput, setSearchInput] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [roleFilter, setRoleFilter] = React.useState("");
  const [statusFilter, setStatusFilter] = React.useState("");

  // Pagination
  const [pageIndex, setPageIndex] = React.useState(1);
  const [pageSize] = React.useState(10);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);

  // Roles (for Edit modal role dropdown)
  const [roles, setRoles] = React.useState([]);

  // Create modal
  const [isCreateOpen, setIsCreateOpen] = React.useState(false);
  const [createForm, setCreateForm] = React.useState(emptyCreateForm);
  const [createError, setCreateError] = React.useState("");
  const [createLoading, setCreateLoading] = React.useState(false);

  // Import modal
  const [isImportOpen, setIsImportOpen] = React.useState(false);
  const [importFile, setImportFile] = React.useState(null);
  const [importError, setImportError] = React.useState("");
  const [importLoading, setImportLoading] = React.useState(false);
  const [importResult, setImportResult] = React.useState(null);

  // Edit modal
  const [isEditOpen, setIsEditOpen] = React.useState(false);
  const [editTarget, setEditTarget] = React.useState(null);
  const [editForm, setEditForm] = React.useState({ firstName: "", lastName: "", email: "", roleId: "" });
  const [editError, setEditError] = React.useState("");
  const [editLoading, setEditLoading] = React.useState(false);

  // Deactivate confirm modal
  const [deactivateTarget, setDeactivateTarget] = React.useState(null);
  const [deactivateError, setDeactivateError] = React.useState("");
  const [deactivateLoading, setDeactivateLoading] = React.useState(false);

  // Debounce search input -> search
  React.useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPageIndex(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchInput]);

  // Load roles once (for Edit modal dropdown)
  React.useEffect(() => {
    adminApi.getRoles()
      .then((res) => setRoles(res.data || []))
      .catch((err) => console.error("Không thể tải danh sách vai trò:", err));
  }, []);

  const fetchAccounts = React.useCallback(() => {
    setLoading(true);
    setError("");

    const params = { pageIndex, pageSize };
    if (roleFilter) params.role = roleFilter;
    if (statusFilter === "ACTIVE") params.isActive = true;
    if (statusFilter === "INACTIVE") params.isActive = false;
    if (search) params.search = search;

    adminApi.getAccounts(params)
      .then((res) => {
        const data = res.data || {};
        setAccounts(data.items || []);
        setTotalCount(data.totalCount || 0);
        setTotalPages(data.totalPages || 1);
      })
      .catch((err) => {
        console.error("Không thể tải danh sách tài khoản:", err);
        setError(resolveErrorMessage(err, "Không thể kết nối tới máy chủ API."));
      })
      .finally(() => setLoading(false));
  }, [pageIndex, pageSize, roleFilter, statusFilter, search]);

  React.useEffect(() => {
    fetchAccounts();
  }, [fetchAccounts]);

  const handleResetFilters = () => {
    setSearchInput("");
    setSearch("");
    setRoleFilter("");
    setStatusFilter("");
    setPageIndex(1);
  };

  // ---------- Create ----------
  const openCreateModal = () => {
    setCreateForm(emptyCreateForm);
    setCreateError("");
    setIsCreateOpen(true);
  };

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (!createForm.username.trim() || !createForm.email.trim() || !createForm.password.trim() ||
      !createForm.firstName.trim() || !createForm.lastName.trim()) {
      setCreateError("Vui lòng điền đầy đủ các trường bắt buộc.");
      return;
    }
    if (createForm.password.length < 8) {
      setCreateError(ERROR_MESSAGES.PASSWORD_TOO_SHORT);
      return;
    }

    setCreateLoading(true);
    setCreateError("");
    try {
      await adminApi.createAccountManually({
        username: createForm.username.trim(),
        email: createForm.email.trim(),
        password: createForm.password,
        firstName: createForm.firstName.trim(),
        lastName: createForm.lastName.trim(),
        phoneNumber: createForm.phoneNumber.trim() || null,
        dateOfBirth: createForm.dateOfBirth || null,
        roleName: createForm.roleName
      });
      setIsCreateOpen(false);
      fetchAccounts();
    } catch (err) {
      console.error(err);
      setCreateError(resolveErrorMessage(err, "Tạo tài khoản thất bại."));
    } finally {
      setCreateLoading(false);
    }
  };

  // ---------- Import ----------
  const openImportModal = () => {
    setImportFile(null);
    setImportError("");
    setImportResult(null);
    setIsImportOpen(true);
  };

  const handleImportSubmit = async (e) => {
    e.preventDefault();
    if (!importFile) {
      setImportError("Vui lòng chọn tệp .xlsx để nhập.");
      return;
    }

    setImportLoading(true);
    setImportError("");
    try {
      const res = await adminApi.importAccounts(importFile);
      setImportResult(res.data);
      fetchAccounts();
    } catch (err) {
      console.error(err);
      setImportError(resolveErrorMessage(err, "Nhập dữ liệu từ Excel thất bại."));
    } finally {
      setImportLoading(false);
    }
  };

  // ---------- Edit ----------
  const openEditModal = (account) => {
    setEditTarget(account);
    setEditForm({
      firstName: account.firstName,
      lastName: account.lastName,
      email: account.email,
      roleId: account.roleId
    });
    setEditError("");
    setIsEditOpen(true);
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!editTarget) return;
    if (!editForm.firstName.trim() || !editForm.lastName.trim() || !editForm.email.trim() || !editForm.roleId) {
      setEditError("Vui lòng điền đầy đủ các trường bắt buộc.");
      return;
    }

    setEditLoading(true);
    setEditError("");
    try {
      await adminApi.updateAccount(editTarget.accountId, {
        firstName: editForm.firstName.trim(),
        lastName: editForm.lastName.trim(),
        email: editForm.email.trim(),
        roleId: editForm.roleId
      });
      setIsEditOpen(false);
      fetchAccounts();
    } catch (err) {
      console.error(err);
      setEditError(resolveErrorMessage(err, "Cập nhật tài khoản thất bại."));
    } finally {
      setEditLoading(false);
    }
  };

  // ---------- Activate / Deactivate ----------
  const handleActivate = async (account) => {
    try {
      await adminApi.toggleAccountStatus(account.accountId, true);
      fetchAccounts();
    } catch (err) {
      console.error(err);
      setError(resolveErrorMessage(err, "Kích hoạt tài khoản thất bại."));
    }
  };

  const openDeactivateConfirm = (account) => {
    setDeactivateTarget(account);
    setDeactivateError("");
  };

  const handleConfirmDeactivate = async () => {
    if (!deactivateTarget) return;
    setDeactivateLoading(true);
    setDeactivateError("");
    try {
      await adminApi.toggleAccountStatus(deactivateTarget.accountId, false);
      setDeactivateTarget(null);
      fetchAccounts();
    } catch (err) {
      console.error(err);
      setDeactivateError(resolveErrorMessage(err, "Vô hiệu hóa tài khoản thất bại."));
    } finally {
      setDeactivateLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">

        <DashboardPageHeader
          title="Quản lý tài khoản"
          subtitle="Xem, tìm kiếm, tạo mới và quản lý trạng thái tài khoản người dùng trong hệ thống."
        >
          <Button variant="outline" onClick={openImportModal}>
            <span className="material-symbols-outlined text-[18px] mr-1.5">upload_file</span>
            Nhập từ Excel
          </Button>
          <Button onClick={openCreateModal}>
            <span className="material-symbols-outlined text-[18px] mr-1.5">person_add</span>
            Tạo tài khoản
          </Button>
        </DashboardPageHeader>

        {error && (
          <div className="p-4 border rounded-xl flex items-center justify-between text-sm font-semibold shadow-sm bg-error/10 border-error/20 text-error">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined">error</span>
              <span>{error}</span>
            </div>
          </div>
        )}

        {/* Filters */}
        <div className="grid grid-cols-1 gap-3 bg-pure-surface border border-whisper-border p-4 rounded-xl shadow-sm md:grid-cols-4">
          <div className="bg-surface-container-lowest border border-whisper-border rounded-lg flex items-center shadow-inner focus-within:ring-2 focus-within:ring-primary focus-within:border-transparent transition-all h-10 md:col-span-2">
            <span className="material-symbols-outlined text-on-surface-variant px-3 select-none">search</span>
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full bg-transparent border-none focus:ring-0 text-[14px] text-on-surface placeholder-on-surface-variant outline-none py-2 pr-4"
              placeholder="Tìm theo tên đăng nhập, email, họ tên..."
              type="text"
            />
          </div>

          <CustomSelect
            value={roleFilter || "ALL"}
            onValueChange={(val) => { setRoleFilter(val === "ALL" ? "" : val); setPageIndex(1); }}
            placeholder="Vai trò"
            items={[
              { value: "ALL", label: "Tất cả vai trò" },
              { value: "Admin", label: "Quản trị viên" },
              { value: "Expert", label: "Chuyên gia" },
              { value: "Teacher", label: "Giáo viên" },
              { value: "Student", label: "Học sinh" }
            ]}
          />

          <div className="flex gap-2">
            <CustomSelect
              value={statusFilter || "ALL"}
              onValueChange={(val) => { setStatusFilter(val === "ALL" ? "" : val); setPageIndex(1); }}
              placeholder="Trạng thái"
              items={[
                { value: "ALL", label: "Tất cả trạng thái" },
                { value: "ACTIVE", label: "Đang hoạt động" },
                { value: "INACTIVE", label: "Đã vô hiệu hóa" }
              ]}
            />
            <button
              onClick={handleResetFilters}
              className="w-10 h-10 shrink-0 p-0 inline-flex items-center justify-center text-on-surface-variant hover:text-error transition-colors rounded-lg border border-whisper-border bg-pure-surface hover:bg-surface-container-low cursor-pointer"
              aria-label="Xóa bộ lọc"
              title="Xóa bộ lọc"
            >
              <span className="material-symbols-outlined text-[20px]">filter_alt_off</span>
            </button>
          </div>
        </div>

        {/* Table */}
        <div className="w-full bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead className="bg-surface-container-low border-b border-whisper-border">
                <tr className="text-on-surface-variant uppercase text-[11px] font-bold tracking-wider">
                  <th className="py-3 px-4">Tên đăng nhập</th>
                  <th className="py-3 px-4">Họ tên</th>
                  <th className="py-3 px-4">Email</th>
                  <th className="py-3 px-4 w-32">Vai trò</th>
                  <th className="py-3 px-4 w-36">Trạng thái</th>
                  <th className="py-3 px-4 w-32">Ngày tạo</th>
                  <th className="py-3 px-4 w-28 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-whisper-border bg-pure-surface text-[14px]">
                {loading ? (
                  <tr>
                    <td colSpan={7} className="py-20 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
                        <span>Đang tải danh sách tài khoản...</span>
                      </div>
                    </td>
                  </tr>
                ) : accounts.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-12 text-center text-on-surface-variant">
                      <div className="flex flex-col items-center gap-2">
                        <span className="material-symbols-outlined text-[36px] text-outline-variant">search_off</span>
                        Không tìm thấy tài khoản phù hợp.
                      </div>
                    </td>
                  </tr>
                ) : (
                  accounts.map((account) => (
                    <tr key={account.accountId} className="hover:bg-surface-bright transition-all group duration-150">
                      <td className="py-3 px-4 font-semibold text-on-surface">{account.username}</td>
                      <td className="py-3 px-4">{account.firstName} {account.lastName}</td>
                      <td className="py-3 px-4 text-on-surface-variant">{account.email}</td>
                      <td className="py-3 px-4">
                        <Badge variant="outline">{account.roleName}</Badge>
                      </td>
                      <td className="py-3 px-4">
                        <Badge variant={account.isActive ? "success" : "secondary"}>
                          {account.isActive ? "Hoạt động" : "Vô hiệu hóa"}
                        </Badge>
                      </td>
                      <td className="py-3 px-4 text-on-surface-variant">{formatDate(account.createdTime)}</td>
                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => openEditModal(account)}
                            className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer"
                            aria-label="Sửa tài khoản"
                            title="Sửa"
                          >
                            <span className="material-symbols-outlined text-[18px]">edit</span>
                          </button>
                          {account.isActive ? (
                            <button
                              onClick={() => openDeactivateConfirm(account)}
                              disabled={account.accountId === currentAccountId}
                              className="p-1.5 text-on-surface-variant hover:text-error hover:bg-error/5 rounded transition-colors cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
                              aria-label="Vô hiệu hóa tài khoản"
                              title={account.accountId === currentAccountId ? "Không thể tự vô hiệu hóa chính mình" : "Vô hiệu hóa"}
                            >
                              <span className="material-symbols-outlined text-[18px]">block</span>
                            </button>
                          ) : (
                            <button
                              onClick={() => handleActivate(account)}
                              className="p-1.5 text-on-surface-variant hover:text-emerald-success hover:bg-emerald-success/5 rounded transition-colors cursor-pointer"
                              aria-label="Kích hoạt tài khoản"
                              title="Kích hoạt"
                            >
                              <span className="material-symbols-outlined text-[18px]">check_circle</span>
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          <div className="bg-surface-container-low border-t border-whisper-border p-4 flex items-center justify-between">
            <span className="text-xs text-on-surface-variant font-bold">
              Hiển thị {accounts.length} trong số {totalCount} tài khoản
            </span>
            <div className="flex gap-1">
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                disabled={pageIndex <= 1 || loading}
              >
                Trước
              </Button>
              <div className="flex items-center justify-center bg-pure-surface border border-whisper-border rounded px-3 text-xs font-bold select-none text-on-surface">
                {pageIndex} / {totalPages || 1}
              </div>
              <Button
                variant="outline"
                size="sm"
                className="normal-case px-2.5 h-8 font-bold"
                onClick={() => setPageIndex((p) => Math.min(totalPages || 1, p + 1))}
                disabled={pageIndex >= totalPages || loading}
              >
                Tiếp
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* CREATE ACCOUNT DIALOG */}
      <Dialog isOpen={isCreateOpen} onClose={() => setIsCreateOpen(false)} variant="modal">
        <DialogHeader>
          <DialogTitle>Tạo tài khoản mới</DialogTitle>
          <DialogDescription>Tạo trực tiếp tài khoản Học sinh, Giáo viên hoặc Chuyên gia với trạng thái hoạt động ngay.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleCreateSubmit}>
          <DialogContent className="space-y-4">
            {createError && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{createError}</span>
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Họ <span className="text-error">*</span></label>
                <input
                  value={createForm.lastName}
                  onChange={(e) => setCreateForm({ ...createForm, lastName: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={createLoading}
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Tên <span className="text-error">*</span></label>
                <input
                  value={createForm.firstName}
                  onChange={(e) => setCreateForm({ ...createForm, firstName: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={createLoading}
                />
              </div>
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Tên đăng nhập <span className="text-error">*</span></label>
              <input
                value={createForm.username}
                onChange={(e) => setCreateForm({ ...createForm, username: e.target.value })}
                className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                disabled={createLoading}
              />
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Email <span className="text-error">*</span></label>
              <input
                type="email"
                value={createForm.email}
                onChange={(e) => setCreateForm({ ...createForm, email: e.target.value })}
                className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                disabled={createLoading}
              />
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Mật khẩu (tối thiểu 8 ký tự) <span className="text-error">*</span></label>
              <input
                type="password"
                value={createForm.password}
                onChange={(e) => setCreateForm({ ...createForm, password: e.target.value })}
                className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                disabled={createLoading}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Số điện thoại</label>
                <input
                  value={createForm.phoneNumber}
                  onChange={(e) => setCreateForm({ ...createForm, phoneNumber: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={createLoading}
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Ngày sinh</label>
                <input
                  type="date"
                  value={createForm.dateOfBirth}
                  onChange={(e) => setCreateForm({ ...createForm, dateOfBirth: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={createLoading}
                />
              </div>
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Vai trò <span className="text-error">*</span></label>
              <CustomSelect
                value={createForm.roleName}
                onValueChange={(val) => setCreateForm({ ...createForm, roleName: val })}
                items={CREATABLE_ROLES}
                disabled={createLoading}
              />
            </div>
          </DialogContent>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setIsCreateOpen(false)} disabled={createLoading}>
              Hủy
            </Button>
            <Button type="submit" disabled={createLoading}>
              {createLoading ? "Đang tạo..." : "Tạo tài khoản"}
            </Button>
          </DialogFooter>
        </form>
      </Dialog>

      {/* IMPORT EXCEL DIALOG */}
      <Dialog isOpen={isImportOpen} onClose={() => setIsImportOpen(false)} variant="modal">
        <DialogHeader>
          <DialogTitle>Nhập tài khoản từ Excel</DialogTitle>
          <DialogDescription>
            Tệp .xlsx theo thứ tự cột: Username, Email, Password, FirstName, LastName, PhoneNumber, DateOfBirth (yyyy-MM-dd), Role (Student/Teacher/Expert).
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleImportSubmit}>
          <DialogContent className="space-y-4">
            {importError && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{importError}</span>
              </div>
            )}

            {!importResult && (
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Tệp Excel (.xlsx)</label>
                <input
                  type="file"
                  accept=".xlsx"
                  onChange={(e) => setImportFile(e.target.files?.[0] || null)}
                  className="w-full text-xs file:mr-3 file:py-2 file:px-3 file:rounded-lg file:border-0 file:text-xs file:font-bold file:bg-primary/10 file:text-primary"
                  disabled={importLoading}
                />
              </div>
            )}

            {importResult && (
              <div className="space-y-3">
                <div className="grid grid-cols-3 gap-3 text-center">
                  <div className="p-3 rounded-xl bg-surface-container border border-whisper-border">
                    <p className="text-lg font-bold text-on-surface">{importResult.totalRows}</p>
                    <p className="text-[10px] uppercase font-bold text-on-surface-variant">Tổng số dòng</p>
                  </div>
                  <div className="p-3 rounded-xl bg-emerald-success/10 border border-emerald-success/20">
                    <p className="text-lg font-bold text-emerald-success">{importResult.successCount}</p>
                    <p className="text-[10px] uppercase font-bold text-on-surface-variant">Thành công</p>
                  </div>
                  <div className="p-3 rounded-xl bg-error/10 border border-error/20">
                    <p className="text-lg font-bold text-error">{importResult.skippedCount}</p>
                    <p className="text-[10px] uppercase font-bold text-on-surface-variant">Bị bỏ qua</p>
                  </div>
                </div>

                {importResult.skippedRows?.length > 0 && (
                  <div className="border border-whisper-border rounded-xl overflow-hidden">
                    <table className="w-full text-left text-xs">
                      <thead className="bg-surface-container-low">
                        <tr>
                          <th className="py-2 px-3">Dòng</th>
                          <th className="py-2 px-3">Username</th>
                          <th className="py-2 px-3">Email</th>
                          <th className="py-2 px-3">Lý do</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-whisper-border">
                        {importResult.skippedRows.map((row) => (
                          <tr key={row.rowNumber}>
                            <td className="py-2 px-3">{row.rowNumber}</td>
                            <td className="py-2 px-3">{row.username || "-"}</td>
                            <td className="py-2 px-3">{row.email || "-"}</td>
                            <td className="py-2 px-3 text-error">{row.reason}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}
          </DialogContent>
          <DialogFooter>
            {!importResult ? (
              <>
                <Button type="button" variant="outline" onClick={() => setIsImportOpen(false)} disabled={importLoading}>
                  Hủy
                </Button>
                <Button type="submit" disabled={importLoading}>
                  {importLoading ? "Đang nhập..." : "Nhập dữ liệu"}
                </Button>
              </>
            ) : (
              <Button type="button" onClick={() => setIsImportOpen(false)}>
                Đóng
              </Button>
            )}
          </DialogFooter>
        </form>
      </Dialog>

      {/* EDIT ACCOUNT DIALOG */}
      <Dialog isOpen={isEditOpen} onClose={() => setIsEditOpen(false)} variant="modal">
        <DialogHeader>
          <DialogTitle>Sửa tài khoản</DialogTitle>
          <DialogDescription>Cập nhật họ tên, email và vai trò của tài khoản.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleEditSubmit}>
          <DialogContent className="space-y-4">
            {editError && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{editError}</span>
              </div>
            )}

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Họ</label>
                <input
                  value={editForm.lastName}
                  onChange={(e) => setEditForm({ ...editForm, lastName: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={editLoading}
                />
              </div>
              <div>
                <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Tên</label>
                <input
                  value={editForm.firstName}
                  onChange={(e) => setEditForm({ ...editForm, firstName: e.target.value })}
                  className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                  disabled={editLoading}
                />
              </div>
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Email</label>
              <input
                type="email"
                value={editForm.email}
                onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                disabled={editLoading}
              />
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Vai trò</label>
              <CustomSelect
                value={editForm.roleId}
                onValueChange={(val) => setEditForm({ ...editForm, roleId: val })}
                items={roles.map((r) => ({ value: r.roleId, label: r.roleName }))}
                disabled={editLoading}
              />
            </div>
          </DialogContent>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setIsEditOpen(false)} disabled={editLoading}>
              Hủy
            </Button>
            <Button type="submit" disabled={editLoading}>
              {editLoading ? "Đang lưu..." : "Lưu thay đổi"}
            </Button>
          </DialogFooter>
        </form>
      </Dialog>

      {/* DEACTIVATE CONFIRM DIALOG */}
      <Dialog isOpen={!!deactivateTarget} onClose={() => setDeactivateTarget(null)} variant="modal">
        <DialogHeader>
          <DialogTitle>Vô hiệu hóa tài khoản?</DialogTitle>
          <DialogDescription>
            Tài khoản "{deactivateTarget?.username}" sẽ không thể đăng nhập cho đến khi được kích hoạt lại.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          {deactivateError && (
            <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
              <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
              <span>{deactivateError}</span>
            </div>
          )}
        </DialogContent>
        <DialogFooter>
          <Button variant="outline" onClick={() => setDeactivateTarget(null)} disabled={deactivateLoading}>
            Hủy
          </Button>
          <Button
            className="bg-error hover:bg-deep-rose text-white"
            onClick={handleConfirmDeactivate}
            disabled={deactivateLoading}
          >
            {deactivateLoading ? "Đang xử lý..." : "Vô hiệu hóa"}
          </Button>
        </DialogFooter>
      </Dialog>
    </AdminLayout>
  );
}
