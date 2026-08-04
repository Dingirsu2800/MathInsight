import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import StudentLayout from '../../components/layout/StudentLayout';
import ScoreOverviewCard from './test-result/ScoreOverviewCard';
import TopicBreakdownCard from './test-result/TopicBreakdownCard';
import QuestionAnswerCard from './test-result/QuestionAnswerCard';
import CompositeQuestionCard from './test-result/CompositeQuestionCard';
import ChatbotWidget from '../../components/student/ChatbotWidget';
import { getSessionResult, reportSessionQuestion } from '../../services/gradingApi';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../../components/ui/dialog';

/** Map DifficultyLevel (1-4) to label and CSS class */
function difficultyLabel(level) {
  switch (level) {
    case 1: return { text: 'DỄ', cls: 'bg-emerald-success/10 text-emerald-success' };
    case 2: return { text: 'TB', cls: 'bg-primary-fixed text-primary' };
    case 3: return { text: 'KHÓ', cls: 'bg-amber-warning/20 text-amber-warning' };
    case 4: return { text: 'RẤT KHÓ', cls: 'bg-tertiary-fixed text-tertiary' };
    default: return { text: '—', cls: 'bg-surface-container text-outline' };
  }
}

const FILTER_OPTIONS = [
  { label: 'Tất cả', key: 'all' },
  { label: 'Câu sai', key: 'wrong' },
  { label: 'Câu đúng', key: 'correct' },
  { label: 'Bỏ qua', key: 'skipped' },
];

export default function TestResultPage() {
  const { sessionId } = useParams();
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [filter, setFilter] = useState('all');
  const [reportTarget, setReportTarget] = useState(null);
  const [reportReason, setReportReason] = useState('');
  const [reportError, setReportError] = useState('');
  const [reportSubmitting, setReportSubmitting] = useState(false);

  // --- Chatbot state ---
  const [chatContext, setChatContext] = useState(null);
  const [isChatOpen, setIsChatOpen] = useState(false);

  const openChat = (context) => {
    setChatContext(context);
    setIsChatOpen(true);
  };

  useEffect(() => {
    if (!sessionId) return;
    let cancelled = false;
    let pollTimer = null;
    let pollCount = 0;
    const MAX_POLLS = 15;
    const POLL_INTERVAL_MS = 2000;

    const fetchResult = async () => {
      try {
        const data = await getSessionResult(sessionId);
        if (cancelled) return;

        // If grading is still in progress, poll until Graded
        if (data.status !== 'Graded' && pollCount < MAX_POLLS) {
          pollCount++;
          pollTimer = setTimeout(fetchResult, POLL_INTERVAL_MS);
          return;
        }

        setResult(data);
        setLoading(false);
      } catch {
        if (!cancelled) {
          setError(true);
          setLoading(false);
        }
      }
    };

    setLoading(true);
    setError(false);
    fetchResult();

    return () => {
      cancelled = true;
      if (pollTimer) clearTimeout(pollTimer);
    };
  }, [sessionId]);

  const filteredAnswers = useMemo(() => {
    if (!result?.answers) return [];
    return result.answers.filter((a) => {
      if (filter === 'all') return true;
      if (a.isScoreInvalidated) return false;
      if (filter === 'correct') return a.isCorrect === true;
      if (filter === 'wrong') return a.isCorrect === false && !a.isAbandoned;
      if (filter === 'skipped') return a.isCorrect === null || a.isAbandoned;
      return true;
    });
  }, [result, filter]);

  const filterOptions = FILTER_OPTIONS.map((opt) => {
    let count = 0;
    if (!result) return opt;
    if (opt.key === 'all') count = result.totalQuestion;
    if (opt.key === 'correct') count = result.numCorrect;
    if (opt.key === 'wrong') count = result.numIncorrect;
    if (opt.key === 'skipped') count = result.numAbandoned;
    return { ...opt, label: `${opt.label} (${count})` };
  });

  const openReportDialog = (answer) => {
    setReportTarget(answer);
    setReportReason('');
    setReportError('');
  };

  const submitReport = async () => {
    const reason = reportReason.trim();
    if (!reportTarget || reason.length < 10) {
      setReportError('Lý do báo cáo phải có ít nhất 10 ký tự.');
      return;
    }

    setReportSubmitting(true);
    setReportError('');
    try {
      await reportSessionQuestion(sessionId, reportTarget.questionId, reason);
      setReportTarget(null);
      setReportReason('');
    } catch (requestError) {
      const code = requestError.response?.data?.code;
      setReportError(code === 'REPORT_ALREADY_PENDING'
        ? 'Bạn đã gửi báo cáo cho câu hỏi này và báo cáo đang được xử lý.'
        : 'Không thể gửi báo cáo. Vui lòng thử lại.');
    } finally {
      setReportSubmitting(false);
    }
  };

  return (
    <StudentLayout>
      <div className="space-y-8">
        {/* Loading */}
        {loading && (
          <div className="flex items-center justify-center py-20 text-outline animate-pulse">
            Đang tải kết quả bài làm...
          </div>
        )}

        {/* Error */}
        {!loading && error && (
          <div className="flex items-center justify-center py-20 text-deep-rose text-sm">
            Không thể tải kết quả. Vui lòng thử lại sau.
          </div>
        )}

        {/* Data */}
        {!loading && !error && result && (
          <>
            {/* Score + Topic Analysis row */}
            <div className="grid grid-cols-12 gap-6">
              <div className="col-span-12 lg:col-span-5">
                <ScoreOverviewCard
                  score={result.score}
                  testFormat={result.testFormat}
                  submissionType={result.submissionType ?? '—'}
                  numCorrect={result.numCorrect}
                  numIncorrect={result.numIncorrect}
                  numAbandoned={result.numAbandoned}
                />
              </div>
              <div className="col-span-12 lg:col-span-7">
                <TopicBreakdownCard answers={result.answers} />
              </div>
            </div>

            {/* Question Detail Section */}
            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <h3 className="text-2xl font-semibold text-on-surface">Chi tiết câu hỏi</h3>
                <div className="flex gap-2">
                  {filterOptions.map((opt) => (
                    <button
                      key={opt.key}
                      className={`px-4 py-2 border rounded-lg text-sm font-medium transition-colors ${
                        filter === opt.key
                          ? 'bg-primary text-white border-primary'
                          : 'bg-pure-surface border-whisper-border text-on-surface hover:opacity-80'
                      }`}
                      onClick={() => setFilter(opt.key)}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              </div>

              {filteredAnswers.length === 0 && (
                <p className="text-sm text-outline text-center py-8">
                  Không có câu hỏi nào trong bộ lọc này.
                </p>
              )}

              {filteredAnswers.map((answer) => {
                const diff = difficultyLabel(answer.difficultyLevel);

                if (answer.questionType === 'COMPOSITE') {
                  // Xây dựng correctAnswer từ các ý đúng của composite
                  const correctStatements = answer.answerParts
                    .filter((p) => p.isCorrect)
                    .map((p, i) => `Ý ${i + 1}: ${p.partContent} → ${p.correctAnswer}`);
                  const compositeCorrectAnswer = correctStatements.length > 0
                    ? correctStatements.join('; ')
                    : 'Xem lời giải chi tiết';

                  return (
                    <CompositeQuestionCard
                      key={answer.questionId}
                      index={answer.questionNo}
                      stem={answer.questionContent}
                      difficulty={diff.text}
                      difficultyClass={diff.cls}
                      statements={answer.answerParts.map((p) => ({
                        text: p.partContent,
                        correctAnswer: p.correctAnswer === 'True',
                        studentAnswer: p.studentAnswer === 'True',
                        isCorrect: p.isCorrect,
                      }))}
                      maxScore={answer.maxPoints}
                      earnedScore={answer.effectivePoints}
                      machinePoints={answer.machinePointsEarned}
                      isScoreInvalidated={answer.isScoreInvalidated}
                      reportReason={answer.reportReason}
                      scoreAdjustedTime={answer.scoreAdjustedTime}
                      solution={answer.solutionContent ? [answer.solutionContent] : []}
                      onReport={() => openReportDialog(answer)}
                      onAskChatbot={() => openChat({
                        sessionId,
                        questionId: answer.questionId,
                        questionNo: answer.questionNo,
                        questionContent: answer.questionContent,
                        correctAnswer: compositeCorrectAnswer,
                      })}
                    />
                  );
                }

                // SINGLE_CHOICE, MULTIPLE_SELECT, TRUE_FALSE, SHORT_ANSWER
                // Xây dựng correctAnswer từ các option đúng
                const correctOptions = (answer.answerOptions || [])
                  .filter((o) => o.isCorrect)
                  .map((o, i) => `${String.fromCharCode(65 + (answer.answerOptions || []).indexOf(o))}. ${o.answerContent}`)
                  .join(', ');
                const mcqCorrectAnswer = correctOptions || 'Xem lời giải chi tiết';

                return (
                  <QuestionAnswerCard
                    key={answer.questionId}
                    index={answer.questionNo}
                    question={answer.questionContent}
                    difficulty={diff.text}
                    difficultyClass={diff.cls}
                    isCorrect={answer.isCorrect}
                    options={(answer.answerOptions || []).map((option, optionIndex) => ({
                      label: String.fromCharCode(65 + optionIndex),
                      text: option.answerContent,
                      isCorrect: option.isCorrect,
                      isSelected: option.wasSelected,
                    }))}
                    solution={answer.solutionContent ? [answer.solutionContent] : []}
                    machinePoints={answer.machinePointsEarned}
                    effectivePoints={answer.effectivePoints}
                    maxPoints={answer.maxPoints}
                    isScoreInvalidated={answer.isScoreInvalidated}
                    reportReason={answer.reportReason}
                    scoreAdjustedTime={answer.scoreAdjustedTime}
                    onReport={() => openReportDialog(answer)}
                    onAskChatbot={() => openChat({
                      sessionId,
                      questionId: answer.questionId,
                      questionNo: answer.questionNo,
                      questionContent: answer.questionContent,
                      correctAnswer: mcqCorrectAnswer,
                    })}
                  />
                );
              })}
            </div>
          </>
        )}
      </div>

      <Dialog isOpen={Boolean(reportTarget)} onClose={() => !reportSubmitting && setReportTarget(null)}>
        <DialogHeader>
          <DialogTitle>Báo cáo câu hỏi {reportTarget?.questionNo}</DialogTitle>
          <DialogDescription>
            Mô tả rõ nội dung hoặc đáp án bạn cho rằng chưa chính xác.
          </DialogDescription>
        </DialogHeader>
        <DialogContent>
          <label className="block text-sm font-bold text-on-surface" htmlFor="student-question-report-reason">
            Lý do báo cáo
          </label>
          <textarea
            id="student-question-report-reason"
            rows={5}
            maxLength={1000}
            value={reportReason}
            onChange={(event) => setReportReason(event.target.value)}
            className="mt-2 w-full resize-y rounded-lg border border-outline-variant bg-pure-surface p-3 text-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
          <div className="mt-1 flex justify-between text-xs text-on-surface-variant">
            <span>{reportError && <span className="text-error">{reportError}</span>}</span>
            <span>{reportReason.length}/1000</span>
          </div>
        </DialogContent>
        <DialogFooter>
          <Button variant="outline" onClick={() => setReportTarget(null)} disabled={reportSubmitting}>
            Hủy
          </Button>
          <Button onClick={submitReport} disabled={reportSubmitting || reportReason.trim().length < 10}>
            {reportSubmitting ? 'Đang gửi...' : 'Gửi báo cáo'}
          </Button>
        </DialogFooter>
      </Dialog>

      {/* Chatbot Widget — floating bottom-right */}
      <ChatbotWidget
        isOpen={isChatOpen}
        onClose={() => setIsChatOpen(false)}
        context={chatContext}
      />
    </StudentLayout>
  );
}


