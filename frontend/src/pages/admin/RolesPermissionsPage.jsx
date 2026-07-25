import * as React from "react";
import AdminLayout from "./AdminLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import { Dialog, DialogHeader, DialogTitle, DialogDescription, DialogContent, DialogFooter } from "../../components/ui/dialog";
import { adminApi } from "../../services/adminApi";
import { cn } from "../../utils/cn";

const SYSTEM_ROLE_NAMES = new Set(["Admin", "Expert", "Teacher", "Student"]);

function resolveErrorMessage(err, fallback) {
  const data = err?.response?.data;
  return data?.message || err?.message || fallback;
}

function grantedIdSet(role) {
  return new Set((role.permissions || []).filter((p) => p.isGranted).map((p) => p.permissionId));
}

function setsEqual(a, b) {
  if (a.size !== b.size) return false;
  for (const item of a) if (!b.has(item)) return false;
  return true;
}

export default function RolesPermissionsPage() {
  const [roles, setRoles] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState("");

  // roleId -> Set(permissionId) currently checked in the UI (draft state)
  const [drafts, setDrafts] = React.useState({});
  // roleId -> Set(permissionId) as last saved/loaded from the server
  const [originals, setOriginals] = React.useState({});
  // roleId -> saving flag / error message
  const [savingRoleId, setSavingRoleId] = React.useState(null);
  const [saveErrors, setSaveErrors] = React.useState({});

  // Edit role name/description modal
  const [editTarget, setEditTarget] = React.useState(null);
  const [editForm, setEditForm] = React.useState({ roleName: "", description: "" });
  const [editError, setEditError] = React.useState("");
  const [editLoading, setEditLoading] = React.useState(false);

  const applyRoles = (data) => {
    setRoles(data);
    const nextDrafts = {};
    const nextOriginals = {};
    data.forEach((role) => {
      const granted = grantedIdSet(role);
      nextDrafts[role.roleId] = new Set(granted);
      nextOriginals[role.roleId] = new Set(granted);
    });
    setDrafts(nextDrafts);
    setOriginals(nextOriginals);
  };

  const fetchRoles = React.useCallback(() => {
    setLoading(true);
    setError("");
    adminApi.getRoles()
      .then((res) => applyRoles(res.data || []))
      .catch((err) => {
        console.error("Không thể tải danh sách vai trò:", err);
        setError(resolveErrorMessage(err, "Không thể kết nối tới máy chủ API."));
      })
      .finally(() => setLoading(false));
  }, []);

  React.useEffect(() => {
    fetchRoles();
  }, [fetchRoles]);

  const togglePermission = (roleId, permissionId) => {
    setDrafts((prev) => {
      const current = new Set(prev[roleId] || []);
      if (current.has(permissionId)) {
        current.delete(permissionId);
      } else {
        current.add(permissionId);
      }
      return { ...prev, [roleId]: current };
    });
  };

  const isDirty = (roleId) => {
    const draft = drafts[roleId];
    const original = originals[roleId];
    if (!draft || !original) return false;
    return !setsEqual(draft, original);
  };

  const handleSavePermissions = async (roleId) => {
    setSavingRoleId(roleId);
    setSaveErrors((prev) => ({ ...prev, [roleId]: "" }));
    try {
      const res = await adminApi.adjustPermissions(roleId, {
        permissionIds: Array.from(drafts[roleId] || [])
      });
      const updatedRole = res.data;
      setRoles((prev) => prev.map((r) => (r.roleId === roleId ? updatedRole : r)));
      const granted = grantedIdSet(updatedRole);
      setDrafts((prev) => ({ ...prev, [roleId]: new Set(granted) }));
      setOriginals((prev) => ({ ...prev, [roleId]: new Set(granted) }));
    } catch (err) {
      console.error(err);
      setSaveErrors((prev) => ({
        ...prev,
        [roleId]: resolveErrorMessage(err, "Cập nhật quyền thất bại.")
      }));
    } finally {
      setSavingRoleId(null);
    }
  };

  const openEditModal = (role) => {
    setEditTarget(role);
    setEditForm({ roleName: role.roleName, description: role.description || "" });
    setEditError("");
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    if (!editTarget) return;

    setEditLoading(true);
    setEditError("");
    try {
      const res = await adminApi.updateRole(editTarget.roleId, {
        roleName: editForm.roleName.trim(),
        description: editForm.description.trim()
      });
      const updatedRole = res.data;
      setRoles((prev) => prev.map((r) => (r.roleId === editTarget.roleId ? updatedRole : r)));
      setEditTarget(null);
    } catch (err) {
      console.error(err);
      setEditError(resolveErrorMessage(err, "Cập nhật vai trò thất bại."));
    } finally {
      setEditLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">

        <DashboardPageHeader
          title="Vai trò & Quyền"
          subtitle="Chỉnh sửa mô tả vai trò và bật/tắt quyền hạn cho từng vai trò trong hệ thống."
        />

        {error && (
          <div className="p-4 border rounded-xl flex items-center justify-between text-sm font-semibold shadow-sm bg-error/10 border-error/20 text-error">
            <div className="flex items-center gap-2">
              <span className="material-symbols-outlined">error</span>
              <span>{error}</span>
            </div>
          </div>
        )}

        {loading ? (
          <div className="flex flex-col items-center justify-center gap-3 py-20 text-on-surface-variant">
            <div className="w-8 h-8 border-4 border-primary border-t-transparent rounded-full animate-spin"></div>
            <span>Đang tải danh sách vai trò...</span>
          </div>
        ) : (
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {roles.map((role) => {
              const isSystemRole = SYSTEM_ROLE_NAMES.has(role.roleName);
              const dirty = isDirty(role.roleId);
              const saving = savingRoleId === role.roleId;

              return (
                <div key={role.roleId} className="bg-pure-surface border border-whisper-border rounded-xl shadow-sm p-5 flex flex-col gap-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2 mb-1">
                        <h3 className="text-[16px] font-bold text-on-surface">{role.roleName}</h3>
                        {isSystemRole && <Badge variant="outline">Vai trò hệ thống</Badge>}
                      </div>
                      <p className="text-[13px] text-on-surface-variant">{role.description || "Chưa có mô tả."}</p>
                    </div>
                    <button
                      onClick={() => openEditModal(role)}
                      className="p-1.5 text-on-surface-variant hover:text-primary hover:bg-surface-container rounded transition-colors cursor-pointer shrink-0"
                      aria-label="Sửa vai trò"
                      title="Sửa"
                    >
                      <span className="material-symbols-outlined text-[18px]">edit</span>
                    </button>
                  </div>

                  <div className="border-t border-whisper-border pt-3 space-y-2">
                    <h4 className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1">Quyền hạn</h4>
                    {(role.permissions || []).length === 0 ? (
                      <p className="text-[12px] text-on-surface-variant italic">Chưa có quyền nào được khai báo.</p>
                    ) : (
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        {role.permissions.map((permission) => {
                          const checked = drafts[role.roleId]?.has(permission.permissionId) || false;
                          return (
                            <label
                              key={permission.permissionId}
                              className="flex items-start gap-2 p-2 rounded-lg border border-transparent hover:border-whisper-border hover:bg-surface-container-low transition-colors cursor-pointer"
                              title={permission.description || permission.permissionKey}
                            >
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => togglePermission(role.roleId, permission.permissionId)}
                                className="mt-0.5 accent-primary cursor-pointer"
                              />
                              <span className="text-[12px] font-mono text-on-surface leading-tight">
                                {permission.permissionKey}
                              </span>
                            </label>
                          );
                        })}
                      </div>
                    )}

                    {saveErrors[role.roleId] && (
                      <div className="p-2.5 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-lg flex items-start gap-2">
                        <span className="material-symbols-outlined text-[14px] shrink-0 mt-0.5">error</span>
                        <span>{saveErrors[role.roleId]}</span>
                      </div>
                    )}

                    <div className="flex justify-end pt-1">
                      <Button
                        size="sm"
                        onClick={() => handleSavePermissions(role.roleId)}
                        disabled={!dirty || saving}
                      >
                        {saving ? "Đang lưu..." : "Lưu quyền"}
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* EDIT ROLE DIALOG */}
      <Dialog isOpen={!!editTarget} onClose={() => setEditTarget(null)} variant="modal">
        <DialogHeader>
          <DialogTitle>Sửa vai trò</DialogTitle>
          <DialogDescription>
            {editTarget && SYSTEM_ROLE_NAMES.has(editTarget.roleName)
              ? "Vai trò hệ thống không thể đổi tên, chỉ có thể sửa mô tả."
              : "Cập nhật tên và mô tả của vai trò."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleEditSubmit}>
          <DialogContent className="space-y-4">
            {editError && (
              <div className="p-3 text-xs font-bold text-deep-rose bg-deep-rose/5 border border-deep-rose/15 rounded-xl leading-relaxed flex items-start gap-2">
                <span className="material-symbols-outlined text-[16px] shrink-0 mt-0.5">error</span>
                <span>{editError}</span>
              </div>
            )}

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Tên vai trò</label>
              <input
                value={editForm.roleName}
                onChange={(e) => setEditForm({ ...editForm, roleName: e.target.value })}
                disabled={editLoading || (editTarget && SYSTEM_ROLE_NAMES.has(editTarget.roleName))}
                className={cn(
                  "w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold",
                  editTarget && SYSTEM_ROLE_NAMES.has(editTarget.roleName) && "opacity-50 cursor-not-allowed"
                )}
              />
            </div>

            <div>
              <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Mô tả</label>
              <textarea
                value={editForm.description}
                onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                rows="3"
                className="w-full px-3 py-2 bg-transparent border border-outline-variant rounded-lg text-xs focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-all font-semibold"
                disabled={editLoading}
              />
            </div>
          </DialogContent>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setEditTarget(null)} disabled={editLoading}>
              Hủy
            </Button>
            <Button type="submit" disabled={editLoading}>
              {editLoading ? "Đang lưu..." : "Lưu thay đổi"}
            </Button>
          </DialogFooter>
        </form>
      </Dialog>
    </AdminLayout>
  );
}
