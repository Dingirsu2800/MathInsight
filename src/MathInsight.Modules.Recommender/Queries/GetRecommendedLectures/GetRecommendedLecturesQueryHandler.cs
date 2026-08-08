using MathInsight.Modules.Recommender.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;

public sealed class GetRecommendedLecturesQueryHandler
    : IRequestHandler<GetRecommendedLecturesQuery, IReadOnlyList<RecommendedLectureResponse>>
{
    private const decimal WeakThreshold = 5.00m;
    private const decimal ProgressionThreshold = 7.50m;
    private const int MinimumEvidenceCount = 3;
    private const int MaximumPerTopic = 2;
    private const int MaximumRecommendations = 6;

    private readonly RecommenderDbContext _db;

    public GetRecommendedLecturesQueryHandler(RecommenderDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RecommendedLectureResponse>> Handle(
        GetRecommendedLecturesQuery request,
        CancellationToken cancellationToken)
    {
        var contexts = await (
            from mastery in _db.TagsMasteries.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on mastery.TagId equals topic.TagId
            where mastery.StudentId == request.StudentId
                && mastery.NumberDone >= MinimumEvidenceCount
                && topic.IsActive
            select new
            {
                mastery.TagId,
                topic.TagName,
                mastery.OfficialPoint,
                mastery.NumberDone,
                mastery.RecommendedDifficultyLevel
            })
            .ToListAsync(cancellationToken);

        var lectureRows = await (
            from lecture in _db.Lectures.AsNoTracking()
            join topic in _db.TagTopics.AsNoTracking() on lecture.TagId equals topic.TagId
            join difficulty in _db.TagDifficulties.AsNoTracking() on lecture.DifficultyId equals difficulty.DifficultyId
            where lecture.Status == "Published"
                && topic.IsActive
                && difficulty.IsActive
            select new
            {
                lecture.LectureId,
                lecture.Title,
                lecture.ThumbnailUrl,
                lecture.TagId,
                topic.TagName,
                lecture.DifficultyId,
                difficulty.DifficultyName,
                DifficultyLevel = difficulty.LevelValue,
                lecture.Likes,
                lecture.UpdatedTime,
                TopicGrade = topic.Grade
            })
            .ToListAsync(cancellationToken);

        var lectures = lectureRows
            .Select(x => new LectureCandidate(
                x.LectureId,
                x.Title,
                x.ThumbnailUrl,
                x.TagId,
                x.TagName,
                x.DifficultyId!,
                x.DifficultyName,
                x.DifficultyLevel,
                x.Likes,
                x.UpdatedTime,
                x.TopicGrade))
            .ToList();

        if (contexts.Count == 0)
            return await BuildColdStartRecommendationsAsync(request.StudentId, lectures, cancellationToken);

        var recommendations = new List<RecommendedLectureResponse>();

        foreach (var context in contexts
            .OrderBy(x => GetPriority(x.OfficialPoint))
            .ThenBy(x => x.OfficialPoint)
            .ThenByDescending(x => x.NumberDone)
            .ThenBy(x => x.TagId))
        {
            var priority = GetPriority(context.OfficialPoint);
            var candidates = lectures
                .Where(x => x.TagId == context.TagId && x.DifficultyLevel <= context.RecommendedDifficultyLevel)
                .OrderBy(x => x.DifficultyLevel == context.RecommendedDifficultyLevel ? 0 : 1)
                .ThenByDescending(x => x.DifficultyLevel)
                .ThenByDescending(x => x.Likes)
                .ThenByDescending(x => x.UpdatedTime)
                .ThenBy(x => x.LectureId)
                .Take(MaximumPerTopic);

            foreach (var lecture in candidates)
            {
                var isFallback = lecture.DifficultyLevel < context.RecommendedDifficultyLevel;
                recommendations.Add(new RecommendedLectureResponse(
                    lecture.LectureId,
                    lecture.Title,
                    lecture.ThumbnailUrl,
                    lecture.TagId,
                    lecture.TagName,
                    lecture.DifficultyId!,
                    lecture.DifficultyName,
                    lecture.DifficultyLevel,
                    context.RecommendedDifficultyLevel,
                    context.OfficialPoint,
                    context.NumberDone,
                    lecture.Likes,
                    isFallback,
                    BuildReason(priority, isFallback)));

                if (recommendations.Count == MaximumRecommendations)
                    return recommendations;
            }
        }

        return recommendations;
    }

    private async Task<IReadOnlyList<RecommendedLectureResponse>> BuildColdStartRecommendationsAsync(
        string studentId,
        IReadOnlyList<LectureCandidate> lectures,
        CancellationToken cancellationToken)
    {
        var grade = await _db.Students
            .AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Select(x => x.CurrentGrade)
            .SingleOrDefaultAsync(cancellationToken);

        if (!grade.HasValue)
            return [];

        return lectures
            .Where(x => x.TopicGrade == grade.Value && x.DifficultyLevel == 1)
            .GroupBy(x => x.TagId)
            .SelectMany(group => group
                .OrderByDescending(x => x.Likes)
                .ThenByDescending(x => x.UpdatedTime)
                .ThenBy(x => x.LectureId)
                .Take(MaximumPerTopic))
            .OrderByDescending(x => x.Likes)
            .ThenByDescending(x => x.UpdatedTime)
            .ThenBy(x => x.LectureId)
            .Take(MaximumRecommendations)
            .Select(lecture => new RecommendedLectureResponse(
                lecture.LectureId,
                lecture.Title,
                lecture.ThumbnailUrl,
                lecture.TagId,
                lecture.TagName,
                lecture.DifficultyId,
                lecture.DifficultyName,
                lecture.DifficultyLevel,
                1,
                null,
                0,
                lecture.Likes,
                false,
                "ColdStartGradeFoundation"))
            .ToList();
    }

    private static int GetPriority(decimal officialPoint) => officialPoint < WeakThreshold
        ? 0
        : officialPoint < ProgressionThreshold ? 1 : 2;

    private static string BuildReason(int priority, bool isFallback) => priority == 0
        ? isFallback ? "WeakTopicLowerDifficultyFallback" : "WeakTopicExactDifficulty"
        : isFallback ? "ProgressionLowerDifficultyFallback" : "ProgressionExactDifficulty";

    private sealed record LectureCandidate(
        string LectureId,
        string Title,
        string? ThumbnailUrl,
        string TagId,
        string TagName,
        string DifficultyId,
        string DifficultyName,
        int DifficultyLevel,
        int Likes,
        DateTime UpdatedTime,
        int TopicGrade);
}
