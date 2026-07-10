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

    private sealed record TopicNode(string TagId, string? ParentTagId, bool IsActive);
}
