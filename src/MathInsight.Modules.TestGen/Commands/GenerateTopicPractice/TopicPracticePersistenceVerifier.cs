using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;

internal static class TopicPracticePersistenceVerifier
{
    public static bool IsValid(
        Test test,
        string studentId,
        string selectedTagId,
        string expectedTestName)
    {
        var questions = test.Questions.ToList();
        var orders = questions
            .Select(question => question.QuestionOrder)
            .OrderBy(order => order)
            .ToList();
        var validScoringRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ScoringRules.AllOrNothing,
            ScoringRules.TieredTrueFalse,
            ScoringRules.WeightedParts
        };

        return test.BlueprintId is null &&
            string.Equals(test.GeneratedForStudentId, studentId, StringComparison.Ordinal) &&
            string.Equals(test.TestStatus, GeneratedTestValues.ActiveStatus, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestMode, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.GeneratedBy, GeneratedTestValues.SystemGenerator, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(test.TestName, expectedTestName, StringComparison.Ordinal) &&
            test.TestCode is null &&
            test.DurationMinutes == 0 &&
            test.TotalQuestions == TopicPracticePolicy.QuestionCount &&
            questions.Count == TopicPracticePolicy.QuestionCount &&
            test.MaxScore == TopicPracticePolicy.MaxScore &&
            string.Equals(test.ScoringPolicy, ScoringPolicies.NormalizedWeight, StringComparison.OrdinalIgnoreCase) &&
            questions.Select(question => question.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == TopicPracticePolicy.QuestionCount &&
            orders.SequenceEqual(Enumerable.Range(1, TopicPracticePolicy.QuestionCount)) &&
            questions.Sum(question => question.MaxPointsSnapshot) == TopicPracticePolicy.MaxScore &&
            questions.All(question =>
                question.SourceBlueprintDetailId is null &&
                string.Equals(question.SelectionReason, "TopicPractice", StringComparison.OrdinalIgnoreCase) &&
                !question.IsAdaptiveSelected &&
                string.Equals(question.RecommendedForTagId, selectedTagId, StringComparison.OrdinalIgnoreCase) &&
                question.RecommendedDifficultyId is null &&
                question.PtagAtSelection is null &&
                string.Equals(question.RuleVersion, TopicPracticePolicy.RuleVersion, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(question.QuestionVersionId) &&
                question.WeightSnapshot > 0m &&
                question.MaxPointsSnapshot > 0m &&
                validScoringRules.Contains(question.ScoringRuleSnapshot) &&
                !question.IsScoreInvalidated &&
                question.InvalidatedByReportId is null);
    }
}
