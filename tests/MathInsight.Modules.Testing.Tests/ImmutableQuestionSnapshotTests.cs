using System.Text.Json;
using MathInsight.Modules.Testing.Commands.AutoSave;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Queries.GetSessionContent;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Tests;

public sealed class ImmutableQuestionSnapshotTests
{
    [Fact]
    public async Task GetSessionContent_ReturnsImmutableQuestionsWithoutCorrectFlags()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new Commands.StartSession.StartSessionCommandHandler(db).Handle(
            new Commands.StartSession.StartSessionCommand(
                TestDataSeeder.ActiveTestId,
                TestDataSeeder.StudentId),
            CancellationToken.None);

        var result = await new GetSessionContentQueryHandler(db).Handle(
            new GetSessionContentQuery(start.Value!.SessionId, TestDataSeeder.StudentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Questions.Count);
        Assert.All(result.Value.Questions, question => Assert.NotEmpty(question.QuestionVersionId));
        Assert.DoesNotContain("IsCorrect", JsonSerializer.Serialize(result.Value), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AutoSave_RejectsAnswerFromAnotherQuestionVersion()
    {
        await using var context = TestingInMemoryContext.Create();
        var db = context.Context;
        await TestDataSeeder.SeedActiveTestWithQuestions(db);
        var start = await new Commands.StartSession.StartSessionCommandHandler(db).Handle(
            new Commands.StartSession.StartSessionCommand(
                TestDataSeeder.ActiveTestId,
                TestDataSeeder.StudentId),
            CancellationToken.None);

        var result = await new AutoSaveCommandHandler(db).Handle(
            new AutoSaveCommand(
                start.Value!.SessionId,
                TestDataSeeder.StudentId,
                [new AutoSaveAnswerDto(TestDataSeeder.Question1Id, "ans-2", null, 1, null, null)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ANSWER_NOT_IN_TEST_VERSION", result.Error!.Code);
        var stored = await db.TestAnswers.SingleAsync(item => item.QuestionId == TestDataSeeder.Question1Id);
        Assert.Null(stored.AnswerId);
    }
}
