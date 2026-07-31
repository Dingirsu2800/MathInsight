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
                []));

    public static bool IsValid(Test test, PreparedTopicPracticeGeneration prepared)
    {
        var questions = test.Questions.ToList();
        var validScoringRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ScoringRules.AllOrNothing,
            ScoringRules.TieredTrueFalse,
            ScoringRules.WeightedParts
        };

        var aggregateIsValid = test.BlueprintId is null &&
            string.Equals(test.GeneratedForStudentId, prepared.StudentId, StringComparison.Ordinal) &&
            string.Equals(test.TestStatus, GeneratedTestValues.ActiveStatus, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestMode, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.GeneratedBy, GeneratedTestValues.SystemGenerator, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestName, prepared.TestName, StringComparison.Ordinal) &&
            test.TestCode is null &&
            test.DurationMinutes == 0 &&
            test.TotalQuestions == TopicPracticePolicy.QuestionCount &&
            questions.Count == TopicPracticePolicy.QuestionCount &&
            test.MaxScore == TopicPracticePolicy.MaxScore &&
            string.Equals(test.ScoringPolicy, ScoringPolicies.NormalizedWeight, StringComparison.OrdinalIgnoreCase) &&
            questions.Select(question => question.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == TopicPracticePolicy.QuestionCount &&
            questions.Select(question => question.QuestionOrder).OrderBy(order => order).SequenceEqual(Enumerable.Range(1, TopicPracticePolicy.QuestionCount)) &&
            questions.Sum(question => question.MaxPointsSnapshot) == TopicPracticePolicy.MaxScore &&
            questions.All(question =>
                question.SourceBlueprintDetailId is null &&
                !string.IsNullOrWhiteSpace(question.QuestionVersionId) &&
                question.WeightSnapshot > 0m &&
                question.MaxPointsSnapshot > 0m &&
                validScoringRules.Contains(question.ScoringRuleSnapshot) &&
                !question.IsScoreInvalidated &&
                question.InvalidatedByReportId is null);

        if (!aggregateIsValid)
            return false;

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
