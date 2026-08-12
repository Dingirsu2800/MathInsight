using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

internal static class TopicPracticePersistenceVerifier
{
    // Keeps the prior baseline verifier available for smoke coverage created before WeakTag audit existed.
    public static bool IsValid(
        Test test,
        string studentId,
        string selectedTagId,
        string expectedTestName) =>
        IsValid(
            test,
            new PreparedTopicPracticeGeneration(
                test.TestId,
                studentId,
                selectedTagId,
                string.Empty,
                expectedTestName,
                test.CreatedTime,
                TopicPracticeRecommendationContext.Baseline,
                null,
                TopicPracticeDifficultySelectionModes.Recommended,
                null,
                null,
                null,
                test.Questions
                    .Select(question => new PreparedTopicPracticeQuestion(
                        new BlueprintExamCandidate(
                            question.QuestionId,
                            question.QuestionVersionId,
                            question.WeightSnapshot,
                            string.Empty,
                            string.Empty,
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                            new HashSet<string>([question.ScoringRuleSnapshot], StringComparer.OrdinalIgnoreCase)),
                        question.QuestionOrder,
                        question.MaxPointsSnapshot,
                        question.ScoringRuleSnapshot,
                        IsWeakTagFocus: false))
                    .ToList()));

    public static bool IsValid(Test test, PreparedTopicPracticeGeneration prepared)
    {
        var questions = test.Questions.ToList();
        var preparedQuestions = prepared.Questions.ToList();
        var preparedQuestionIds = preparedQuestions
            .Select(question => question.Question.QuestionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validScoringRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ScoringRules.AllOrNothing,
            ScoringRules.TieredTrueFalse,
            ScoringRules.WeightedParts
        };

        var aggregateIsValid = string.Equals(test.TestId, prepared.TestId, StringComparison.Ordinal) &&
            test.CreatedTime == prepared.CreatedTime &&
            test.BlueprintId is null &&
            string.Equals(test.GeneratedForStudentId, prepared.StudentId, StringComparison.Ordinal) &&
            string.Equals(test.TestStatus, GeneratedTestValues.ActiveStatus, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestMode, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.GeneratedBy, GeneratedTestValues.SystemGenerator, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestName, prepared.TestName, StringComparison.Ordinal) &&
            test.TestCode is null &&
            test.DurationMinutes == 0 &&
            test.TotalQuestions == TopicPracticePolicy.QuestionCount &&
            questions.Count == TopicPracticePolicy.QuestionCount &&
            preparedQuestions.Count == TopicPracticePolicy.QuestionCount &&
            preparedQuestionIds.Count == TopicPracticePolicy.QuestionCount &&
            test.MaxScore == TopicPracticePolicy.MaxScore &&
            string.Equals(test.ScoringPolicy, ScoringPolicies.NormalizedWeight, StringComparison.OrdinalIgnoreCase) &&
            questions.Select(question => question.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == TopicPracticePolicy.QuestionCount &&
            questions.Select(question => question.QuestionOrder).OrderBy(order => order).SequenceEqual(Enumerable.Range(1, TopicPracticePolicy.QuestionCount)) &&
            questions.Sum(question => question.MaxPointsSnapshot) == TopicPracticePolicy.MaxScore &&
            questions.All(question =>
                question.SourceBlueprintDetailId is null &&
                string.Equals(question.TestId, test.TestId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(question.QuestionVersionId) &&
                question.WeightSnapshot > 0m &&
                question.MaxPointsSnapshot > 0m &&
                validScoringRules.Contains(question.ScoringRuleSnapshot) &&
                !question.IsScoreInvalidated &&
                question.InvalidatedByReportId is null);

        if (!aggregateIsValid)
            return false;

        var persistedMatchesPrepared = preparedQuestions.All(preparedQuestion =>
        {
            var persisted = questions.SingleOrDefault(question =>
                string.Equals(
                    question.QuestionId,
                    preparedQuestion.Question.QuestionId,
                    StringComparison.OrdinalIgnoreCase));
            return persisted is not null &&
                persisted.QuestionOrder == preparedQuestion.QuestionOrder &&
                string.Equals(
                    persisted.QuestionVersionId,
                    preparedQuestion.Question.QuestionVersionId,
                    StringComparison.OrdinalIgnoreCase) &&
                persisted.WeightSnapshot == preparedQuestion.Question.DefaultWeight &&
                persisted.MaxPointsSnapshot == preparedQuestion.MaxPoints &&
                string.Equals(
                    persisted.ScoringRuleSnapshot,
                    preparedQuestion.ScoringRule,
                    StringComparison.OrdinalIgnoreCase);
        });
        if (!persistedMatchesPrepared)
            return false;

        if (string.Equals(prepared.DifficultySelectionMode, TopicPracticeDifficultySelectionModes.Manual, StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(prepared.SelectedDifficultyId) &&
                questions.All(question =>
                    string.Equals(question.SelectionReason, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
                    !question.IsAdaptiveSelected &&
                    string.Equals(question.RecommendedForTagId, prepared.SelectedTagId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(question.RecommendedDifficultyId, prepared.SelectedDifficultyId, StringComparison.OrdinalIgnoreCase) &&
                    question.PtagAtSelection is null &&
                    string.Equals(question.RuleVersion, TopicPracticePolicy.ManualRuleVersion, StringComparison.Ordinal));
        }

        if (!prepared.Recommendation.IsAdaptive)
        {
            return questions.All(question =>
                string.Equals(question.SelectionReason, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
                !question.IsAdaptiveSelected &&
                string.Equals(question.RecommendedForTagId, prepared.SelectedTagId, StringComparison.OrdinalIgnoreCase) &&
                question.RecommendedDifficultyId is null &&
                question.PtagAtSelection is null &&
                string.Equals(question.RuleVersion, TopicPracticePolicy.RuleVersion, StringComparison.Ordinal));
        }

        var advice = prepared.Recommendation.RepresentativeAdvice;
        if (advice is null || string.IsNullOrWhiteSpace(prepared.RecommendedDifficultyId))
            return false;

        if (questions.Any(question => !string.Equals(question.RuleVersion, TopicPracticePolicy.WeakTagRuleVersion, StringComparison.Ordinal)))
            return false;

        var focusQuestionIds = prepared.Questions
            .Where(question => question.IsWeakTagFocus)
            .Select(question => question.Question.QuestionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return questions.All(question =>
        {
            var isFocus = focusQuestionIds.Contains(question.QuestionId);
            return isFocus
                ? string.Equals(question.SelectionReason, "WeakTagPractice", StringComparison.OrdinalIgnoreCase) &&
                    question.IsAdaptiveSelected &&
                    string.Equals(question.RecommendedForTagId, advice.TagId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(question.RecommendedDifficultyId, prepared.RecommendedDifficultyId, StringComparison.OrdinalIgnoreCase) &&
                    question.PtagAtSelection == advice.OfficialPoint
                : string.Equals(question.SelectionReason, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
                    !question.IsAdaptiveSelected &&
                    string.Equals(question.RecommendedForTagId, prepared.SelectedTagId, StringComparison.OrdinalIgnoreCase) &&
                    question.RecommendedDifficultyId is null &&
                    question.PtagAtSelection is null;
        });
    }
}
