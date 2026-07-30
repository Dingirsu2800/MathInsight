using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicTreeResolverTests
{
    [Fact]
    public void Resolve_IncludesSelectedTopicAndActiveDescendants()
    {
        var result = TopicTreeResolver.ResolveActiveSubtree("root", [Topic("root"), Topic("child", "root"), Topic("grandchild", "child")]);
        Assert.Equal(["child", "grandchild", "root"], result.OrderBy(id => id));
    }

    [Fact]
    public void Resolve_ExcludesInactiveDescendants()
    {
        var result = TopicTreeResolver.ResolveActiveSubtree("root", [Topic("root"), Topic("active", "root"), Topic("inactive", "root", false), Topic("hidden", "inactive")]);
        Assert.Equal(["active", "root"], result.OrderBy(id => id));
    }

    [Fact]
    public void Resolve_TerminatesAndReturnsUniqueIds_WhenTaxonomyContainsCycle()
    {
        var result = TopicTreeResolver.ResolveActiveSubtree("a", [Topic("a", "b"), Topic("b", "a")]);
        Assert.Equal(["a", "b"], result.OrderBy(id => id));
    }

    private static TagTopicReadModel Topic(string tagId, string? parentId = null, bool active = true) => new() { TagId = tagId, ParentTagId = parentId, IsActive = active };
}
