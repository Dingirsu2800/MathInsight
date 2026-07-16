using MathInsight.Modules.Grading_Analytics.Handlers;
using MathInsight.Modules.Grading_Analytics.Services;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Grading_Analytics.Tests;

public sealed class GradeCalculatedEventContractTests
{
    [Fact]
    public void Producer_UsesStringIdsAndWeightedTopicScore()
    {
        var tagId = Guid.NewGuid();
        var session = TestDataBuilder.CreateSession(testFormat: "Exam");
        TestDataBuilder.AddCompositeAllTrueFalse(
            session,
            defaultPoint: 4m,
            [
                ("True", "True"),
                ("True", "True"),
                ("True", "True"),
                ("True", "False")
            ],
            primaryTagId: tagId);
        var correctAnswerId = Guid.NewGuid();
        TestDataBuilder.AddSingleChoiceAnswer(
            session,
            defaultPoint: 1m,
            correctAnswerId,
            correctAnswerId,
            primaryTagId: tagId);

        var gradingResult = new GradingEngine().Grade(session);
        var evt = GradeSubmittedSessionHandler.BuildGradeCalculatedEvent(
            session,
            gradingResult,
            new TestSubmittedEvent());

        Assert.Equal(session.SessionId.ToString("D"), evt.SessionId);
        Assert.Equal(session.StudentId.ToString("D"), evt.StudentId);
        Assert.Equal(session.TestId.ToString("D"), evt.TestId);

        var topic = Assert.Single(evt.PerTagResults);
        Assert.Equal(tagId.ToString("D"), topic.TagId);
        Assert.Equal(2m, topic.TotalItems);
        Assert.Equal(1m, topic.CorrectItems);
        Assert.Equal(3m, topic.EarnedPoints);
        Assert.Equal(5m, topic.MaxPoints);
        Assert.Equal(6m, topic.TopicScore);

        Assert.All(evt.Answers, answer =>
        {
            Assert.False(string.IsNullOrWhiteSpace(answer.QuestionId));
            Assert.Equal(tagId.ToString("D"), answer.TagId);
            Assert.True(answer.MaxPoints > 0m);
        });
    }
}
