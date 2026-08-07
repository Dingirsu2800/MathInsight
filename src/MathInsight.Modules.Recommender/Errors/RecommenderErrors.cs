using MathInsight.Shared.Results;

namespace MathInsight.Modules.Recommender.Errors;

public static class RecommenderErrors
{
    public static readonly Error LectureRecommendationUnavailable = new(
        "LECTURE_RECOMMENDATION_UNAVAILABLE",
        "Lecture recommendations are temporarily unavailable.");
}
