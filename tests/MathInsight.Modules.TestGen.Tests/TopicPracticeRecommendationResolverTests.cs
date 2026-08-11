using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Recommendations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeRecommendationResolverTests
{
    private readonly Mock<IStudentRecommendationProvider> _provider = new();

    [Fact]
    public async Task ResolveForTopicsAsync_Disabled_DoesNotCallProviderAndReturnsBaselineContexts()
    {
        var result = await CreateResolver(enabled: false).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Values, context => Assert.False(context.IsAdaptive));
        _provider.Verify(
            provider => provider.GetWeakTagAdviceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_EmptyAdvice_ReturnsBaselineContexts()
    {
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Values, context => Assert.False(context.IsAdaptive));
    }

    [Fact]
    public async Task ResolveForTopicsAsync_MapsAdviceOnlyToTheExactSelectedTopic()
    {
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeakTagAdvice("child", "Child", 2.40m, 5, 1, "OfficialPointBelow5")
            ]);

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.False(result.Value!["parent"].IsAdaptive);
        Assert.True(result.Value!["child"].IsAdaptive);
        Assert.Equal("child", result.Value!["child"].RepresentativeAdvice!.TagId);
        Assert.False(result.Value!["sibling"].IsAdaptive);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_MapsAdviceOnlyByExactTagId()
    {
        var topics = new[]
        {
            Topic("parent", null, 1),
            Topic("shallow", "parent", 30),
            Topic("middle", "parent", 20),
            Topic("deep", "middle", 10)
        };
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeakTagAdvice("shallow", "Shallow", 2.40m, 5, 1, "OfficialPointBelow5"),
                new WeakTagAdvice("deep", "Deep", 2.40m, 5, 1, "OfficialPointBelow5")
            ]);

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", topics, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Count);
        Assert.False(result.Value!["parent"].IsAdaptive);
        Assert.True(result.Value!["shallow"].IsAdaptive);
        Assert.False(result.Value!["middle"].IsAdaptive);
        Assert.True(result.Value!["deep"].IsAdaptive);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_ProviderFailure_ReturnsStableUnavailableError()
    {
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("provider unavailable"));

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDER_UNAVAILABLE", result.Error!.Code);
    }

    [Theory]
    [InlineData("", "Name", 2.40, 5, 1, "Reason")]
    [InlineData("child", "", 2.40, 5, 1, "Reason")]
    [InlineData("child", "Name", 5.00, 5, 1, "Reason")]
    [InlineData("child", "Name", -0.01, 5, 1, "Reason")]
    [InlineData("child", "Name", 2.40, 2, 1, "Reason")]
    [InlineData("child", "Name", 2.40, 5, 3, "Reason")]
    [InlineData("child", "Name", 2.40, 5, 1, "")]
    public async Task ResolveForTopicsAsync_InvalidAdvice_ReturnsStableInvalidError(
        string tagId,
        string tagName,
        decimal officialPoint,
        int evidenceCount,
        byte recommendedDifficultyLevel,
        string reason)
    {
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeakTagAdvice(tagId, tagName, officialPoint, evidenceCount, recommendedDifficultyLevel, reason)
            ]);

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDATION_INVALID", result.Error!.Code);
    }

    [Fact]
    public async Task ResolveForTopicsAsync_DuplicateAdviceTag_ReturnsStableInvalidError()
    {
        _provider
            .Setup(provider => provider.GetWeakTagAdviceAsync("student_01", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WeakTagAdvice("child", "Child", 2.40m, 5, 1, "Reason"),
                new WeakTagAdvice("CHILD", "Child", 3.00m, 5, 2, "Reason")
            ]);

        var result = await CreateResolver(enabled: true).ResolveForTopicsAsync(
            "student_01", Topics(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TOPIC_PRACTICE_RECOMMENDATION_INVALID", result.Error!.Code);
    }

    private TopicPracticeRecommendationResolver CreateResolver(bool enabled)
        => new(
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
        TagId = tagId,
        ParentTagId = parentTagId,
        TagName = tagId,
        Grade = 12,
        IsActive = true,
        DisplayOrder = displayOrder
    };
}
