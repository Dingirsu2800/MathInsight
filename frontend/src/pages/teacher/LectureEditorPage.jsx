import * as React from "react";
import { useState, useEffect } from "react";
import { useNavigate, useParams } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import { createLecture, getLecture, updateLecture, getTopics } from "../../services/learningApi";

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
  });
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [topics, setTopics] = useState([]);
  const [thumbnailPreview, setThumbnailPreview] = useState(null);

  useEffect(() => {
    getTopics()
      .then((res) => setTopics(res.data || []))
      .catch(() => setTopics([
        { tagId: "1", tagName: "Đại số > Phương trình > Phương trình bậc 2" },
        { tagId: "2", tagName: "Hình học > Không gian" },
        { tagId: "3", tagName: "Giải tích > Đạo hàm" },
      ]));
  }, []);

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
        });
      })
      .catch(() => {});
  }, [id, isEdit]);

  const validate = () => {
    const e = {};
    if (!form.title.trim()) e.title = "Vui lòng nhập tiêu đề";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!validate()) return;
    setSaving(true);
    try {
      if (isEdit) {
        await updateLecture(id, form);
      } else {
        await createLecture(form);
      }
      navigate("/teacher/lectures");
    } catch (err) {
      console.error("Lưu bài giảng thất bại:", err);
    } finally {
      setSaving(false);
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

              {/* Topic */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="topic">
                  Chủ đề <span className="text-error">*</span>
                </label>
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
                  <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center px-3 text-on-surface-variant">
                    <span className="material-symbols-outlined">expand_more</span>
                  </div>
                </div>
              </div>

              {/* Content */}
              <div className="space-y-2">
                <label className="block text-[16px] font-medium text-on-surface" htmlFor="content">
                  Nội dung bài giảng
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
                  </div>
                  <textarea
                    className="w-full min-h-[200px] bg-pure-surface px-4 py-3 text-[14px] text-on-surface placeholder:text-outline border-none focus:ring-0 resize-y"
                    id="content"
                    placeholder="Nhập nội dung chi tiết..."
                    value={form.content}
                    onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))}
                  />
                </div>
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
          <div className="mt-8 flex items-center justify-end gap-4">
            <button
              onClick={() => navigate("/teacher/lectures")}
              className="px-6 py-2.5 rounded-lg border border-outline-variant bg-pure-surface text-on-surface text-[16px] font-medium hover:bg-surface-container-low transition-all"
              type="button"
            >
              Hủy bỏ
            </button>
            <button
              onClick={handleSubmit}
              disabled={saving}
              className="px-6 py-2.5 rounded-lg bg-primary text-on-primary text-[16px] font-medium hover:opacity-90 transition-all shadow-sm disabled:opacity-50"
              type="submit"
            >
              {saving ? "Đang lưu..." : "Lưu nháp"}
            </button>
          </div>
        </div>
      </div>
    </TeacherLayout>
  );
}
