import * as React from "react";
import { useState, useEffect, useCallback, useRef } from "react";
import TeacherLayout from "./TeacherLayout";
import DashboardPageHeader from "../../components/layout/DashboardPageHeader";
import { getMaterials, uploadMaterial, deactivateMaterial, updateMaterial, activateMaterial } from "../../services/learningApi";

export default function MaterialListPage() {
  const [materials, setMaterials] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [uploading, setUploading] = useState(false);
  
  // Form Upload
  const [file, setFile] = useState(null);
  const [docName, setDocName] = useState("");
  const fileInputRef = useRef(null);

  // Inline Edit
  const [editingId, setEditingId] = useState(null);
  const [editName, setEditName] = useState("");

  const fetchMaterials = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getMaterials({});
      setMaterials(res.data?.items || res.data || []);
    } catch (err) {
      console.error("Lỗi khi tải danh sách tài liệu:", err);
      setMaterials([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchMaterials(); }, [fetchMaterials]);

  const handleFileChange = (e) => {
    const selected = e.target.files?.[0];
    if (selected) {
      // Validate Size (500MB)
      if (selected.size > 500 * 1024 * 1024) {
        alert("Kích thước tệp vượt quá 500MB!");
        return;
      }
      // Validate Format
      const ext = selected.name.split('.').pop().toLowerCase();
      if (!['pdf', 'mp4', 'docx'].includes(ext)) {
        alert("Chỉ hỗ trợ tệp định dạng PDF, MP4, DOCX!");
        return;
      }

      setFile(selected);
      if (!docName) setDocName(selected.name.split('.')[0]);
    }
  };

  const handleUpload = async () => {
    if (!file || !docName) return;
    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("materialName", docName);
      await uploadMaterial(formData);
      setShowUploadModal(false);
      setFile(null);
      setDocName("");
      fetchMaterials();
    } catch (err) {
      console.error(err);
      alert("Tải lên thất bại: " + (err.response?.data?.message || err.message || "Lỗi không xác định"));
    } finally {
      setUploading(false);
    }
  };

  const handleDeactivate = async (id) => {
    if (!window.confirm("Bạn có chắc chắn muốn ngừng hoạt động tài liệu này?")) return;
    try {
      await deactivateMaterial(id);
      fetchMaterials();
    } catch (e) {
      console.error(e);
      alert("Lỗi khi ngừng hoạt động!");
    }
  };

  const handleActivate = async (id) => {
    try {
      await activateMaterial(id);
      fetchMaterials();
    } catch (e) {
      console.error(e);
      alert("Lỗi khi kích hoạt lại!");
    }
  };

  const handleSaveRename = async (id) => {
    if (!editName.trim()) return;
    try {
      await updateMaterial(id, { materialName: editName });
      setEditingId(null);
      fetchMaterials();
    } catch (e) {
      console.error(e);
      alert("Đổi tên thất bại!");
    }
  };

  const getFormatIcon = (format) => {
    const f = format?.toUpperCase() || "";
    if (f.includes("PDF")) return { icon: "picture_as_pdf", color: "text-[#ef4444]" };
    if (f.includes("MP4") || f.includes("VIDEO")) return { icon: "movie", color: "text-[#3b82f6]" };
    if (f.includes("DOC") || f.includes("WORD")) return { icon: "description", color: "text-[#2563eb]" };
    return { icon: "insert_drive_file", color: "text-on-surface-variant" };
  };

  return (
    <TeacherLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        <DashboardPageHeader
          title="Tài liệu của tôi"
          subtitle="Tải lên, quản lý và gắn tài liệu vào bài giảng."
        >
          <button
            onClick={() => setShowUploadModal(true)}
            className="flex items-center gap-2 bg-primary text-on-primary py-2 px-4 rounded-lg font-medium text-[16px] hover:opacity-90 transition-opacity"
          >
            <span className="material-symbols-outlined" style={{ fontVariationSettings: "'FILL' 0" }}>add</span>
            Tải lên tài liệu
          </button>
        </DashboardPageHeader>

        <div className="bg-pure-surface border border-whisper-border rounded-xl overflow-hidden shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-surface-container-low border-b border-whisper-border">
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider w-16">STT</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Tên tài liệu</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Định dạng</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Trạng thái</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Ngày tải lên</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider">Bài giảng liên kết</th>
                  <th className="py-3 px-4 text-[12px] font-semibold text-on-surface-variant uppercase tracking-wider text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="text-[13px] text-on-surface divide-y divide-whisper-border">
                {materials.map((item, idx) => {
                  const formatInfo = getFormatIcon(item.format || item.fileType);
                  const isActive = item.status === "Active";
                  return (
                    <tr key={item.id} className="hover:bg-surface-container-lowest transition-colors group">
                      <td className="py-3 px-4 text-on-surface-variant font-mono">{String(idx + 1).padStart(2, '0')}</td>
                      <td className="py-3 px-4 font-medium text-[16px] text-on-surface">
                        {editingId === (item.id || item.materialId) ? (
                          <div className="flex items-center gap-2">
                            <input 
                              type="text" 
                              className="px-2 py-1 border border-outline-variant rounded focus:ring-primary focus:border-primary text-[14px]"
                              value={editName}
                              onChange={(e) => setEditName(e.target.value)}
                              onKeyDown={(e) => {
                                if (e.key === 'Enter') handleSaveRename(item.id || item.materialId);
                                if (e.key === 'Escape') setEditingId(null);
                              }}
                              autoFocus
                            />
                            <button onClick={() => handleSaveRename(item.id || item.materialId)} className="text-primary hover:text-primary-container"><span className="material-symbols-outlined text-[18px]">check</span></button>
                            <button onClick={() => setEditingId(null)} className="text-error hover:text-error-container"><span className="material-symbols-outlined text-[18px]">close</span></button>
                          </div>
                        ) : (
                          item.name || item.materialName
                        )}
                      </td>
                      <td className="py-3 px-4">
                        <div className="flex items-center gap-2">
                          <span className={`material-symbols-outlined ${formatInfo.color} text-[18px]`}>{formatInfo.icon}</span>
                          <span>{item.format || item.fileType}</span>
                        </div>
                      </td>
                      <td className="py-3 px-4">
                        {isActive ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold tracking-wide bg-[#d1fae5] text-[#065f46]">Hoạt động</span>
                        ) : (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold tracking-wide bg-[#ffe4e6] text-[#9f1239]">Ngừng hoạt động</span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-on-surface-variant font-mono">{new Date(item.uploadedAt || item.uploadedTime).toLocaleDateString("vi-VN")}</td>
                      <td className="py-3 px-4">
                        {item.lectureName ? (
                          <a className="text-primary hover:underline font-medium" href="#">{item.lectureName}</a>
                        ) : (
                          <span className="text-on-surface-variant">—</span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-2 text-on-surface-variant opacity-0 group-hover:opacity-100 transition-opacity">
                          {isActive && editingId !== (item.id || item.materialId) && (
                            <button 
                              onClick={() => { setEditingId(item.id || item.materialId); setEditName(item.name || item.materialName); }}
                              className="hover:text-primary transition-colors p-1" title="Chỉnh sửa tên"
                            >
                              <span className="material-symbols-outlined text-[18px]">edit</span>
                            </button>
                          )}
                          {isActive ? (
                            <button onClick={() => handleDeactivate(item.id || item.materialId)} className="hover:text-error transition-colors p-1" title="Ngừng hoạt động">
                              <span className="material-symbols-outlined text-[18px]">block</span>
                            </button>
                          ) : (
                            <button onClick={() => handleActivate(item.id || item.materialId)} className="hover:text-[#10B981] transition-colors p-1" title="Kích hoạt lại">
                              <span className="material-symbols-outlined text-[18px]">check_circle</span>
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Upload Modal */}
      {showUploadModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div className="absolute inset-0 bg-on-surface/40 backdrop-blur-sm" onClick={() => setShowUploadModal(false)}></div>
          <div className="relative bg-pure-surface w-full max-w-lg rounded-xl shadow-lg border border-outline-variant flex flex-col m-4">
            <div className="px-6 py-4 border-b border-whisper-border flex justify-between items-center">
              <h3 className="text-[20px] font-semibold text-on-surface">Tải lên tài liệu mới</h3>
              <button className="text-on-surface-variant hover:text-on-surface transition-colors" onClick={() => setShowUploadModal(false)}>
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>
            
            <div className="p-6 flex flex-col gap-6">
              <div 
                className="border-2 border-dashed border-outline-variant rounded-xl p-8 flex flex-col items-center justify-center text-center bg-surface-container-lowest hover:bg-surface-container-low transition-colors cursor-pointer group"
                onClick={() => fileInputRef.current?.click()}
              >
                <span className="material-symbols-outlined text-4xl text-primary/70 mb-3 group-hover:text-primary transition-colors">cloud_upload</span>
                {file ? (
                  <p className="text-[16px] font-medium text-on-surface mb-1">{file.name}</p>
                ) : (
                  <>
                    <p className="text-[16px] font-medium text-on-surface mb-1">Kéo thả tệp vào đây hoặc nhấn để chọn</p>
                    <p className="text-[13px] text-on-surface-variant">Định dạng hỗ trợ: PDF, MP4, DOCX — Tối đa 500MB</p>
                  </>
                )}
                <input type="file" className="hidden" ref={fileInputRef} onChange={handleFileChange} />
              </div>

              {uploading && (
                <div className="space-y-2">
                  <div className="flex justify-between items-center text-[13px]">
                    <span className="text-on-surface font-medium">{file?.name}</span>
                    <span className="text-primary font-mono">Đang tải...</span>
                  </div>
                  <div className="h-1.5 w-full bg-surface-variant rounded-full overflow-hidden">
                    <div className="h-full bg-primary rounded-full w-[45%] animate-pulse"></div>
                  </div>
                </div>
              )}

              <div className="space-y-1.5">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="doc-name">Tên tài liệu <span className="text-error">*</span></label>
                <input 
                  className="w-full px-3 py-2 bg-pure-surface border border-outline-variant rounded-lg text-[14px] text-on-surface focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary placeholder:text-outline transition-shadow" 
                  id="doc-name" 
                  placeholder="VD: Bài tập chương 3" 
                  type="text"
                  value={docName}
                  onChange={(e) => setDocName(e.target.value)}
                />
              </div>
            </div>

            <div className="px-6 py-4 bg-surface-container-low border-t border-whisper-border flex justify-end gap-3 rounded-b-xl">
              <button 
                className="px-4 py-2 border border-outline-variant rounded-lg text-[16px] font-medium text-on-surface hover:bg-surface-variant transition-colors"
                onClick={() => {
                  setShowUploadModal(false);
                  setFile(null);
                  setDocName("");
                  setUploading(false);
                }}
              >
                Hủy
              </button>
              <button 
                className="px-4 py-2 bg-primary rounded-lg text-[16px] font-medium text-on-primary hover:opacity-90 transition-opacity disabled:opacity-50"
                onClick={handleUpload}
                disabled={!file || !docName || uploading}
              >
                Tải lên
              </button>
            </div>
          </div>
        </div>
      )}
    </TeacherLayout>
  );
}
