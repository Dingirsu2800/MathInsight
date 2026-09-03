import * as React from "react";
import { useState, useEffect, useCallback } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import StudentLayout from "../../components/layout/StudentLayout";
import { getLectures, getTopics, getDifficulties } from "../../services/learningApi";
import RecommendedLecturesCard from "./dashboard/RecommendedLecturesCard";
export default function StudentLectureListPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const search = searchParams.get("search") || "";
  const gradeFilter = searchParams.get("grade") || "";
  const topicFilter = searchParams.get("topic") || "";
  const difficultyFilter = searchParams.get("difficulty") || "";
  const page = parseInt(searchParams.get("page") || "1", 10);

  const updateSearchParam = (key, value) => {
    setSearchParams(prev => {
      const newParams = new URLSearchParams(prev);
      if (value) {
        newParams.set(key, value);
      } else {
        newParams.delete(key);
      }
      if (key !== 'page') {
        newParams.set('page', '1');
      }
      return newParams;
    }, { replace: true });
  };

  const setSearch = (val) => updateSearchParam("search", val);
  const setGradeFilter = (val) => updateSearchParam("grade", val);
  const setTopicFilter = (val) => updateSearchParam("topic", val);
  const setDifficultyFilter = (val) => updateSearchParam("difficulty", val);
  const setPage = (val) => {
    const newPage = typeof val === 'function' ? val(page) : val;
    updateSearchParam("page", newPage.toString());
  };

  const [lectures, setLectures] = useState([]);
  const [topics, setTopics] = useState([]);
  const [difficulties, setDifficulties] = useState([]);
  const [loading, setLoading] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const PAGE_SIZE = 12;
  useEffect(() => {
    getTopics(gradeFilter)
      .then(res => {
        let flat = [];
        res.data.forEach(ch => {
          flat.push({ id: ch.tagId, name: ch.tagName, isChapter: true });
          ch.children?.forEach(top => {
            flat.push({ id: top.tagId, name: top.tagName, isChapter: false });
          });
        });
        setTopics(flat);
      })
      .catch(err => console.error(err));
      
      getDifficulties()
      .then(res => {
        setDifficulties(res.data);
      })
      .catch(err => console.error(err));
  }, [gradeFilter]);

  const fetchLectures = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getLectures({ 
        page, 
        pageSize: PAGE_SIZE, 
        search, 
        topic: topicFilter || undefined,
        grade: gradeFilter || undefined,
        difficulty: difficultyFilter || undefined,
        isStudent: true 
      });
      setLectures(res.data?.items || res.data || []);
      setTotalCount(res.data?.totalCount || 0);
    } catch (err) {
      console.error("Lỗi khi tải danh sách bài giảng:", err);
      setLectures([]);
    } finally {
      setLoading(false);
    }
  }, [search, topicFilter, gradeFilter, difficultyFilter, page]);

  useEffect(() => { fetchLectures(); }, [fetchLectures]);

  return (
    <StudentLayout>
      <div className="p-gutter flex flex-col gap-6 w-full max-w-screen-2xl mx-auto">
        <div className="flex flex-col gap-2">
          <h1 className="text-[32px] font-semibold text-on-surface">Thư viện bài giảng</h1>
          <p className="text-[14px] text-on-surface-variant">Khám phá các bài giảng từ giáo viên của bạn.</p>
        </div>

        {page === 1 && !search && !gradeFilter && !topicFilter && !difficultyFilter && (
          <RecommendedLecturesCard />
        )}

        <div className="flex flex-col md:flex-row gap-4 mb-2 mt-2">
          <div className="relative w-full md:w-96">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant">search</span>
            <input
              className="w-full pl-10 pr-4 py-3 bg-pure-surface border border-outline-variant rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none transition-shadow text-[14px]"
              placeholder="Tìm kiếm bài giảng..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          
          <select 
            className="w-full md:w-48 px-4 py-3 bg-pure-surface border border-outline-variant rounded-xl text-[14px] outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            value={gradeFilter}
            onChange={(e) => {
              setGradeFilter(e.target.value);
              setTopicFilter("");
            }}
          >
            <option value="">Tất cả các lớp</option>
            <option value="12">Lớp 12</option>
            <option value="11">Lớp 11</option>
            <option value="10">Lớp 10</option>
          </select>

          <select 
            className="w-full md:w-64 px-4 py-3 bg-pure-surface border border-outline-variant rounded-xl text-[14px] outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            value={topicFilter}
            onChange={(e) => setTopicFilter(e.target.value)}
          >
            <option value="">Tất cả chủ đề</option>
            {topics.map(t => (
              <option key={t.id} value={t.id} disabled={t.isChapter} className={t.isChapter ? "font-bold text-on-surface-variant bg-surface-container" : "pl-4"}>
                {t.isChapter ? t.name : `— ${t.name}`}
              </option>
            ))}
          </select>
          
          <select 
            className="w-full md:w-48 px-4 py-3 bg-pure-surface border border-outline-variant rounded-xl text-[14px] outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            value={difficultyFilter}
            onChange={(e) => setDifficultyFilter(e.target.value)}
          >
            <option value="">Tất cả độ khó</option>
            {difficulties.map(d => (
              <option key={d.difficultyId} value={d.difficultyId}>
                {d.difficultyName}
              </option>
            ))}
          </select>
        </div>

        {loading ? (
          <div className="flex items-center justify-center p-12">
            <div className="w-8 h-8 rounded-full border-4 border-primary/30 border-t-primary animate-spin"></div>
          </div>
        ) : lectures.length === 0 ? (
          <div className="bg-surface-container-low border border-whisper-border rounded-xl p-12 flex flex-col items-center justify-center text-center">
            <span className="material-symbols-outlined text-[48px] text-on-surface-variant/50 mb-4">search_off</span>
            <h3 className="text-[16px] font-medium text-on-surface">Không tìm thấy bài giảng</h3>
            <p className="text-[14px] text-on-surface-variant mt-1">Chưa có bài giảng nào được xuất bản hoặc phù hợp với tìm kiếm của bạn.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
            {lectures.map((lec) => (
              <div 
                key={lec.lectureId} 
                className="bg-pure-surface rounded-2xl border border-whisper-border overflow-hidden group cursor-pointer hover:shadow-md hover:border-primary/30 transition-all flex flex-col h-full"
                onClick={() => navigate(`/student/lectures/${lec.lectureId}`)}
              >
                <div className="relative aspect-video bg-surface-container-low overflow-hidden">
                  {lec.thumbnailUrl ? (
                    <img src={lec.thumbnailUrl} alt={lec.title} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500" />
                  ) : (
                    <div className="w-full h-full flex flex-col items-center justify-center text-primary/40 bg-primary/5 group-hover:bg-primary/10 transition-colors">
                      <span className="material-symbols-outlined text-[48px] mb-2">play_circle</span>
                    </div>
                  )}
                  <div className="absolute top-3 left-3 px-2 py-1 bg-surface/80 backdrop-blur-sm text-on-surface text-[11px] font-semibold tracking-wider rounded-md uppercase">
                    {lec.tagName || "Chủ đề"}
                  </div>
                </div>
                
                <div className="p-5 flex flex-col flex-1">
                  <h3 className="text-[16px] font-semibold text-on-surface leading-snug mb-2 group-hover:text-primary transition-colors line-clamp-2">
                    {lec.title}
                  </h3>
                  <div className="mt-auto pt-4 flex flex-col gap-2">
                    <div className="flex items-center justify-between text-[13px] text-on-surface-variant">
                      <div className="flex items-center gap-1.5">
                        <div className="w-6 h-6 rounded-full bg-secondary-container text-on-secondary-container flex items-center justify-center font-bold text-[10px]">
                          {lec.teacherName?.[0] || "GV"}
                        </div>
                        <span className="truncate max-w-[120px] font-medium">{lec.teacherName || "Giáo viên"}</span>
                      </div>
                      <div className="flex items-center gap-1 font-medium text-primary">
                        <span className="material-symbols-outlined text-[14px]">favorite</span>
                        {lec.likes || 0}
                      </div>
                    </div>
                    <div className="flex items-center justify-between mt-1">
                      <div className="flex items-center gap-1.5 text-[12px] text-on-surface-variant/80">
                        <span className="material-symbols-outlined text-[14px]">calendar_today</span>
                        {new Date(lec.createdTime).toLocaleDateString("vi-VN")}
                      </div>
                      {lec.difficultyName && (
                        <div className="px-1.5 py-0.5 bg-primary/10 text-primary text-[10px] font-bold tracking-wider rounded uppercase">
                          {lec.difficultyName}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {!loading && totalCount > PAGE_SIZE && (
          <div className="flex items-center justify-center gap-4 mt-8 mb-4">
            <button
              disabled={page === 1}
              onClick={() => { setPage(p => p - 1); window.scrollTo({ top: 0, behavior: 'smooth' }); }}
              className="px-4 py-2 border border-outline-variant rounded-lg text-[14px] font-medium disabled:opacity-50 hover:bg-surface-variant transition-colors flex items-center gap-1"
            >
              <span className="material-symbols-outlined text-[18px]">chevron_left</span>
              Trang trước
            </button>
            <span className="text-on-surface-variant text-[14px] font-medium">
              Trang {page} / {Math.ceil(totalCount / PAGE_SIZE)}
            </span>
            <button
              disabled={page >= Math.ceil(totalCount / PAGE_SIZE)}
              onClick={() => { setPage(p => p + 1); window.scrollTo({ top: 0, behavior: 'smooth' }); }}
              className="px-4 py-2 border border-outline-variant rounded-lg text-[14px] font-medium disabled:opacity-50 hover:bg-surface-variant transition-colors flex items-center gap-1"
            >
              Trang sau
              <span className="material-symbols-outlined text-[18px]">chevron_right</span>
            </button>
          </div>
        )}
      </div>
    </StudentLayout>
  );
}
