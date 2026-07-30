using MathInsight.Modules.TestGen.Persistence.ReadModels;

namespace MathInsight.Modules.TestGen.Generation;

public static class TopicTreeResolver
{
    public static IReadOnlySet<string> ResolveActiveSubtree(
        string selectedTagId,
        IReadOnlyCollection<TagTopicReadModel> topics)
    {
        var activeTopics = topics.Where(topic => topic.IsActive).ToList();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(selectedTagId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            foreach (var child in activeTopics.Where(topic => string.Equals(topic.ParentTagId, current, StringComparison.OrdinalIgnoreCase)))
                queue.Enqueue(child.TagId);
        }

        return visited;
    }
}
