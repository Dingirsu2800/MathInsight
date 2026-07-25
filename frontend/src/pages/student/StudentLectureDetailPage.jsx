import * as React from "react";
import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import StudentLayout from "./StudentLayout";
import { getLecture, getDiscussions, askQuestion, answerQuestion, reportDiscussion, likeLecture, unlikeLecture, updateComment, deleteComment } from "../../services/learningApi";
import LatexPreview from "../../components/expert/LatexPreview";

const getYouTubeEmbedUrl = (url) => {
  if (!url) return null;
  const regExp = /^.*(youtu.be\/|v\/|u\/\w\/|embed\/|watch\?v=|\&v=)([^#\&\?]*).*/;
  const match = url.match(regExp);
  return (match && match[2].length === 11) ? `https://www.youtube.com/embed/${match[2]}` : null;
};

export default function StudentLectureDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [lecture, setLecture] = useState(null);
  const [loading, setLoading] = useState(true);
  const [discussions, setDiscussions] = useState([]);
  const [newQuestionTitle, setNewQuestionTitle] = useState("");
  const currentAccountId = localStorage.getItem("AccountId");
  const userRole = localStorage.getItem("RoleName");
  
  const getFormatIcon = (format) => {
    const f = format?.toUpperCase() || "";
    if (f.includes("PDF")) return "picture_as_pdf";
    if (f.includes("MP4") || f.includes("VIDEO")) return "movie";
    if (f.includes("DOC") || f.includes("WORD")) return "description";
    return "insert_drive_file";
  };
  const [newQuestionContent, setNewQuestionContent] = useState("");
  const [submittingQuestion, setSubmittingQuestion] = useState(false);
  const [reportModal, setReportModal] = useState({ isOpen: false, targetId: null, isQuestion: false, reason: "" });
  const [isLiked, setIsLiked] = useState(false);
  
  const [editingComment, setEditingComment] = useState(null);
  const [editContent, setEditContent] = useState("");
  
  const [replyContent, setReplyContent] = useState({});
  const [submittingReply, setSubmittingReply] = useState(null);

  const fetchDiscussionsData = async () => {
    try {
      const res = await getDiscussions(id, { page: 1, pageSize: 50 });
      const mappedDiscussions = (res.data || []).map(d => ({
        id: d.discussionQuestionId,
        authorId: d.studentId,
        author: d.authorName || "Học sinh ẩn danh",
        authorInitials: d.authorName ? d.authorName.substring(0, 2).toUpperCase() : "HS",
        timeAgo: new Date(d.createdTime).toLocaleString("vi-VN"),
        title: d.title,
        content: d.content,
        status: d.status,
        answers: (d.answers || []).map(a => ({
          id: a.discussionAnswerId,
          authorId: a.accountId,
          author: a.authorName || "Giáo viên",
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
      .then((res) => {
        setLecture(res.data);
        if (res.data.isLiked !== undefined) {
          setIsLiked(res.data.isLiked);
        }
      })
      .catch((err) => {
        console.error("Lỗi khi tải chi tiết bài giảng:", err);
        setLecture(null);
      })
      .finally(() => setLoading(false));

    fetchDiscussionsData();
  }, [id]);

  const handleLikeToggle = async () => {
    try {
      if (isLiked) {
        await unlikeLecture(id);
        setLecture(prev => ({ ...prev, likes: Math.max(0, (prev.likes || 1) - 1) }));
      } else {
        await likeLecture(id);
        setLecture(prev => ({ ...prev, likes: (prev.likes || 0) + 1 }));
      }
      setIsLiked(!isLiked);
    } catch (err) {
      console.error("Lỗi khi tương tác like", err);
    }
  };

  const handleSubmitQuestion = async () => {
    if (!newQuestionTitle.trim() || !newQuestionContent.trim()) return;
    setSubmittingQuestion(true);
    try {
      await askQuestion({
        lectureId: id,
        title: newQuestionTitle,
        content: newQuestionContent
      });
      setNewQuestionTitle("");
      setNewQuestionContent("");
      await fetchDiscussionsData();
    } catch (err) {
      console.error("Lỗi khi gửi câu hỏi", err);
      alert("Lỗi khi gửi câu hỏi!");
    } finally {
      setSubmittingQuestion(false);
    }
  };

  const handleAnswerQuestion = async (questionId) => {
    const content = replyContent[questionId];
    if (!content || !content.trim()) return;
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

  const handleSubmitReport = async () => {
    if (!reportModal.reason.trim()) return;
    try {
      const payload = {
        questionId: reportModal.isQuestion ? reportModal.targetId : null,
        answerId: reportModal.isQuestion ? null : reportModal.targetId,
        reason: reportModal.reason
      };
      await reportDiscussion(payload);
      alert("Đã gửi báo cáo vi phạm thành công!");
      setReportModal({ isOpen: false, targetId: null, isQuestion: false, reason: "" });
    } catch (err) {
      console.error("Lỗi khi gửi báo cáo:", err);
      alert("Lỗi khi gửi báo cáo vi phạm!");
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
      <StudentLayout>
        <div className="flex items-center justify-center h-full min-h-[400px]">
          <div className="w-10 h-10 rounded-full border-4 border-primary/30 border-t-primary animate-spin"></div>
        </div>
      </StudentLayout>
    );
  }

  if (!lecture) {
    return (
      <StudentLayout>
        <div className="flex flex-col items-center justify-center h-full min-h-[400px]">
          <h2 className="text-2xl font-semibold text-on-surface mb-2">Không tìm thấy bài giảng</h2>
          <p className="text-on-surface-variant mb-6">Bài giảng có thể đã bị xóa hoặc ngừng hoạt động.</p>
          <button onClick={() => navigate("/student/lectures")} className="px-6 py-2 bg-primary text-on-primary rounded-xl font-medium">Quay lại danh sách</button>
        </div>
      </StudentLayout>
    );
  }

  return (
    <StudentLayout>
      <div className="p-gutter flex flex-col w-full max-w-4xl mx-auto mb-12">
        {/* Navigation Breadcrumb */}
        <button 
          onClick={() => navigate("/student/lectures")}
          className="flex items-center gap-2 text-on-surface-variant hover:text-primary transition-colors mb-6 w-fit"
        >
          <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          <span className="text-[14px] font-medium">Quay lại thư viện</span>
        </button>

        {/* Video & Info Section */}
        <article className="bg-pure-surface border border-whisper-border rounded-2xl overflow-hidden shadow-sm mb-8">
          <div className="relative aspect-video bg-black flex items-center justify-center overflow-hidden group">
            {lecture.videoUrl ? (
              getYouTubeEmbedUrl(lecture.videoUrl) ? (
                <iframe src={getYouTubeEmbedUrl(lecture.videoUrl)} className="w-full h-full" allowFullScreen allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" />
              ) : (
                <video src={lecture.videoUrl} controls className="w-full h-full object-cover" poster={lecture.thumbnailUrl} />
              )
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
                <button 
                  onClick={handleLikeToggle}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors font-medium text-[16px] ${
                    isLiked ? "bg-[#fee2e2] text-[#ef4444] hover:bg-[#fecaca]" : "bg-surface-container text-on-surface-variant hover:bg-surface-container-high hover:text-[#ef4444]"
                  }`}
                >
                  <span className="material-symbols-outlined" style={{ fontVariationSettings: isLiked ? "'FILL' 1" : "'FILL' 0" }}>favorite</span>
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
              <span className="material-symbols-outlined">attachment</span>
              Tài liệu đính kèm ({lecture.materials.length})
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {lecture.materials.map((mat) => {
                const fmt = (mat.format || mat.fileType)?.toUpperCase() || "";
                let icon = "insert_drive_file";
                let color = "text-on-surface-variant";
                if (fmt.includes("PDF")) { icon = "picture_as_pdf"; color = "text-[#ef4444]"; }
                if (fmt.includes("MP4") || fmt.includes("VIDEO")) { icon = "movie"; color = "text-[#3b82f6]"; }
                if (fmt.includes("DOC") || fmt.includes("WORD")) { icon = "description"; color = "text-[#2563eb]"; }

                return (
                  <div key={mat.materialId} className="flex items-center justify-between p-4 bg-pure-surface border border-whisper-border rounded-xl hover:border-primary/30 transition-colors group">
                    <div className="flex items-center gap-3 overflow-hidden">
                      <div className={`w-10 h-10 rounded-lg bg-surface-container flex items-center justify-center shrink-0 ${color}`}>
                        <span className="material-symbols-outlined text-[20px]">{icon}</span>
                      </div>
                      <div className="min-w-0">
                        <h4 className="text-[14px] font-medium text-on-surface truncate pr-2">{mat.name || mat.materialName}</h4>
                        <p className="text-[12px] text-on-surface-variant uppercase tracking-wider">{mat.format || mat.fileType}</p>
                      </div>
                    </div>
                    <a href={mat.url || mat.fileUrl} target="_blank" rel="noopener noreferrer" className="p-2 text-primary hover:bg-primary/10 rounded-full transition-colors flex items-center justify-center shrink-0" title={fmt.includes("MP4") ? "Xem" : "Tải xuống"}>
                      <span className="material-symbols-outlined text-[20px]">{fmt.includes("MP4") ? "visibility" : "download"}</span> 
                    </a>
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

          {/* Ask Question Box */}
          <div className="bg-surface-container-low rounded-xl p-6 mb-8 border border-whisper-border">
            <h3 className="text-[16px] font-medium text-on-surface mb-4">Bạn có câu hỏi gì về bài giảng này?</h3>
            <div className="flex flex-col gap-3">
              <input 
                type="text" 
                placeholder="Tiêu đề câu hỏi..." 
                className="w-full px-4 py-2 border border-outline-variant rounded-lg focus:ring-primary focus:border-primary text-[14px]"
                value={newQuestionTitle}
                onChange={(e) => setNewQuestionTitle(e.target.value)}
              />
              <textarea 
                className="w-full px-4 py-3 bg-pure-surface border border-outline-variant rounded-lg text-[14px] focus:ring-primary focus:border-primary resize-y min-h-[100px]"
                placeholder="Mô tả chi tiết câu hỏi của bạn..."
                value={newQuestionContent}
                onChange={(e) => setNewQuestionContent(e.target.value)}
              />
              <div className="flex justify-end">
                <button 
                  onClick={handleSubmitQuestion}
                  disabled={!newQuestionTitle.trim() || !newQuestionContent.trim() || submittingQuestion}
                  className="px-6 py-2 bg-primary text-on-primary rounded-lg font-medium hover:opacity-90 transition-opacity disabled:opacity-50"
                >
                  {submittingQuestion ? "Đang gửi..." : "Gửi câu hỏi"}
                </button>
              </div>
            </div>
          </div>
          
          <div className="space-y-6">
            {discussions.map((disc) => (
              <div key={disc.id} className={`bg-pure-surface rounded-xl border border-whisper-border p-6 relative group ${disc.status === "Hidden" ? "opacity-60 bg-surface-container-lowest" : ""}`}>
                {/* Actions Question */}
                <div className="absolute top-6 right-6 flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity bg-pure-surface px-2 rounded-md shadow-sm border border-whisper-border">
                  {disc.authorId === currentAccountId ? (
                    <>
                      <button onClick={() => startEdit(disc.id, disc.content)} className="text-on-surface-variant hover:text-primary p-1" title="Sửa">
                        <span className="material-symbols-outlined text-sm">edit</span>
                      </button>
                      <button onClick={() => handleDeleteComment(disc.id, true)} className="text-on-surface-variant hover:text-error p-1" title="Xóa">
                        <span className="material-symbols-outlined text-sm">delete</span>
                      </button>
                    </>
                  ) : (
                    <button 
                      onClick={() => setReportModal({ isOpen: true, targetId: disc.id, isQuestion: true, reason: "" })}
                      className="text-on-surface-variant hover:text-[#f59e0b] p-1"
                      title="Báo cáo vi phạm"
                    >
                      <span className="material-symbols-outlined text-sm">flag</span>
                    </button>
                  )}
                </div>

                {/* Question */}
                <div className="flex gap-4">
                  <div className="w-10 h-10 rounded-full bg-secondary-container text-on-secondary-container flex items-center justify-center font-medium text-[16px] shrink-0">
                    {disc.authorInitials}
                  </div>
                  <div className="flex-1 pr-6">
                    <div className="flex justify-between items-start mb-1">
                      <div>
                        <h4 className="text-[16px] font-medium text-on-surface truncate max-w-[200px] sm:max-w-[400px]">
                          {disc.author}
                          {disc.status === "Hidden" && <span className="ml-2 text-[10px] font-bold uppercase tracking-wider bg-[#fee2e2] text-[#ef4444] px-2 py-0.5 rounded-full">Đã bị ẩn</span>}
                        </h4>
                        <p className="text-[13px] text-on-surface-variant">{disc.timeAgo}</p>
                      </div>
                    </div>
                    <h5 className="text-[16px] font-medium text-on-surface mt-2 mb-1">{disc.title}</h5>
                    {editingComment === disc.id ? (
                      <div className="mt-2">
                        <textarea className="w-full px-3 py-2 border border-outline-variant rounded-md text-[14px] focus:border-primary focus:ring-1 focus:ring-primary mb-2" value={editContent} onChange={e => setEditContent(e.target.value)} />
                        <div className="flex gap-2">
                          <button className="px-3 py-1 bg-primary text-white rounded-md text-[13px]" onClick={() => handleUpdateComment(disc.id, true)}>Lưu</button>
                          <button className="px-3 py-1 bg-surface-variant rounded-md text-[13px]" onClick={() => setEditingComment(null)}>Hủy</button>
                        </div>
                      </div>
                    ) : (
                      <>
                        {disc.status === "Hidden" && (
                          <div className="bg-[#fee2e2] text-[#ef4444] text-[13px] px-3 py-2 rounded-lg mb-3 flex items-center gap-2 border border-[#fca5a5]">
                            <span className="material-symbols-outlined text-[16px]">info</span>
                            Bình luận của bạn đã bị ẩn do vi phạm tiêu chuẩn cộng đồng.
                          </div>
                        )}
                        <p className={`text-[14px] text-on-surface-variant ${disc.status === "Hidden" ? "blur-[1px] select-none italic" : ""}`}>{disc.content}</p>
                      </>
                    )}
                  </div>
                </div>

                {/* Answers */}
                {disc.answers?.map((ans) => (
                  <div key={ans.id} className={`mt-4 ml-14 pl-4 border-l-2 border-whisper-border flex gap-4 relative group/ans ${ans.status === "Hidden" ? "opacity-60" : ""}`}>
                    {/* Actions Answer */}
                    <div className="absolute top-4 right-4 flex items-center gap-2 opacity-0 group-hover/ans:opacity-100 transition-opacity bg-pure-surface px-2 rounded-md shadow-sm border border-whisper-border z-10">
                      {ans.authorId === currentAccountId ? (
                        <>
                          <button onClick={() => startEdit(ans.id, ans.content)} className="text-on-surface-variant hover:text-primary p-1" title="Sửa">
                            <span className="material-symbols-outlined text-sm">edit</span>
                          </button>
                          <button onClick={() => handleDeleteComment(ans.id, false)} className="text-on-surface-variant hover:text-error p-1" title="Xóa">
                            <span className="material-symbols-outlined text-sm">delete</span>
                          </button>
                        </>
                      ) : (
                        <button 
                          onClick={() => setReportModal({ isOpen: true, targetId: ans.id, isQuestion: false, reason: "" })}
                          className="text-on-surface-variant hover:text-[#f59e0b] p-1"
                          title="Báo cáo vi phạm"
                        >
                          <span className="material-symbols-outlined text-sm">flag</span>
                        </button>
                      )}
                    </div>

                    <div className="w-8 h-8 rounded-full bg-surface-variant flex items-center justify-center font-medium shrink-0">
                      {ans.author?.[0] || "GV"}
                    </div>
                    <div className="flex-1 pr-6">
                      <div className="flex items-center gap-2 mb-1">
                        <h4 className="text-[14px] font-medium text-on-surface">{ans.author}</h4>
                        <span className="text-[10px] uppercase tracking-wider font-semibold bg-surface-variant text-on-surface-variant px-1.5 py-0.5 rounded-sm">
                          {ans.role}
                        </span>
                        {ans.status === "Hidden" && <span className="text-[10px] font-bold uppercase tracking-wider bg-[#fee2e2] text-[#ef4444] px-2 py-0.5 rounded-full">Đã bị ẩn</span>}
                      </div>
                      <p className="text-[12px] text-on-surface-variant mb-2">{ans.timeAgo}</p>
                      {editingComment === ans.id ? (
                        <div className="mt-2">
                          <textarea className="w-full px-3 py-2 border border-outline-variant rounded-md text-[14px] focus:border-primary focus:ring-1 focus:ring-primary mb-2" value={editContent} onChange={e => setEditContent(e.target.value)} />
                          <div className="flex gap-2">
                            <button className="px-3 py-1 bg-primary text-white rounded-md text-[13px]" onClick={() => handleUpdateComment(ans.id, false)}>Lưu</button>
                            <button className="px-3 py-1 bg-surface-variant rounded-md text-[13px]" onClick={() => setEditingComment(null)}>Hủy</button>
                          </div>
                        </div>
                      ) : (
                        <>
                          {ans.status === "Hidden" && (
                            <div className="bg-[#fee2e2] text-[#ef4444] text-[12px] px-2 py-1.5 rounded-md mb-2 flex items-start gap-1.5 border border-[#fca5a5]">
                              <span className="material-symbols-outlined text-[14px] mt-0.5">info</span>
                              <span>Bình luận đã bị ẩn do vi phạm tiêu chuẩn cộng đồng.</span>
                            </div>
                          )}
                          <p className={`text-[14px] text-on-surface ${ans.status === "Hidden" ? "blur-[1px] select-none italic text-on-surface-variant" : ""}`}>{ans.content}</p>
                        </>
                      )}
                    </div>
                  </div>
                ))}
                
                {/* Reply Input Box */}
                {disc.status !== "Hidden" && (
                  <div className="mt-4 ml-14 pl-4 flex gap-4">
                      <input 
                        className="w-full bg-pure-surface border border-outline-variant rounded-lg px-4 py-2 text-[14px] focus:ring-primary focus:border-primary" 
                        placeholder="Nhập câu trả lời..." 
                        value={replyContent[disc.id] || ""}
                        onChange={(e) => setReplyContent(prev => ({ ...prev, [disc.id]: e.target.value }))}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") handleAnswerQuestion(disc.id);
                        }}
                      />
                      <button 
                        className="bg-primary text-on-primary px-4 py-2 rounded-lg text-[14px] font-medium flex items-center justify-center min-w-[70px] disabled:opacity-50"
                        onClick={() => handleAnswerQuestion(disc.id)}
                        disabled={submittingReply === disc.id || !(replyContent[disc.id] || "").trim()}
                      >
                        {submittingReply === disc.id ? "Đang gửi" : "Gửi"}
                      </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>
      </div>

      {/* Report Modal */}
      {reportModal.isOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div className="absolute inset-0 bg-on-surface/40 backdrop-blur-sm" onClick={() => setReportModal({ ...reportModal, isOpen: false })}></div>
          <div className="relative bg-pure-surface w-full max-w-md rounded-xl shadow-lg border border-outline-variant flex flex-col m-4">
            <div className="px-6 py-4 border-b border-whisper-border flex justify-between items-center">
              <h3 className="text-[20px] font-semibold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined text-[#f59e0b]">flag</span>
                Báo cáo vi phạm
              </h3>
              <button className="text-on-surface-variant hover:text-on-surface transition-colors" onClick={() => setReportModal({ ...reportModal, isOpen: false })}>
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>
            <div className="p-6">
              <p className="text-[14px] text-on-surface-variant mb-4">Vui lòng cung cấp lý do báo cáo bình luận này. Quản trị viên sẽ xem xét và xử lý.</p>
              <textarea 
                className="w-full bg-pure-surface border border-outline-variant rounded-lg px-4 py-3 text-[14px] focus:ring-primary focus:border-primary resize-y min-h-[100px]"
                placeholder="Lý do báo cáo (VD: Ngôn từ đả kích, Spam, ...)"
                value={reportModal.reason}
                onChange={(e) => setReportModal({ ...reportModal, reason: e.target.value })}
                autoFocus
              />
            </div>
            <div className="px-6 py-4 bg-surface-container-low border-t border-whisper-border flex justify-end gap-3 rounded-b-xl">
              <button 
                className="px-4 py-2 border border-outline-variant rounded-lg text-[16px] font-medium text-on-surface hover:bg-surface-variant transition-colors"
                onClick={() => setReportModal({ ...reportModal, isOpen: false })}
              >
                Hủy
              </button>
              <button 
                className="px-4 py-2 bg-primary rounded-lg text-[16px] font-medium text-on-primary hover:opacity-90 transition-opacity disabled:opacity-50"
                onClick={handleSubmitReport}
                disabled={!reportModal.reason.trim()}
              >
                Gửi báo cáo
              </button>
            </div>
          </div>
        </div>
      )}

    </StudentLayout>
  );
}
