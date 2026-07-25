using System.Text.Json;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Shared.Questions;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

internal static class QuestionVersionSnapshotFactory
{
    public const short CurrentSchemaVersion = 2;

    public static QuestionVersion Create(
        Question question,
        string expertId,
        int versionNumber,
        DateTime createdTime)
    {
        var snapshot = new QuestionSnapshotV2(
            question.QuestionId,
            question.QuestionType,
            question.DifficultyId,
            question.Grade,
            question.DefaultWeight,
            question.QuestionTopics
                .OrderByDescending(topic => topic.IsPrimary)
                .ThenBy(topic => topic.TagId)
                .Select(topic => new QuestionTopicSnapshot(topic.TagId, topic.IsPrimary))
                .ToList(),
            question.Answers
                .Where(answer => !answer.IsArchived)
                .OrderBy(answer => answer.AnswerId)
                .Select(answer => new QuestionAnswerSnapshot(
                    answer.AnswerId,
                    answer.AnswerContent,
                    answer.IsCorrect))
                .ToList(),
            question.Parts
                .Where(part => !part.IsArchived)
                .OrderBy(part => part.PartOrder)
                .Select(part => new QuestionPartSnapshot(
                    part.PartId,
                    part.PartOrder,
                    part.PartLabel,
                    part.PartContent,
                    part.PartType,
                    part.CorrectBoolean,
                    part.CorrectText,
                    part.CorrectNumeric,
                    part.NumericTolerance,
                    part.Explanation,
                    part.DefaultWeight))
                .ToList(),
            question.QuestionContent,
            question.SolutionContent,
            question.PictureUrl);

        return new QuestionVersion
        {
            VersionId = Guid.NewGuid().ToString("D"),
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = question.SolutionContent,
            AnswersSnapshot = JsonSerializer.Serialize(snapshot),
            PictureUrl = question.PictureUrl,
            VersionNumber = versionNumber,
            SnapshotSchemaVersion = CurrentSchemaVersion,
            CreatedTime = createdTime,
            ExpertId = expertId
        };
    }
}
