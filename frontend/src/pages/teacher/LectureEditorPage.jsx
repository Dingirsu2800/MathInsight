import * as React from "react";
import { useState, useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import { createLecture, getLecture, updateLecture, getTopics, attachMaterial, publishLecture, uploadLectureThumbnail, uploadMaterial } from "../../services/learningApi";
import LatexPreview from "../../components/expert/LatexPreview";
import MathTextArea from "../../components/common/MathTextArea";

export default function LectureEditorPage() {
  const navigate = useNavigate();
  const { id } = useParams();
  const isEdit = Boolean(id);

  const [form, setForm] = useState({
    title: "",
    tagId: "",
    content: "",
    videoUrl: "",
    thumbnailFile: null,
    thumbnailUrl: "",
    materialIds: [],
  });
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [topics, setTopics] = useState([]);
  const [attachedMaterials, setAttachedMaterials] = useState([]);
  const [isUploadingMaterial, setIsUploadingMaterial] = useState(false);
  const [isExtractingOcr, setIsExtractingOcr] = useState(false);
  const [selectedGrade, setSelectedGrade] = useState("12");
  const [thumbnailPreview, setThumbnailPreview] = useState(null);
  const materialInputRef = useRef(null);
  const ocrInputRef = useRef(null);

  useEffect(() => {
    const flattenTopics = (nodes, parentPath = "") => {
      let result = [];
      nodes.forEach(node => {
        const currentPath = parentPath ? `${parentPath} > ${node.tagName}` : node.tagName;
        result.push({ tagId: node.tagId, tagName: currentPath });
        if (node.children && node.children.length > 0) {
          result = result.concat(flattenTopics(node.children, currentPath));
        }
      });
      return result;
    };

    getTopics(selectedGrade)
      .then((res) => {
        const rawTopics = res.data || [];
        setTopics(flattenTopics(rawTopics));
      })
      .catch((err) => {
        console.error("Lỗi khi tải danh sách chủ đề:", err);
        setTopics([]);
      });
  }, [selectedGrade]);

  useEffect(() => {
    if (!isEdit) return;
    getLecture(id)
      .then((res) => {
        const data = res.data;
        setForm({
          title: data.title || "",
          tagId: data.tagId || "",
          content: data.content || "",
          videoUrl: data.videoUrl || "",
          thumbnailFile: null,
          thumbnailUrl: data.thumbnailUrl || "",
          materialIds: (data.materials || []).map(m => m.id || m.materialId)
        });
        setAttachedMaterials(data.materials || []);
        if (data.thumbnailUrl) {
          setThumbnailPreview(data.thumbnailUrl);
        }
      })
      .catch(() => {});
  }, [id, isEdit]);

  const validate = () => {
    const e = {};
    if (!form.title.trim()) e.title = "Vui lòng nhập tiêu đề";
    if (!form.tagId) e.tagId = "Vui lòng chọn chủ đề";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (e, isPublish = false) => {
    if (e) e.preventDefault();
    if (!validate()) return;
    
    if (isPublish) setPublishing(true);
    else setSaving(true);

    try {
      let currentLectureId = id;
      let finalForm = { ...form };

      if (form.thumbnailFile) {
        const formData = new FormData();
        formData.append("file", form.thumbnailFile);
        const uploadRes = await uploadLectureThumbnail(formData);
        finalForm.thumbnailUrl = uploadRes.data.url;
      }

      if (isEdit) {
        await updateLecture(currentLectureId, finalForm);
      } else {
        const res = await createLecture(finalForm);
        currentLectureId = res.data?.id || res.data?.lectureId;
      }

      if (isPublish && currentLectureId) {
        await publishLecture(currentLectureId);
        alert("Xuất bản bài giảng thành công!");
      } else {
        alert(isEdit ? "Cập nhật bài giảng thành công!" : "Tạo bài giảng thành công!");
      }

      navigate("/teacher/lectures");
    } catch (err) {
      console.error("Lưu bài giảng thất bại:", err);
      alert("Đã xảy ra lỗi khi lưu bài giảng!");
    } finally {
      setSaving(false);
      setPublishing(false);
    }
  };

  const handleUploadMaterial = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate Size (500MB)
    if (file.size > 500 * 1024 * 1024) {
      alert("Kích thước tệp vượt quá 500MB!");
      if (materialInputRef.current) materialInputRef.current.value = '';
      return;
    }
    // Validate Format
    const ext = file.name.split('.').pop().toLowerCase();
    if (!['pdf', 'mp4', 'docx'].includes(ext)) {
      alert("Chỉ hỗ trợ tệp định dạng PDF, MP4, DOCX!");
      if (materialInputRef.current) materialInputRef.current.value = '';
      return;
    }

    setIsUploadingMaterial(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("materialName", file.name.split('.')[0]);

      const res = await uploadMaterial(formData);
      const newMaterial = res.data;
      const newMaterialId = newMaterial.materialId || newMaterial.id;
      
      setAttachedMaterials(prev => [...prev, newMaterial]);
      setForm(prev => ({
        ...prev,
        materialIds: [...prev.materialIds, newMaterialId]
      }));
      
    } catch (err) {
      console.error("Upload material failed:", err);
      alert("Tải lên tài liệu thất bại: " + (err.response?.data?.message || err.message));
    } finally {
      setIsUploadingMaterial(false);
      if (materialInputRef.current) materialInputRef.current.value = '';
    }
  };

  const removeAttachedMaterial = (matId) => {
    setAttachedMaterials(prev => prev.filter(m => (m.materialId || m.id) !== matId));
    setForm(prev => ({
      ...prev,
      materialIds: prev.materialIds.filter(id => id !== matId)
    }));
  };

  const handleExtractOcr = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 20 * 1024 * 1024) {
      alert("Kích thước tệp vượt quá 20MB!");
      if (ocrInputRef.current) ocrInputRef.current.value = '';
      return;
    }

    if (!file.type.startsWith('image/') && file.type !== 'application/pdf') {
      alert("Chỉ hỗ trợ file PDF hoặc ảnh (JPG, PNG)!");
      if (ocrInputRef.current) ocrInputRef.current.value = '';
      return;
    }

    setIsExtractingOcr(true);
    try {
      const formData = new FormData();
      formData.append("file", file);

      const { extractLectureOcr } = await import('../../services/learningApi');
      const res = await extractLectureOcr(formData);
      
      const newText = res.data.markdown;
      setForm(prev => ({
        ...prev,
        content: prev.content ? prev.content + "\n\n" + newText : newText
      }));
      alert("Đã nhận diện thành công nội dung từ ảnh!");
    } catch (err) {
      console.error("Lỗi OCR ảnh", err);
      alert(err.response?.data?.message || "Đã xảy ra lỗi khi quét ảnh OCR");
    } finally {
      setIsExtractingOcr(false);
      if (ocrInputRef.current) ocrInputRef.current.value = '';
    }
  };

  const handleFileSelect = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      setForm((f) => ({ ...f, thumbnailFile: file }));
      setThumbnailPreview(URL.createObjectURL(file));
    }
  };

  const fieldClass = (name) =>
    `w-full bg-pure-surface border ${errors[name] ? "border-error" : "border-outline-variant"} rounded-lg px-4 py-3 text-[14px] text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 ${errors[name] ? "focus:ring-error/20 focus:border-error" : "focus:ring-primary/20 focus:border-primary"} transition-all`;

  return (
    <TeacherLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        {/* Breadcrumb */}
        <nav aria-label="Breadcrumb" className="flex items-center text-on-surface-variant text-[13px]">
          <ol className="inline-flex items-center space-x-1 md:space-x-3">
            <li className="inline-flex items-center">
              <button onClick={() => navigate("/teacher/lectures")} className="hover:text-primary transition-colors">
                Bài giảng
              </button>
            </li>
            <li>
              <div className="flex items-center">
                <span className="material-symbols-outlined text-[16px] mx-1">chevron_right</span>
                <span className="text-on-surface font-medium">{isEdit ? "Chỉnh sửa" : "Tạo mới"}</span>
              </div>
            </li>
          </ol>
        </nav>

        <div className="mb-2">
          <h2 className="text-[32px] font-semibold leading-[40px] tracking-[-0.02em] text-on-surface">
            {isEdit ? "Chỉnh sửa bài giảng" : "Tạo bài giảng mới"}
          </h2>
          <p className="text-[14px] text-on-surface-variant mt-1">
            Điền thông tin chi tiết cho bài giảng của bạn.
          </p>
        </div>

        {/* Form Card */}
        <div className="max-w-[720px] mx-auto w-full">
          <div className="bg-pure-surface border border-whisper-border rounded-xl p-8 shadow-sm">
            <form className="space-y-6" onSubmit={handleSubmit}>
              {/* Title */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="title">
                  Tiêu đề bài giảng <span className="text-error">*</span>
                </label>
                <input
                  className={fieldClass("title")}
                  id="title"
                  placeholder="VD: Ôn tập phương trình bậc 2"
                  type="text"
                  value={form.title}
                  onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                />
                {errors.title && (
                  <p className="text-[13px] text-error mt-1 flex items-center gap-1">
                    <span className="material-symbols-outlined text-[16px]">error</span>
                    {errors.title}
                  </p>
                )}
              </div>

              {/* Classification Info */}
              <div className="bg-surface-container-lowest p-5 rounded-xl border border-outline-variant space-y-6 shadow-inner">
                <h3 className="text-xs font-black uppercase text-primary tracking-wider border-b border-whisper-border pb-2.5 flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[16px]">label</span>
                  Thông tin phân loại
                </h3>

                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
                  <div className="space-y-4">
                    {/* Grade Select */}
                    <div>
                      <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Khối lớp học</label>
                      <div className="relative">
                        <select
                          className={`appearance-none ${fieldClass("")} pr-10`}
                          value={selectedGrade}
                          onChange={(e) => {
                            setSelectedGrade(e.target.value);
                            setForm((f) => ({ ...f, tagId: "" }));
                          }}
                        >
                          <option value="10">Lớp 10</option>
                          <option value="11">Lớp 11</option>
                          <option value="12">Lớp 12</option>
                        </select>
                        <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-on-surface-variant">
                          <span className="material-symbols-outlined">expand_more</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <div className="space-y-4">
                    {/* Topic Select */}
                    <div>
                      <label className="block text-[10px] font-bold text-on-surface-variant uppercase tracking-wider mb-1.5">Chủ đề bài giảng <span className="text-error">*</span></label>
                      <div className="relative">
                        <select
                          className={`appearance-none ${fieldClass("tagId")} pr-10`}
                          id="topic"
                          value={form.tagId}
                          onChange={(e) => setForm((f) => ({ ...f, tagId: e.target.value }))}
                        >
                          <option value="">Chọn chủ đề</option>
                          {topics.map((t) => (
                            <option key={t.tagId} value={t.tagId}>{t.tagName}</option>
                          ))}
                        </select>
                        {errors.tagId && (
                          <p className="text-[13px] text-error mt-1 flex items-center gap-1">
                            <span className="material-symbols-outlined text-[16px]">error</span>
                            {errors.tagId}
                          </p>
                        )}
                        <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-on-surface-variant">
                          <span className="material-symbols-outlined">expand_more</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Content */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="content">
                  Nội dung bài giảng (Hỗ trợ LaTeX)
                </label>
                <div className="border border-outline-variant rounded-lg overflow-hidden bg-pure-surface focus-within:ring-2 focus-within:ring-primary/20 focus-within:border-primary transition-all">
                  {/* Toolbar */}
                  <div className="bg-surface-container-low border-b border-outline-variant px-3 py-2 flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {["format_bold", "format_italic", "format_underlined"].map((icon) => (
                        <button key={icon} type="button" className="p-1 rounded text-on-surface-variant hover:bg-surface-variant hover:text-on-surface transition-colors">
                          <span className="material-symbols-outlined text-[18px]">{icon}</span>
                        </button>
                      ))}
                      <div className="w-[1px] h-4 bg-outline-variant mx-1" />
                      {["format_list_bulleted", "format_list_numbered"].map((icon) => (
                        <button key={icon} type="button" className="p-1 rounded text-on-surface-variant hover:bg-surface-variant hover:text-on-surface transition-colors">
                          <span className="material-symbols-outlined text-[18px]">{icon}</span>
                        </button>
                      ))}
                      <div className="w-px h-6 bg-outline-variant mx-1 self-center"></div>
                    </div>
                    
                    <div>
                      <input
                        type="file"
                        accept=".pdf, image/png, image/jpeg, image/jpg"
                        className="hidden"
                        ref={ocrInputRef}
                        onChange={handleExtractOcr}
                      />
                      <button
                        type="button"
                        onClick={() => ocrInputRef.current?.click()}
                        disabled={isExtractingOcr}
                        className="flex items-center gap-1.5 px-3 py-1 bg-primary text-white rounded-md text-[13px] font-medium transition-colors hover:opacity-90 disabled:opacity-50"
                      >
                        <span className="material-symbols-outlined text-[16px]">
                          {isExtractingOcr ? "sync" : "document_scanner"}
                        </span>
                        {isExtractingOcr ? "Đang xử lý..." : "Nhập PDF/Ảnh (OCR)"}
                      </button>
                    </div>
                  </div>
                  
                  <MathTextArea
                    value={form.content}
                    onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))}
                    minHeight="min-h-[200px]"
                    className="border-none rounded-none focus-within:ring-0 focus-within:border-none"
                  />
                </div>
                {/* Preview Box */}
                {form.content && (
                  <div className="mt-4 p-4 border border-whisper-border rounded-lg bg-surface-container-lowest">
                    <h4 className="text-[12px] font-bold text-on-surface-variant uppercase tracking-wider mb-2">Xem trước nội dung:</h4>
                    <LatexPreview content={form.content} />
                  </div>
                )}
              </div>

              {/* Video URL */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="video">
                  Đường dẫn Video (YouTube/Vimeo)
                </label>
                <div className="relative">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-on-surface-variant">
                    <span className="material-symbols-outlined text-[20px]">link</span>
                  </div>
                  <input
                    className={`${fieldClass("")} pl-10`}
                    id="video"
                    placeholder="https://youtube.com/..."
                    type="url"
                    value={form.videoUrl}
                    onChange={(e) => setForm((f) => ({ ...f, videoUrl: e.target.value }))}
                  />
                </div>
              </div>

              {/* Attach Materials */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <label className="block text-[16px] font-medium text-on-surface">
                    Đính kèm Tài liệu <span className="text-[13px] text-on-surface-variant font-normal">({attachedMaterials.length} file)</span>
                  </label>
                  <button
                    type="button"
                    onClick={() => materialInputRef.current?.click()}
                    disabled={isUploadingMaterial}
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-primary/10 text-primary hover:bg-primary/20 rounded-lg text-[13px] font-medium transition-colors disabled:opacity-50"
                  >
                    <span className="material-symbols-outlined text-[16px]">
                      {isUploadingMaterial ? "sync" : "upload"}
                    </span>
                    {isUploadingMaterial ? "Đang tải lên..." : "Tải lên tài liệu mới"}
                  </button>
                  <input 
                    type="file" 
                    ref={materialInputRef} 
                    className="hidden" 
                    accept=".pdf,.mp4,.docx"
                    onChange={handleUploadMaterial}
                  />
                </div>
                
                <div className="min-h-[100px] border border-outline-variant rounded-lg bg-surface-container-lowest p-3 space-y-2">
                  {attachedMaterials.length === 0 ? (
                    <div className="flex flex-col items-center justify-center h-24 text-on-surface-variant">
                      <span className="material-symbols-outlined text-[24px] mb-1 opacity-50">folder_open</span>
                      <p className="text-[13px]">Chưa có tài liệu đính kèm.</p>
                    </div>
                  ) : (
                    attachedMaterials.map((mat) => {
                      const id = mat.materialId || mat.id;
                      return (
                        <div key={id} className="flex items-center justify-between gap-3 p-2.5 bg-pure-surface border border-whisper-border hover:border-outline-variant rounded-lg transition-colors group">
                          <div className="flex items-center gap-3 overflow-hidden">
                            <span className="material-symbols-outlined text-[#3b82f6] text-[20px]">
                              {(mat.fileType || mat.format || "").toUpperCase().includes("PDF") ? "picture_as_pdf" : 
                               (mat.fileType || mat.format || "").toUpperCase().includes("MP4") ? "movie" : "description"}
                            </span>
                            <span className="text-[14px] font-medium text-on-surface truncate">{mat.materialName || mat.name}</span>
                          </div>
                          <button
                            type="button"
                            onClick={() => removeAttachedMaterial(id)}
                            className="text-error hover:bg-error/10 p-1.5 rounded transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100 flex items-center justify-center"
                            title="Xóa tài liệu đính kèm"
                          >
                            <span className="material-symbols-outlined text-[18px]">close</span>
                          </button>
                        </div>
                      );
                    })
                  )}
                </div>
                <p className="text-[12px] text-on-surface-variant flex items-center gap-1 mt-1">
                  <span className="material-symbols-outlined text-[14px]">info</span>
                  Tài liệu tải lên ở đây sẽ tự động được gán vào bài giảng và xuất hiện trong Kho tài liệu.
                </p>
              </div>

              {/* Thumbnail Upload */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface">
                  Ảnh đại diện
                </label>
                <div
                  className="w-full border-2 border-dashed border-outline-variant rounded-xl p-8 flex flex-col items-center justify-center text-center cursor-pointer hover:bg-surface-container-low transition-colors group"
                  onClick={() => document.getElementById("thumbnail-input").click()}
                >
                  {thumbnailPreview ? (
                    <img src={thumbnailPreview} alt="Preview" className="max-h-32 rounded-lg mb-2 object-cover" />
                  ) : (
                    <span className="material-symbols-outlined text-[40px] text-on-surface-variant mb-4 group-hover:text-primary transition-colors" style={{ fontVariationSettings: "'FILL' 1" }}>
                      cloud_upload
                    </span>
                  )}
                  <h4 className="text-[16px] font-medium text-on-surface mb-1">Kéo thả hoặc nhấn để tải ảnh lên</h4>
                  <p className="text-[13px] text-on-surface-variant">PNG, JPG tối đa 5MB</p>
                  <input id="thumbnail-input" accept="image/png, image/jpeg" className="hidden" type="file" onChange={handleFileSelect} />
                </div>
              </div>
            </form>
          </div>

          {/* Bottom Actions */}
          <div className="mt-8 flex items-center justify-between">
            <button
              onClick={() => navigate("/teacher/lectures")}
              className="px-6 py-2.5 rounded-lg border border-outline-variant bg-pure-surface text-on-surface text-[16px] font-medium hover:bg-surface-container-low transition-all"
              type="button"
            >
              Hủy bỏ
            </button>
            <div className="flex gap-4">
              <button
                onClick={(e) => handleSubmit(e, false)}
                disabled={saving || publishing}
                className="px-6 py-2.5 rounded-lg border border-primary text-primary bg-pure-surface text-[16px] font-medium hover:bg-primary-container/20 transition-all shadow-sm disabled:opacity-50"
                type="button"
              >
                {saving ? "Đang lưu..." : "Lưu nháp"}
              </button>
              <button
                onClick={(e) => handleSubmit(e, true)}
                disabled={saving || publishing}
                className="px-6 py-2.5 rounded-lg bg-primary text-on-primary text-[16px] font-medium hover:opacity-90 transition-all shadow-sm flex items-center gap-2 disabled:opacity-50"
                type="button"
              >
                <span className="material-symbols-outlined text-[20px]">publish</span>
                {publishing ? "Đang xuất bản..." : "Xuất bản"}
              </button>
            </div>
          </div>
        </div>
      </div>
    </TeacherLayout>
  );
}
