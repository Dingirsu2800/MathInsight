import CircularGauge from '../../../components/ui/CircularGauge';

// TODO: Replace with API data from /grading/submissions/:id/result
const MOCK_SCORE = {
  score: 7.5,
  maxScore: 10,
  testType: 'Luyện tập',
  submissionType: 'StudentSubmit',
  correct: 15,
  wrong: 3,
  skipped: 2,
};

export default function ScoreOverviewCard() {
  const { score, maxScore, testType, submissionType, correct, wrong, skipped } = MOCK_SCORE;

  return (
    <div className="bg-pure-surface rounded-xl p-8 border border-whisper-border flex flex-col items-center justify-center relative overflow-hidden">
      {/* Test type badge */}
      <div className="absolute top-0 right-0 p-4">
        <span className="px-3 py-1 bg-primary-fixed text-primary text-[12px] font-bold rounded-full uppercase">
          {testType}
        </span>
      </div>

      {/* Gauge */}
      <CircularGauge value={score} max={maxScore} size={160} strokeWidth={12} />

      {/* Labels */}
      <div className="text-center mb-6 mt-4">
        <h2 className="text-xl font-semibold text-on-surface">Kết quả bài làm</h2>
        <p className="text-on-surface-variant text-sm mt-1">
          Submission type:{' '}
          <span className="font-mono text-xs text-primary">{submissionType}</span>
        </p>
      </div>

      {/* Correct / Wrong / Skipped */}
      <div className="flex gap-3 w-full">
        <div className="flex-1 bg-emerald-success/10 rounded-lg p-3 text-center border border-emerald-success/20">
          <p className="text-[11px] text-emerald-success font-bold uppercase tracking-wider">Đúng</p>
          <p className="text-xl font-bold text-emerald-success">{correct}</p>
        </div>
        <div className="flex-1 bg-deep-rose/10 rounded-lg p-3 text-center border border-deep-rose/20">
          <p className="text-[11px] text-deep-rose font-bold uppercase tracking-wider">Sai</p>
          <p className="text-xl font-bold text-deep-rose">{wrong}</p>
        </div>
        <div className="flex-1 bg-surface-container-low rounded-lg p-3 text-center border border-whisper-border">
          <p className="text-[11px] text-outline font-bold uppercase tracking-wider">Bỏ qua</p>
          <p className="text-xl font-bold text-on-surface">{skipped}</p>
        </div>
      </div>
    </div>
  );
}
