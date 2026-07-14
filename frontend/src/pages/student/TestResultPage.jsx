import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import StudentLayout from '../../components/layout/StudentLayout';
import ScoreOverviewCard from './test-result/ScoreOverviewCard';
import TopicBreakdownCard from './test-result/TopicBreakdownCard';
import QuestionAnswerCard from './test-result/QuestionAnswerCard';
import CompositeQuestionCard from './test-result/CompositeQuestionCard';
import { getSessionResult } from '../../services/gradingApi';

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

  useEffect(() => {
    if (!sessionId) return;
    let cancelled = false;
    setLoading(true);
    setError(false);

    getSessionResult(sessionId)
      .then((data) => { if (!cancelled) setResult(data); })
      .catch(() => { if (!cancelled) setError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [sessionId]);

  const filteredAnswers = useMemo(() => {
    if (!result?.answers) return [];
    return result.answers.filter((a) => {
      if (filter === 'all') return true;
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
                  return (
                    <CompositeQuestionCard
                      key={answer.questionId}
                      index={answer.questionNo}
                      stem={answer.questionContent}
                      difficulty={diff.text}
                      difficultyClass={diff.cls}
                      statements={answer.answerParts.map((p) => ({
                        text: p.studentAnswer ?? '',
                        isCorrect: p.isCorrect,
                      }))}
                      maxScore={answer.maxPoints}
                      earnedScore={answer.pointsEarned}
                    />
                  );
                }

                // SINGLE_CHOICE, MULTIPLE_SELECT, TRUE_FALSE, SHORT_ANSWER
                return (
                  <QuestionAnswerCard
                    key={answer.questionId}
                    index={answer.questionNo}
                    question={answer.questionContent}
                    difficulty={diff.text}
                    difficultyClass={diff.cls}
                    isCorrect={answer.isCorrect}
                    pointsEarned={answer.pointsEarned}
                    maxPoints={answer.maxPoints}
                  />
                );
              })}
            </div>
          </>
        )}
      </div>
    </StudentLayout>
  );
}


