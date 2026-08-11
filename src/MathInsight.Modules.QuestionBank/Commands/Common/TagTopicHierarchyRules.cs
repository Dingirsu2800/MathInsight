using MathInsight.Modules.QuestionBank.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.Common;

internal static class TagTopicHierarchyRules
{
    public static async Task<bool> HasActiveDescendantAsync(
        QuestionBankDbContext context,
        string rootTagId,
        CancellationToken cancellationToken)
    {
        var topics = await context.TagTopics
            .AsNoTracking()
            .Select(topic => new TopicNode(topic.TagId, topic.ParentTagId, topic.IsActive))
            .ToListAsync(cancellationToken);

        var childrenByParentId = topics
            .Where(topic => topic.ParentTagId is not null)
            .GroupBy(topic => topic.ParentTagId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var pendingTagIds = new Stack<string>();
        var visitedTagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootTagId };
        pendingTagIds.Push(rootTagId);

        while (pendingTagIds.Count > 0)
        {
            var currentTagId = pendingTagIds.Pop();

            if (!childrenByParentId.TryGetValue(currentTagId, out var children))
                continue;

            foreach (var child in children)
            {
                if (!visitedTagIds.Add(child.TagId))
                    continue;

                if (child.IsActive)
                    return true;

                pendingTagIds.Push(child.TagId);
            }
        }

        return false;
    }

    /// <summary>
    /// An active child under an inactive (or orphaned) ancestor cannot be reached from
    /// the active topic tree. Treat that lineage as invalid for create/reactivation.
    /// </summary>
    public static async Task<bool> HasInactiveOrMissingAncestorAsync(
        QuestionBankDbContext context,
        string parentTagId,
        CancellationToken cancellationToken)
    {
        return await AnyHasInactiveOrMissingAncestorAsync(
            context,
            [parentTagId],
            cancellationToken);
    }

    public static async Task<bool> AnyHasInactiveOrMissingAncestorAsync(
        QuestionBankDbContext context,
        IEnumerable<string> tagIds,
        CancellationToken cancellationToken)
    {
        var topicsById = await context.TagTopics
            .AsNoTracking()
            .Select(topic => new TopicNode(topic.TagId, topic.ParentTagId, topic.IsActive))
            .ToDictionaryAsync(topic => topic.TagId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var tagId in tagIds)
        {
            var visitedTagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? currentTagId = tagId;

            while (currentTagId is not null)
            {
                if (!visitedTagIds.Add(currentTagId) ||
                    !topicsById.TryGetValue(currentTagId, out var currentTopic) ||
                    !currentTopic.IsActive)
                {
                    return true;
                }

                currentTagId = currentTopic.ParentTagId;
            }
        }

        return false;
    }

    private sealed record TopicNode(string TagId, string? ParentTagId, bool IsActive);
}
