import * as React from "react";
import { useState, useEffect, useRef } from "react";
import { useNavigate, useParams } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import { createLecture, getLecture, updateLecture, getTopics, getMaterials, attachMaterial, publishLecture, uploadLectureThumbnail } from "../../services/learningApi";
import LatexPreview from "../../components/expert/LatexPreview";

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
  const [availableMaterials, setAvailableMaterials] = useState([]);
  const [selectedGrade, setSelectedGrade] = useState("12");
  const [thumbnailPreview, setThumbnailPreview] = useState(null);
  const [isMathHelperOpen, setIsMathHelperOpen] = useState(false);
  const contentTextareaRef = useRef(null);

  const handleInsertLatex = (latex) => {
    const textarea = contentTextareaRef.current;
    if (textarea) {
      const startPos = textarea.selectionStart;
      const endPos = textarea.selectionEnd;
      const text = form.content;
      const newText = text.substring(0, startPos) + latex + text.substring(endPos, text.length);
      setForm(prev => ({ ...prev, content: newText }));
      setTimeout(() => {
        textarea.focus();
        textarea.setSelectionRange(startPos + latex.length, startPos + latex.length);
      }, 0);
    } else {
      setForm(prev => ({ ...prev, content: prev.content + " " + latex }));
    }
  };

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
    // Load available materials
    getMaterials({ pageSize: 100 })
      .then(res => setAvailableMaterials(res.data?.items || res.data || []))
      .catch(err => console.error("Lỗi tải tài liệu:", err));

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

  const toggleMaterial = (matId) => {
    setForm(prev => {
      const isSelected = prev.materialIds.includes(matId);
      if (isSelected) {
        return { ...prev, materialIds: prev.materialIds.filter(id => id !== matId) };
      } else {
        return { ...prev, materialIds: [...prev.materialIds, matId] };
      }
    });
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
                  <div className="bg-surface-container-low border-b border-outline-variant px-3 py-2 flex items-center gap-2">
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
                    <button
                      type="button"
                      onClick={() => setIsMathHelperOpen(!isMathHelperOpen)}
                      className={`px-2 py-1 rounded text-xs font-bold transition-all duration-150 flex items-center gap-1 cursor-pointer active:scale-[0.97] ${
                        isMathHelperOpen
                          ? "bg-primary text-white shadow-sm scale-[1.01]"
                          : "hover:bg-surface-container hover:-translate-y-0.5 text-primary bg-primary/5"
                      }`}
                      title="Mở công cụ chèn công thức LaTeX"
                    >
                      <span className="material-symbols-outlined text-[16px]">calculate</span>
                      Mã toán
                    </button>
                  </div>

                  {/* Math Helper Panel */}
                  {isMathHelperOpen && (
                    <div className="bg-surface-container-lowest border-b border-outline-variant p-4 space-y-4 mi-panel-down">
                      <div>
                        <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Mã toán nhanh:</h5>
                        <div className="flex flex-wrap gap-2">
                          {[
                            { label: "Tích phân", code: "\\int_{a}^{b} f(x) dx" },
                            { label: "Nguyên hàm", code: "\\int f(x) dx" },
                            { label: "Đạo hàm", code: "f'(x) = \\lim_{\\Delta x \\to 0} \\frac{\\Delta y}{\\Delta x}" },
                            { label: "Giới hạn", code: "\\lim_{x \\to x_0} f(x)" },
                            { label: "Phân số", code: "\\frac{a}{b}" },
                            { label: "Căn thức", code: "\\sqrt{x^2 + 1}" }
                          ].map((sym, idx) => (
                            <button
                              key={idx}
                              type="button"
                              onClick={() => handleInsertLatex(`$${sym.code}$`)}
                              className="flex items-center gap-2 bg-pure-surface hover:bg-surface-container border border-whisper-border px-2.5 py-1.5 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer"
                            >
                              <div className="scale-90 select-none">
                                <LatexPreview content={`$${sym.code}$`} />
                              </div>
                              <span className="text-[10px] text-on-surface-variant font-mono bg-surface-container-low px-1.5 py-0.5 rounded">
                                {sym.code}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>

                      <div>
                        <h5 className="text-[10px] font-black text-on-surface-variant mb-2 uppercase tracking-wider">Ký tự Hy Lạp:</h5>
                        <div className="flex flex-wrap gap-1.5">
                          {["\\alpha", "\\beta", "\\gamma", "\\delta", "\\theta", "\\lambda", "\\pi", "\\omega", "\\Delta"].map((sym, idx) => (
                            <button
                              key={idx}
                              type="button"
                              onClick={() => handleInsertLatex(`$${sym}$`)}
                              className="flex flex-col items-center justify-center bg-pure-surface hover:bg-surface-container border border-whisper-border min-w-[50px] py-1 rounded-lg text-xs transition-all duration-150 active:scale-[0.96] cursor-pointer text-center"
                            >
                              <div className="scale-100 select-none h-6 flex items-center justify-center">
                                <LatexPreview content={`$${sym}$`} />
                              </div>
                              <span className="text-[9px] text-on-surface-variant font-mono mt-0.5">
                                {sym}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>
                    </div>
                  )}
                  <textarea
                    ref={contentTextareaRef}
                    className="w-full min-h-[200px] bg-pure-surface px-4 py-3 text-[14px] text-on-surface placeholder:text-outline border-none focus:ring-0 resize-y"
                    id="content"
                    placeholder="Nhập nội dung chi tiết... Hỗ trợ mã LaTeX bọc trong dấu $ hoặc $$"
                    value={form.content}
                    onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))}
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
                <label className="block text-[16px] font-medium text-on-surface">
                  Đính kèm Tài liệu <span className="text-[13px] text-on-surface-variant font-normal">({form.materialIds.length} đã chọn)</span>
                </label>
                <div className="max-h-48 overflow-y-auto border border-outline-variant rounded-lg bg-pure-surface p-2 space-y-1">
                  {availableMaterials.length === 0 ? (
                    <p className="p-3 text-[14px] text-on-surface-variant text-center">Chưa có tài liệu nào. Hãy upload tài liệu trước.</p>
                  ) : (
                    availableMaterials.map((mat) => (
                      <label key={mat.materialId || mat.id} className="flex items-center gap-3 p-2 hover:bg-surface-container-lowest rounded cursor-pointer transition-colors">
                        <input
                          type="checkbox"
                          className="w-4 h-4 text-primary border-outline-variant rounded focus:ring-primary"
                          checked={form.materialIds.includes(mat.materialId || mat.id)}
                          onChange={() => toggleMaterial(mat.materialId || mat.id)}
                        />
                        <span className="text-[14px] text-on-surface flex-1 truncate">{mat.materialName || mat.name}</span>
                        <span className="text-[12px] text-on-surface-variant px-2 py-0.5 bg-surface-variant rounded">{(mat.fileType || mat.format || "FILE").toUpperCase()}</span>
                      </label>
                    ))
                  )}
                </div>
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
