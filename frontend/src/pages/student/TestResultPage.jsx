import { useState } from 'react';
import StudentLayout from '../../components/layout/StudentLayout';
import ScoreOverviewCard from './test-result/ScoreOverviewCard';
import TopicBreakdownCard from './test-result/TopicBreakdownCard';
import QuestionAnswerCard from './test-result/QuestionAnswerCard';
import CompositeQuestionCard from './test-result/CompositeQuestionCard';

// TODO: Replace with API data from /grading/submissions/:sessionId
const MOCK_QUESTIONS = {
  mcq: {
    index: 1,
    question: 'Giá trị của biểu thức P = logₐ(a²) với 0 < a ≠ 1 là:',
    difficulty: 'DỄ',
    difficultyClass: 'bg-primary-fixed text-primary',
    options: [
      { label: 'A', text: 'P = 2', isCorrect: true, isSelected: true },
      { label: 'B', text: 'P = 1', isCorrect: false, isSelected: false },
      { label: 'C', text: 'P = 0', isCorrect: false, isSelected: false },
      { label: 'D', text: 'P = a', isCorrect: false, isSelected: false },
    ],
    solution: [
      'Bước 1: Áp dụng công thức logarit cơ bản: logₐ(aⁿ) = n.',
      'Bước 2: Ở đây n = 2, vậy logₐ(a²) = 2.',
      'Bước 3: Đối chiếu với các phương án, ta chọn A.',
    ],
  },
  composite: {
    index: 5,
    stem: 'Cho hàm số f(x) = (x² - 3x + 2) / (x - 1). Xét tính đúng sai của các khẳng định sau:',
    difficulty: 'KHÓ',
    difficultyClass: 'bg-tertiary-fixed text-tertiary',
    statements: [
      { text: 'a) Tập xác định của hàm số là D = ℝ.', correctAnswer: false, studentAnswer: false },
      { text: 'b) Đồ thị hàm số có tiệm cận đứng tại x = 1.', correctAnswer: false, studentAnswer: true },
      { text: 'c) Hàm số có thể rút gọn thành f(x) = x - 2 trên D.', correctAnswer: true, studentAnswer: true },
      { text: 'd) Hàm số đồng biến trên toàn bộ tập xác định.', correctAnswer: false, studentAnswer: false },
    ],
    maxScore: 1,
    earnedScore: 0.5,
    solution: [
      '<strong>a)</strong> Hàm số f(x) có mẫu thức là (x - 1), nên điều kiện xác định là x ≠ 1. Vậy D = ℝ \\ {1}. Khẳng định này Sai.',
      '<strong>b)</strong> Khi x tiến tới 1, f(x) = (x-1)(x-2)/(x-1) = x-2. Giới hạn hữu hạn là -1, nên không có tiệm cận đứng. Khẳng định này Sai.',
      '<strong>c)</strong> Với x ≠ 1, f(x) = (x-1)(x-2)/(x-1) = x - 2. Khẳng định này Đúng.',
      '<strong>d)</strong> Đạo hàm f\'(x) = 1 > 0 với mọi x thuộc D. Hàm số đồng biến trên từng khoảng xác định (-∞; 1) và (1; +∞), nhưng không đồng biến trên toàn bộ D vì có điểm gián đoạn. Khẳng định này Sai.',
    ],
  },
};

const FILTER_OPTIONS = [
  { label: 'Tất cả (20)', key: 'all', className: 'bg-pure-surface border-whisper-border text-on-surface' },
  { label: 'Câu sai (3)', key: 'wrong', className: 'bg-deep-rose/10 text-deep-rose border-deep-rose/20' },
  { label: 'Câu đúng (15)', key: 'correct', className: 'bg-emerald-success/10 text-emerald-success border-emerald-success/20' },
];

export default function TestResultPage() {
  const [filter, setFilter] = useState('all');

  return (
    <StudentLayout>
      <div className="space-y-8">
        {/* Score + Topic Analysis row */}
        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-12 lg:col-span-5">
            <ScoreOverviewCard />
          </div>
          <div className="col-span-12 lg:col-span-7">
            <TopicBreakdownCard />
          </div>
        </div>

        {/* Question Detail Section */}
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <h3 className="text-2xl font-semibold text-on-surface">Chi tiết câu hỏi</h3>
            <div className="flex gap-2">
              {FILTER_OPTIONS.map((opt) => (
                <button
                  key={opt.key}
                  className={`px-4 py-2 border rounded-lg text-sm font-medium transition-colors ${
                    filter === opt.key
                      ? opt.className + ' ring-2 ring-primary/30'
                      : opt.className + ' hover:opacity-80'
                  }`}
                  onClick={() => setFilter(opt.key)}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          </div>

          {/* Example MCQ */}
          <QuestionAnswerCard
            index={MOCK_QUESTIONS.mcq.index}
            question={MOCK_QUESTIONS.mcq.question}
            difficulty={MOCK_QUESTIONS.mcq.difficulty}
            difficultyClass={MOCK_QUESTIONS.mcq.difficultyClass}
            options={MOCK_QUESTIONS.mcq.options}
            solution={MOCK_QUESTIONS.mcq.solution}
          />

          {/* Example Composite */}
          <CompositeQuestionCard
            index={MOCK_QUESTIONS.composite.index}
            stem={MOCK_QUESTIONS.composite.stem}
            difficulty={MOCK_QUESTIONS.composite.difficulty}
            difficultyClass={MOCK_QUESTIONS.composite.difficultyClass}
            statements={MOCK_QUESTIONS.composite.statements}
            maxScore={MOCK_QUESTIONS.composite.maxScore}
            earnedScore={MOCK_QUESTIONS.composite.earnedScore}
            solution={MOCK_QUESTIONS.composite.solution}
          />
        </div>
      </div>
    </StudentLayout>
  );
}
