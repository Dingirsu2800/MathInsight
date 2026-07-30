using MathInsight.Modules.TestGen.Commands.GenerateTopicPractice;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticePersistenceVerifierTests
{
    [Fact]
    public void IsValid_AcceptsCompletePersistedAggregate()
    {
        var test = CreateValidTest();

        var result = TopicPracticePersistenceVerifier.IsValid(
            test,
            "student_01",
            "topic_01",
            "Luyen tap");

        Assert.True(result);
    }

    [Fact]
    public void IsValid_RejectsWrongOrderOrScoreOrAuditData()
    {
        var wrongOrder = CreateValidTest();
        wrongOrder.Questions.Last().QuestionOrder = 9;

        var wrongScore = CreateValidTest();
        wrongScore.Questions.Last().MaxPointsSnapshot = 0.99m;

        var wrongAudit = CreateValidTest();
        wrongAudit.Questions.Last().RecommendedDifficultyId = "difficulty_01";

        Assert.False(TopicPracticePersistenceVerifier.IsValid(wrongOrder, "student_01", "topic_01", "Luyen tap"));
        Assert.False(TopicPracticePersistenceVerifier.IsValid(wrongScore, "student_01", "topic_01", "Luyen tap"));
        Assert.False(TopicPracticePersistenceVerifier.IsValid(wrongAudit, "student_01", "topic_01", "Luyen tap"));
    }

    private static Test CreateValidTest()
    {
        var test = new Test
        {
            TestId = "test_01",
            BlueprintId = null,
            TestStatus = "Active",
            TestMode = "TopicPractice",
            GeneratedForStudentId = "student_01",
            GeneratedBy = "System",
            TestName = "Luyen tap",
            DurationMinutes = 0,
            TotalQuestions = 10,
            MaxScore = 10m,
            ScoringPolicy = "NormalizedWeight"
        };

        for (var index = 1; index <= 10; index++)
        {
            test.Questions.Add(new TestQuestion
            {
                TestId = test.TestId,
                QuestionId = $"question_{index:00}",
                QuestionOrder = index,
                SelectionReason = "TopicPractice",
                IsAdaptiveSelected = false,
                RecommendedForTagId = "topic_01",
                RecommendedDifficultyId = null,
                RuleVersion = "TopicPractice-v1",
                QuestionVersionId = $"version_{index:00}",
                WeightSnapshot = 1m,
                MaxPointsSnapshot = 1m,
                ScoringRuleSnapshot = "AllOrNothing"
            });
        }

        return test;
    }
}
