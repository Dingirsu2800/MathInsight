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
import ReportReasonChips from '../../components/question-reports/ReportReasonChips';
import { normalizeQuestionType } from '../../utils/questionLabels';

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

function isQuestionAbandoned(a) {
  if (a.isAbandoned !== undefined && a.isAbandoned !== null) return Boolean(a.isAbandoned);
  const normType = normalizeQuestionType(a.questionType);
  if (normType === 'COMPOSITE') {
    return !a.answerParts || a.answerParts.length === 0 || a.answerParts.every(
      (p) => p.studentAnswer === null || p.studentAnswer === undefined || String(p.studentAnswer).trim() === ''
    );
  }
  if (normType === 'SHORT_ANSWER') {
    return !a.shortAnswerText || a.shortAnswerText.trim() === '';
  }
  if (normType === 'MULTIPLE_CHOICE') {
    return (!a.selectedOptionIds || a.selectedOptionIds.length === 0) && !a.selectedOptionId;
  }
  return !a.selectedOptionId && (!a.selectedOptionIds || a.selectedOptionIds.length === 0);
}

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
      const abandoned = isQuestionAbandoned(a);
      if (filter === 'correct') return a.isCorrect === true;
      if (filter === 'wrong') return a.isCorrect === false && !abandoned;
      if (filter === 'skipped') return a.isCorrect === null || abandoned;
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
                const normType = normalizeQuestionType(answer.questionType);

                if (normType === 'COMPOSITE') {
                  const correctStatements = (answer.answerParts || []).map((p, i) => {
                    const label = p.partLabel || `Ý ${i + 1}`;
                    let ansText = p.correctAnswer;
                    if (p.correctAnswer === 'True' || p.correctAnswer === true) ansText = 'Đúng';
                    else if (p.correctAnswer === 'False' || p.correctAnswer === false) ansText = 'Sai';
                    return `${label}: ${ansText ?? '—'}`;
                  });
                  const compositeCorrectAnswer = correctStatements.length > 0
                    ? correctStatements.join('; ')
                    : (answer.solutionContent || 'Xem lời giải chi tiết');

                  return (
                    <CompositeQuestionCard
                      key={answer.questionId}
                      index={answer.questionNo}
                      stem={answer.questionContent}
                      pictureUrl={answer.pictureUrl}
                      difficulty={diff.text}
                      difficultyClass={diff.cls}
                      topicName={answer.topicName}
                      statements={(answer.answerParts || []).map((p, i) => ({
                        partId: p.questionPartId,
                        partOrder: p.partOrder ?? i + 1,
                        partLabel: p.partLabel || `Ý ${i + 1}`,
                        partType: p.partType,
                        text: p.partContent,
                        correctAnswer: p.correctAnswer,
                        studentAnswer: p.studentAnswer,
                        isCorrect: p.isCorrect,
                        pointsEarned: p.pointsEarned,
                        defaultWeight: p.defaultWeight,
                        explanation: p.explanation,
                      }))}
                      maxScore={answer.maxPoints ?? 1}
                      earnedScore={answer.isScoreInvalidated ? 0 : (answer.effectivePoints ?? answer.pointsEarned ?? 0)}
                      machinePoints={answer.machinePointsEarned ?? answer.pointsEarned ?? 0}
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

                // SINGLE_CHOICE, MULTIPLE_CHOICE, TRUE_FALSE, SHORT_ANSWER
                let calculatedCorrectAnswer = 'Xem lời giải chi tiết';
                if (normType === 'SHORT_ANSWER') {
                  const correctOpt = (answer.answerOptions || []).find((o) => o.isCorrect);
                  calculatedCorrectAnswer = correctOpt?.answerContent || answer.solutionContent || (answer.answerOptions || []).map((o) => o.answerContent).join(' hoặc ') || 'Xem lời giải chi tiết';
                } else {
                  const correctOptions = (answer.answerOptions || [])
                    .filter((o) => o.isCorrect)
                    .map((o) => {
                      const idx = (answer.answerOptions || []).indexOf(o);
                      return `${String.fromCharCode(65 + idx)}. ${o.answerContent}`;
                    })
                    .join(', ');
                  calculatedCorrectAnswer = correctOptions || answer.solutionContent || 'Xem lời giải chi tiết';
                }

                return (
                  <QuestionAnswerCard
                    key={answer.questionId}
                    index={answer.questionNo}
                    question={answer.questionContent}
                    questionType={normType}
                    pictureUrl={answer.pictureUrl}
                    difficulty={diff.text}
                    difficultyClass={diff.cls}
                    topicName={answer.topicName}
                    isCorrect={answer.isCorrect}
                    shortAnswerText={answer.shortAnswerText}
                    options={(answer.answerOptions || []).map((option, optionIndex) => ({
                      id: option.answerId,
                      label: String.fromCharCode(65 + optionIndex),
                      text: option.answerContent,
                      isCorrect: option.isCorrect,
                      isSelected: option.wasSelected,
                    }))}
                    solution={answer.solutionContent ? [answer.solutionContent] : []}
                    machinePoints={answer.machinePointsEarned ?? answer.pointsEarned ?? 0}
                    effectivePoints={answer.effectivePoints ?? answer.pointsEarned ?? 0}
                    maxPoints={answer.maxPoints ?? 1}
                    isScoreInvalidated={answer.isScoreInvalidated}
                    reportReason={answer.reportReason}
                    scoreAdjustedTime={answer.scoreAdjustedTime}
                    onReport={() => openReportDialog(answer)}
                    onAskChatbot={() => openChat({
                      sessionId,
                      questionId: answer.questionId,
                      questionNo: answer.questionNo,
                      questionContent: answer.questionContent,
                      correctAnswer: calculatedCorrectAnswer,
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
          <label className="block text-sm font-bold text-on-surface mb-2" htmlFor="student-question-report-reason">
            Lý do báo cáo
          </label>
          <ReportReasonChips
            value={reportReason}
            onChange={setReportReason}
            className="mb-3"
          />
          <textarea
            id="student-question-report-reason"
            rows={5}
            maxLength={1000}
            value={reportReason}
            onChange={(event) => setReportReason(event.target.value)}
            className="mt-1 w-full resize-y rounded-lg border border-outline-variant bg-pure-surface p-3 text-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
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
