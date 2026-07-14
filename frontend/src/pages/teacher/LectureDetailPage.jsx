import * as React from "react";
import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import { getLecture } from "../../services/learningApi";

export default function LectureDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [lecture, setLecture] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getLecture(id)
      .then((res) => setLecture(res.data))
      .catch((err) => {
        console.error("Lỗi khi tải chi tiết bài giảng:", err);
        setLecture(null);
      })
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <TeacherLayout>
        <div className="p-gutter flex justify-center items-center h-64 text-on-surface-variant">Đang tải...</div>
      </TeacherLayout>
    );
  }

  if (!lecture) {
    return (
      <TeacherLayout>
        <div className="p-gutter text-center text-error mt-8">Không tìm thấy bài giảng</div>
      </TeacherLayout>
    );
  }

  return (
    <TeacherLayout>
      <div className="p-gutter max-w-[960px] mx-auto w-full pb-24">
        {/* Back Link */}
        <button 
          onClick={() => navigate("/teacher/lectures")}
          className="inline-flex items-center gap-2 text-primary hover:text-primary-container font-medium text-[16px] mb-6 transition-colors"
        >
          <span className="material-symbols-outlined text-sm">arrow_back</span>
          Quay lại danh sách
        </button>

        {/* Lecture Card */}
        <article className="bg-pure-surface rounded-xl border border-whisper-border shadow-sm overflow-hidden mb-10">
          {/* Video Placeholder */}
          <div className="relative aspect-video bg-gradient-to-br from-primary-container to-secondary-container group cursor-pointer">
            <div className="absolute inset-0 bg-black/20 group-hover:bg-black/10 transition-colors"></div>
            <button className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-16 h-16 bg-pure-surface/90 rounded-full flex items-center justify-center text-primary shadow-lg group-hover:scale-110 transition-transform">
              <span className="material-symbols-outlined text-3xl ml-1" style={{ fontVariationSettings: "'FILL' 1" }}>play_arrow</span>
            </button>
            {/* Progress Bar */}
            <div className="absolute bottom-0 left-0 right-0 h-1 bg-surface-variant">
              <div className="h-full bg-primary w-[45%]"></div>
            </div>
          </div>

          <div className="p-8">
            <div className="flex items-start justify-between gap-4 mb-4">
              <div>
                <span className="inline-block px-3 py-1 bg-primary-container/20 text-primary text-[12px] font-semibold uppercase tracking-wider rounded-full mb-3">
                  {lecture.tagName || "Chủ đề"}
                </span>
                <h1 className="text-[32px] font-semibold leading-[40px] tracking-[-0.02em] text-on-surface mb-2">{lecture.title}</h1>
              </div>
              <div className="flex items-center gap-2">
                <button className="flex items-center gap-2 px-4 py-2 bg-error-container/20 text-error rounded-lg hover:bg-error-container/30 transition-colors font-medium text-[16px]">
                  <span className="material-symbols-outlined text-error" style={{ fontVariationSettings: "'FILL' 1" }}>favorite</span>
                  {lecture.likes || 0}
                </button>
              </div>
            </div>

            <div className="flex items-center gap-6 mb-8 text-[13px] text-on-surface-variant pb-6 border-b border-whisper-border">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-surface-variant flex items-center justify-center text-on-surface-variant font-bold">
                  {lecture.teacherName?.[0] || "GV"}
                </div>
                <div>
                  <p className="text-[16px] font-medium text-on-surface">Giáo viên: {lecture.teacherName || "MathInsight"}</p>
                  <p>Ngày đăng: {new Date(lecture.createdTime).toLocaleDateString("vi-VN")}</p>
                </div>
              </div>
              <div className="flex gap-4 ml-auto">
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-sm">visibility</span> 1,204</span>
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-sm">chat_bubble</span> {lecture.discussions?.length || 0}</span>
              </div>
            </div>

            <div className="prose max-w-none text-[14px] text-on-surface leading-relaxed whitespace-pre-wrap">
              {lecture.content}
            </div>
          </div>
        </article>

        {/* Materials Section */}
        {lecture.materials && lecture.materials.length > 0 && (
          <section className="mb-12">
            <h2 className="text-[20px] font-semibold text-on-surface mb-6 flex items-center gap-2">
              <span className="material-symbols-outlined">attach_file</span>
              Tài liệu đính kèm ({lecture.materials.length})
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-gutter">
              {lecture.materials.map((mat) => {
                let icon = "insert_drive_file";
                let colorCls = "text-on-surface-variant bg-surface-variant";
                const fmt = mat.format?.toUpperCase() || "";
                if (fmt.includes("PDF")) { icon = "picture_as_pdf"; colorCls = "text-error bg-error-container/20"; }
                else if (fmt.includes("DOC")) { icon = "description"; colorCls = "text-primary bg-primary-container/20"; }
                else if (fmt.includes("MP4")) { icon = "movie"; colorCls = "text-tertiary bg-tertiary-container/20"; }

                return (
                  <div key={mat.id} className="bg-pure-surface rounded-lg border border-whisper-border p-4 flex flex-col hover:border-primary/50 transition-colors">
                    <div className="flex items-start gap-3 mb-4">
                      <div className={`w-10 h-10 rounded flex items-center justify-center ${colorCls}`}>
                        <span className="material-symbols-outlined">{icon}</span>
                      </div>
                      <div className="flex-1 min-w-0">
                        <h3 className="text-[16px] font-medium text-on-surface truncate">{mat.name}</h3>
                        <p className="text-[13px] text-on-surface-variant">{mat.size || "1.0 MB"}</p>
                      </div>
                    </div>
                    <button className="mt-auto w-full py-2 bg-pure-surface text-primary border border-whisper-border rounded hover:bg-surface-container-low transition-colors font-medium text-[16px] flex items-center justify-center gap-2">
                      <span className="material-symbols-outlined text-sm">{fmt.includes("MP4") ? "visibility" : "download"}</span> 
                      {fmt.includes("MP4") ? "Xem" : "Tải xuống"}
                    </button>
                  </div>
                );
              })}
            </div>
          </section>
        )}

        {/* Discussion Section */}
        {lecture.discussions && (
          <section>
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-[20px] font-semibold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined">forum</span>
                Thảo luận ({lecture.discussions.length} câu hỏi)
              </h2>
            </div>
            
            <div className="space-y-6">
              {lecture.discussions.map((disc) => (
                <div key={disc.id} className="bg-pure-surface rounded-xl border border-whisper-border p-6">
                  {/* Question */}
                  <div className="flex gap-4">
                    <div className="w-10 h-10 rounded-full bg-secondary-container text-on-secondary-container flex items-center justify-center font-medium text-[16px] shrink-0">
                      {disc.authorInitials}
                    </div>
                    <div className="flex-1">
                      <div className="flex justify-between items-start mb-1">
                        <div>
                          <h4 className="text-[16px] font-medium text-on-surface">{disc.author}</h4>
                          <p className="text-[13px] text-on-surface-variant">{disc.timeAgo}</p>
                        </div>
                      </div>
                      <h5 className="text-[16px] font-medium text-on-surface mt-2 mb-1">{disc.title}</h5>
                      <p className="text-[14px] text-on-surface-variant">{disc.content}</p>
                    </div>
                  </div>

                  {/* Answers */}
                  {disc.answers?.map((ans) => (
                    <div key={ans.id} className="mt-4 ml-14 pl-4 border-l-2 border-whisper-border flex gap-4">
                      <div className="w-8 h-8 rounded-full bg-surface-variant flex items-center justify-center font-medium shrink-0">
                        {ans.author?.[0] || "GV"}
                      </div>
                      <div className="flex-1 bg-surface-container-low rounded-lg p-4">
                        <div className="flex justify-between items-start mb-2">
                          <div>
                            <h4 className="text-[16px] font-medium text-on-surface">
                              {ans.author}
                              {ans.role === "Giáo viên" && (
                                <span className="bg-primary-container/20 text-primary text-[10px] px-2 py-0.5 rounded ml-2 uppercase font-semibold">Giáo viên</span>
                              )}
                            </h4>
                            <p className="text-[13px] text-on-surface-variant">{ans.timeAgo}</p>
                          </div>
                        </div>
                        <p className="text-[14px] text-on-surface">{ans.content}</p>
                      </div>
                    </div>
                  ))}
                  
                  {/* Reply Input Box */}
                  <div className="mt-4 ml-14 pl-4 flex gap-4">
                     <input className="w-full bg-pure-surface border border-outline-variant rounded-lg px-4 py-2 text-[14px] focus:ring-primary focus:border-primary" placeholder="Nhập câu trả lời..." />
                     <button className="bg-primary text-on-primary px-4 py-2 rounded-lg text-[14px] font-medium">Gửi</button>
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}
      </div>
    </TeacherLayout>
  );
}
