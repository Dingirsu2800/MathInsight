using System.Text.Json;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Shared.Questions;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Persistence;

internal static class QuestionSnapshotReader
{
    public static async Task<IReadOnlyDictionary<string, TestQuestionSnapshot>> LoadAsync(
        TestingDbContext db,
        string testId,
        CancellationToken cancellationToken)
    {
        var rows = await db.TestQuestions
            .AsNoTracking()
            .Include(item => item.QuestionVersion)
            .Where(item => item.TestId == testId)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            item => item.QuestionId,
            item => new TestQuestionSnapshot(item, Deserialize(item)),
            StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsValid(TestQuestionSnapshot row, AutoSaveAnswerDto answer)
    {
        var snapshot = row.Snapshot;
        if (answer.AnswerId is not null &&
            snapshot.Answers.All(item => !string.Equals(
                item.AnswerId,
                answer.AnswerId,
                StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (answer.SelectedOptions?.Any(option => snapshot.Answers.All(item =>
                !string.Equals(item.AnswerId, option.AnswerId, StringComparison.OrdinalIgnoreCase))) == true)
        {
            return false;
        }

        return answer.Parts?.Any(part => snapshot.Parts.All(item =>
            !string.Equals(item.PartId, part.PartId, StringComparison.OrdinalIgnoreCase))) != true;
    }

    public static bool IsValid(TestQuestionSnapshot row, TestAnswer answer)
    {
        var snapshot = row.Snapshot;
        if (answer.AnswerId is not null && snapshot.Answers.All(item =>
                !string.Equals(item.AnswerId, answer.AnswerId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (answer.Options.Any(option => snapshot.Answers.All(item =>
                !string.Equals(item.AnswerId, option.AnswerId, StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        return answer.Parts.All(part => snapshot.Parts.Any(item =>
            string.Equals(item.PartId, part.PartId, StringComparison.OrdinalIgnoreCase)));
    }

    private static QuestionSnapshotV2 Deserialize(TestQuestion testQuestion)
    {
        var version = testQuestion.QuestionVersion
            ?? throw new InvalidOperationException(
                $"Question version '{testQuestion.QuestionVersionId}' was not loaded.");
        if (version.SnapshotSchemaVersion != 2)
            throw new InvalidOperationException(
                $"Unsupported snapshot schema for version '{version.VersionId}'.");

        return JsonSerializer.Deserialize<QuestionSnapshotV2>(version.AnswersSnapshot)
            ?? throw new InvalidOperationException(
                $"Invalid snapshot JSON for version '{version.VersionId}'.");
    }
}

internal sealed record TestQuestionSnapshot(
    TestQuestion TestQuestion,
    QuestionSnapshotV2 Snapshot);
