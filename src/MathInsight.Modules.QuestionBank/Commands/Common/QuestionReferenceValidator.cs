using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

/// <summary>
/// Validates the active tag references used by manual question create/update.
/// Import has its own row-level validation; this guard protects the API path.
/// </summary>
public static class QuestionReferenceValidator
{
    public static async Task<Error?> ValidateAsync(
        QuestionBankDbContext context,
        CreateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var difficultyIsActive = await context.TagDifficulties.AnyAsync(
            difficulty => difficulty.DifficultyId == request.DifficultyId && difficulty.IsActive,
            cancellationToken);

        if (!difficultyIsActive)
            return QuestionBankErrors.QuestionDifficultyNotFound;

        var topicIds = request.Topics
            .Select(topic => topic.TagId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var activeTopics = await context.TagTopics
            .Where(topic => topicIds.Contains(topic.TagId) && topic.IsActive)
            .Select(topic => new { topic.TagId, topic.Grade })
            .ToListAsync(cancellationToken);

        if (activeTopics.Count != topicIds.Count || activeTopics.Any(topic => topic.Grade != request.Grade))
            return QuestionBankErrors.QuestionTopicNotFound;

        if (await TagTopicHierarchyRules.AnyHasInactiveOrMissingAncestorAsync(
                context,
                activeTopics.Select(topic => topic.TagId),
                cancellationToken))
        {
            return QuestionBankErrors.QuestionTopicNotFound;
        }

        return null;
    }
}
