import * as React from "react";
import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import TeacherLayout from "./TeacherLayout";
import { getLecture, getDiscussions, answerQuestion, hideComment, reportDiscussion, updateComment, deleteComment } from "../../services/learningApi";
import LatexPreview from "../../components/expert/LatexPreview";

export default function LectureDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [lecture, setLecture] = useState(null);
  const [loading, setLoading] = useState(true);
  const [discussions, setDiscussions] = useState([]);
  const [replyContent, setReplyContent] = useState({});
  const [submittingReply, setSubmittingReply] = useState(null);
  const [reportModal, setReportModal] = useState({ isOpen: false, targetId: null, isQuestion: false, reason: "" });
  
  const [editingComment, setEditingComment] = useState(null);
  const [editContent, setEditContent] = useState("");
  const currentAccountId = localStorage.getItem("AccountId");

  const fetchDiscussionsData = async () => {
    try {
      const res = await getDiscussions(id, { page: 1, pageSize: 50 });
      const mappedDiscussions = (res.data || []).map(d => ({
        id: d.discussionQuestionId,
        author: d.studentId || "Học sinh ẩn danh",
        authorInitials: d.studentId ? d.studentId.substring(0, 2).toUpperCase() : "HS",
        timeAgo: new Date(d.createdTime).toLocaleString("vi-VN"),
        title: d.title,
        content: d.content,
        status: d.status,
        answers: (d.answers || []).map(a => ({
          id: a.discussionAnswerId,
          authorId: a.accountId,
          author: a.accountId || "Giáo viên",
          role: "Giáo viên",
          timeAgo: new Date(a.createdTime).toLocaleString("vi-VN"),
          content: a.content,
          status: a.status
        }))
      }));
      setDiscussions(mappedDiscussions);
    } catch (err) {
      console.error("Lỗi tải thảo luận", err);
    }
  };

  useEffect(() => {
    getLecture(id)
      .then((res) => setLecture(res.data))
      .catch((err) => {
        console.error("Lỗi khi tải chi tiết bài giảng:", err);
        setLecture(null);
      })
      .finally(() => setLoading(false));

    fetchDiscussionsData();
  }, [id]);

  const handleSubmitReply = async (questionId) => {
    const content = replyContent[questionId];
    if (!content || content.trim() === "") return;
    setSubmittingReply(questionId);
    try {
      await answerQuestion(questionId, { content });
      setReplyContent(prev => ({ ...prev, [questionId]: "" }));
      await fetchDiscussionsData();
    } catch (err) {
      console.error("Lỗi khi gửi câu trả lời", err);
      alert("Lỗi khi gửi câu trả lời!");
    } finally {
      setSubmittingReply(null);
    }
  };

  const handleHideComment = async (commentId, isQuestion) => {
    if (!window.confirm("Bạn có chắc chắn muốn ẩn bình luận này?")) return;
    try {
      await hideComment(commentId, isQuestion);
      await fetchDiscussionsData();
    } catch (err) {
      console.error("Lỗi khi ẩn bình luận", err);
      alert("Lỗi khi ẩn bình luận!");
    }
  };


  const handleDeleteComment = async (id, isQuestion) => {
    if (!window.confirm("Bạn có chắc chắn muốn xóa bình luận này?")) return;
    try {
      await deleteComment(id, isQuestion);
      await fetchDiscussionsData();
    } catch (err) {
      console.error("Lỗi khi xóa bình luận", err);
      alert("Lỗi khi xóa bình luận!");
    }
  };

  const handleUpdateComment = async (id, isQuestion) => {
    if (!editContent.trim()) return;
    try {
      await updateComment(id, isQuestion, editContent);
      setEditingComment(null);
      setEditContent("");
      await fetchDiscussionsData();
    } catch (err) {
      console.error("Lỗi khi sửa bình luận", err);
      alert("Lỗi khi sửa bình luận!");
    }
  };

  const startEdit = (id, content) => {
    setEditingComment(id);
    setEditContent(content);
  };

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
          {/* Video Player */}
          <div className="relative aspect-video bg-black flex items-center justify-center overflow-hidden group">
            {lecture.videoUrl ? (
              <video src={lecture.videoUrl} controls className="w-full h-full object-cover" poster={lecture.thumbnailUrl} />
            ) : lecture.thumbnailUrl ? (
              <img src={lecture.thumbnailUrl} alt={lecture.title} className="w-full h-full object-cover opacity-60" />
            ) : (
              <div className="absolute inset-0 bg-gradient-to-tr from-surface-variant to-surface-container-high"></div>
            )}
            {!lecture.videoUrl && (
              <button className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-16 h-16 bg-pure-surface/90 rounded-full flex items-center justify-center text-primary shadow-lg group-hover:scale-110 transition-transform">
                <span className="material-symbols-outlined text-3xl ml-1" style={{ fontVariationSettings: "'FILL' 1" }}>play_arrow</span>
              </button>
            )}
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
                <span className="flex items-center gap-1"><span className="material-symbols-outlined text-sm">chat_bubble</span> {discussions.length}</span>
              </div>
            </div>

            <div className="prose max-w-none text-[14px] text-on-surface leading-relaxed whitespace-pre-wrap">
              <LatexPreview content={lecture.content} />
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
        <section>
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-[20px] font-semibold text-on-surface flex items-center gap-2">
              <span className="material-symbols-outlined">forum</span>
              Thảo luận ({discussions.length} câu hỏi)
            </h2>
          </div>
          
          <div className="space-y-6">
            {discussions.map((disc) => (
              <div key={disc.id} className="bg-pure-surface rounded-xl border border-whisper-border p-6 relative group">
                {/* Actions Question */}
                <div className="absolute top-6 right-6 flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity bg-pure-surface px-2 rounded-md shadow-sm border border-whisper-border z-10">
                  <button 
                    onClick={() => handleHideComment(disc.id, true)}
                    className="text-on-surface-variant hover:text-error p-1"
                    title="Ẩn câu hỏi"
                  >
                    <span className="material-symbols-outlined text-sm">visibility_off</span>
                  </button>
                </div>

                {/* Question */}
                <div className="flex gap-4">
                  <div className="w-10 h-10 rounded-full bg-secondary-container text-on-secondary-container flex items-center justify-center font-medium text-[16px] shrink-0">
                    {disc.authorInitials}
                  </div>
                  <div className="flex-1 pr-6">
                    <div className="flex justify-between items-start mb-1">
                      <div>
                        <h4 className="text-[16px] font-medium text-on-surface truncate max-w-[200px] sm:max-w-[400px]">{disc.author}</h4>
                        <p className="text-[13px] text-on-surface-variant">{disc.timeAgo}</p>
                      </div>
                    </div>
                    <h5 className="text-[16px] font-medium text-on-surface mt-2 mb-1">{disc.title}</h5>
                    {editingComment === disc.id ? (
                      <div className="mt-2 mb-4">
                        <textarea className="w-full px-3 py-2 border border-outline-variant rounded-md text-[14px] focus:border-primary focus:ring-1 focus:ring-primary mb-2" value={editContent} onChange={e => setEditContent(e.target.value)} />
                        <div className="flex gap-2">
                          <button className="px-3 py-1 bg-primary text-white rounded-md text-[13px]" onClick={() => handleUpdateComment(disc.id, true)}>Lưu</button>
                          <button className="px-3 py-1 bg-surface-variant rounded-md text-[13px]" onClick={() => setEditingComment(null)}>Hủy</button>
                        </div>
                      </div>
                    ) : (
                      <p className="text-[14px] text-on-surface-variant mb-4">{disc.content}</p>
                    )}
                  </div>
                </div>
                    
                {/* Answers */}
                {disc.answers?.map((ans) => (
                  <div key={ans.id} className="mt-4 ml-14 pl-4 border-l-2 border-whisper-border flex gap-4 relative group/ans">
                    {/* Actions Answer */}
                    <div className="absolute top-4 right-4 flex items-center gap-2 opacity-0 group-hover/ans:opacity-100 transition-opacity bg-pure-surface px-2 rounded-md shadow-sm border border-whisper-border z-10">
                      {ans.authorId === currentAccountId && (
                        <>
                          <button onClick={() => startEdit(ans.id, ans.content)} className="text-on-surface-variant hover:text-primary p-1" title="Sửa">
                            <span className="material-symbols-outlined text-sm">edit</span>
                          </button>
                          <button onClick={() => handleDeleteComment(ans.id, false)} className="text-on-surface-variant hover:text-error p-1" title="Xóa">
                            <span className="material-symbols-outlined text-sm">delete</span>
                          </button>
                          <div className="w-[1px] h-4 bg-outline-variant mx-1"></div>
                        </>
                      )}
                      {ans.authorId !== currentAccountId && (
                        <button 
                          onClick={() => handleHideComment(ans.id, false)}
                          className="text-on-surface-variant hover:text-error p-1"
                          title="Ẩn bình luận"
                        >
                          <span className="material-symbols-outlined text-sm">visibility_off</span>
                        </button>
                      )}
                    </div>

                    <div className="w-8 h-8 rounded-full bg-surface-variant flex items-center justify-center font-medium shrink-0">
                      {ans.author?.[0] || "GV"}
                    </div>
                    <div className="flex-1 bg-surface-container-low rounded-lg p-4 pr-8">
                      <div className="flex justify-between items-start mb-2">
                        <div>
                          <h4 className="text-[16px] font-medium text-on-surface truncate max-w-[150px] sm:max-w-[300px]">
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
                    <input 
                      className="w-full bg-pure-surface border border-outline-variant rounded-lg px-4 py-2 text-[14px] focus:ring-primary focus:border-primary" 
                      placeholder="Nhập câu trả lời..." 
                      value={replyContent[disc.id] || ""}
                      onChange={(e) => setReplyContent(prev => ({ ...prev, [disc.id]: e.target.value }))}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") handleSubmitReply(disc.id);
                      }}
                    />
                    <button 
                      className="bg-primary text-on-primary px-4 py-2 rounded-lg text-[14px] font-medium flex items-center justify-center min-w-[70px] disabled:opacity-50"
                      onClick={() => handleSubmitReply(disc.id)}
                      disabled={submittingReply === disc.id || !replyContent[disc.id]?.trim()}
                    >
                      {submittingReply === disc.id ? "Đang gửi" : "Gửi"}
                    </button>
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>

    </TeacherLayout>
  );
}
