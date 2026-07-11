using MediatR;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Services;

namespace MathInsight.Modules.Recommender.Queries.GetWeakTags;

/// <summary>
/// Handles <see cref="GetWeakTagsQuery"/> by delegating to <see cref="IRecommenderService"/>.
/// UC-52: View WeakTags.
/// </summary>
public sealed class GetWeakTagsQueryHandler
    : IRequestHandler<GetWeakTagsQuery, IReadOnlyList<WeakTagDto>>
{
    private readonly IRecommenderService _recommenderService;

    public GetWeakTagsQueryHandler(IRecommenderService recommenderService)
    {
        _recommenderService = recommenderService;
    }

    public async Task<IReadOnlyList<WeakTagDto>> Handle(
        GetWeakTagsQuery request, CancellationToken cancellationToken)
    {
        return await _recommenderService.GetStudentWeakTagsAsync(
            request.StudentId, cancellationToken);
    }
}
