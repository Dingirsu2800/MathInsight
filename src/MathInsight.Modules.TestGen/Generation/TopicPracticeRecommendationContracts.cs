using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.TestGen.Generation;

public sealed record TopicPracticeRecommendationContext(
    bool IsAdaptive,
    TopicMasteryAdvice? RepresentativeAdvice,
    IReadOnlySet<string> FocusTagIds)
{
    public static TopicPracticeRecommendationContext Baseline { get; } = new(
        false,
        null,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
