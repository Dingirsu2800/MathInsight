using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Recommendations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeRecommendationResolverTests
{
    private readonly Mock<IStudentTopicMasteryProvider> _provider = new();

    [Fact]
    public async Task ResolveForTopicsAsync_Disabled_DoesNotCallProviderAndReturnsBaselineContexts()
    {
        var result = await CreateResolver(false).ResolveForTopicsAsync("student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Values, context => Assert.False(context.IsAdaptive));
        _provider.Verify(provider => provider.GetTopicMasteryAdviceAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_MapsSufficientEvidenceOnlyToTheExactRequestedTopic()
    {
        SetupAdvice(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            ["child"] = new("child", 5.50m, 3, 3)
        });

        var result = await CreateResolver(true).ResolveForTopicsAsync("student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!["parent"].IsAdaptive);
        Assert.True(result.Value!["child"].IsAdaptive);
        Assert.Equal(5.50m, result.Value!["child"].RepresentativeAdvice!.OfficialPoint);
        Assert.False(result.Value!["sibling"].IsAdaptive);
        _provider.Verify(provider => provider.GetTopicMasteryAdviceAsync(
            "student_01",
            It.Is<IReadOnlyCollection<string>>(ids => ids.Count == 3 && ids.Contains("child")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_InsufficientEvidence_UsesBaseline()
    {
        SetupAdvice(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            ["child"] = new("child", 1.00m, 2, 1)
        });

        var result = await CreateResolver(true).ResolveForTopicsAsync("student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!["child"].IsAdaptive);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_ProviderFailure_ReturnsStableUnavailableError()
    {
        _provider.Setup(provider => provider.GetTopicMasteryAdviceAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("provider unavailable"));

        var result = await CreateResolver(true).ResolveForTopicsAsync("student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", result.Error!.Code);
    }

    [Theory]
    [InlineData("unknown", "unknown", 5.00, 3, 2)]
    [InlineData("child", "other", 5.00, 3, 2)]
    [InlineData("child", "child", -0.01, 3, 2)]
    [InlineData("child", "child", 10.01, 3, 2)]
    [InlineData("child", "child", 5.00, -1, 2)]
    [InlineData("child", "child", 5.00, 3, 5)]
    public async Task ResolveForTopicsAsync_InvalidAdvice_ReturnsStableInvalidError(
        string dictionaryKey, string tagId, decimal point, int evidence, byte level)
    {
        SetupAdvice(new Dictionary<string, TopicMasteryAdvice>(StringComparer.OrdinalIgnoreCase)
        {
            [dictionaryKey] = new(tagId, point, evidence, level)
        });

        var result = await CreateResolver(true).ResolveForTopicsAsync("student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDATION_INVALID", result.Error!.Code);
    }

    private void SetupAdvice(IReadOnlyDictionary<string, TopicMasteryAdvice> advice) =>
        _provider.Setup(provider => provider.GetTopicMasteryAdviceAsync(
                "student_01", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(advice);

    private TopicPracticeRecommendationResolver CreateResolver(bool enabled) => new(
        _provider.Object,
        Options.Create(new TopicPracticeFeatureOptions { WeakTagAdaptiveEnabled = enabled }),
        Mock.Of<ILogger<TopicPracticeRecommendationResolver>>());

    private static IReadOnlyCollection<TagTopicReadModel> Topics() =>
    [
        Topic("parent", null, 1),
        Topic("child", "parent", 2),
        Topic("sibling", null, 3)
    ];

    private static TagTopicReadModel Topic(string tagId, string? parentTagId, int displayOrder) => new()
    {
        TagId = tagId, ParentTagId = parentTagId, TagName = tagId, Grade = 12, IsActive = true, DisplayOrder = displayOrder
    };
}
