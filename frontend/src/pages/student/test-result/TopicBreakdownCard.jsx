import MaterialIcon from '../../../components/ui/MaterialIcon';
import ProgressBar from '../../../components/ui/ProgressBar';

function getBarColor(level) {
  switch (level) {
    case 'good': return 'bg-primary';
    case 'improve': return 'bg-primary/70';
    case 'weak': return 'bg-deep-rose';
    default: return 'bg-primary';
  }
}

function getLabelColor(level) {
  return level === 'weak' ? 'text-deep-rose' : 'text-primary';
}

export default function TopicBreakdownCard({ answers = [] }) {
  // Aggregate accuracy per topic
  const topicMap = {};

  answers.forEach((ans) => {
    const topicName = ans.topicName || (ans.questionType ? `Dạng ${ans.questionType}` : 'Chủ đề khác');
    if (!topicMap[topicName]) {
      topicMap[topicName] = { name: topicName, total: 0, correct: 0, pointsEarned: 0, maxPoints: 0 };
    }
    topicMap[topicName].total += 1;
    if (ans.isCorrect === true) {
      topicMap[topicName].correct += 1;
    }
    topicMap[topicName].pointsEarned += (ans.effectivePoints ?? ans.pointsEarned ?? 0);
    topicMap[topicName].maxPoints += (ans.maxPoints ?? 1);
  });

  const topics = Object.values(topicMap).map((t) => {
    let percent = 0;
    if (t.maxPoints > 0) {
      percent = Math.round((t.pointsEarned / t.maxPoints) * 100);
    } else if (t.total > 0) {
      percent = Math.round((t.correct / t.total) * 100);
    }
    percent = Math.max(0, Math.min(100, percent));

    let level = 'good';
    if (percent < 50) level = 'weak';
    else if (percent < 80) level = 'improve';

    return { name: t.name, percent, level, correct: t.correct, total: t.total };
  });

  return (
    <div className="bg-pure-surface rounded-xl p-8 border border-whisper-border">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-xl font-semibold text-on-surface">Phân tích chủ đề</h3>
      </div>

      {topics.length === 0 ? (
        <div className="text-center py-8 text-outline text-sm">
          Chưa có thông tin phân tích chủ đề cho bài làm này.
        </div>
      ) : (
        <div className="space-y-6">
          {topics.map((topic) => (
            <div key={topic.name}>
              <div className="flex justify-between mb-2">
                <span className="text-sm font-medium text-on-surface">{topic.name}</span>
                <span className={`text-sm font-bold ${getLabelColor(topic.level)}`}>
                  {topic.percent}% ({topic.correct}/{topic.total} đúng)
                </span>
              </div>
              <ProgressBar
                value={topic.percent}
                max={100}
                height="h-2.5"
                colorClass={getBarColor(topic.level)}
                trackClass="bg-surface-container"
              />
            </div>
          ))}
        </div>
      )}

      {/* Legend */}
      <div className="mt-8 pt-6 border-t border-whisper-border flex items-center gap-4 text-sm text-on-surface-variant">
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-primary" /> Tốt (≥80%)
        </span>
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-primary/60" /> Cần cải thiện (50-79%)
        </span>
        <span className="flex items-center gap-1">
          <span className="w-3 h-3 rounded-full bg-deep-rose" /> Yếu (&lt;50%)
        </span>
      </div>
    </div>
  );
}
