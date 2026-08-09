using MediatR;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Services;

namespace MathInsight.Modules.Recommender.Queries.GetAllTagsMastery;

/// <summary>
/// Handles <see cref="GetAllTagsMasteryQuery"/> by delegating to <see cref="IRecommenderService"/>.
/// UC-55: View All Tag Mastery (full competency picture).
/// </summary>
public sealed class GetAllTagsMasteryQueryHandler
    : IRequestHandler<GetAllTagsMasteryQuery, IReadOnlyList<TagMasteryDto>>
{
    private readonly IRecommenderService _recommenderService;

    public GetAllTagsMasteryQueryHandler(IRecommenderService recommenderService)
    {
        _recommenderService = recommenderService;
    }

    public async Task<IReadOnlyList<TagMasteryDto>> Handle(
        GetAllTagsMasteryQuery request, CancellationToken cancellationToken)
    {
        return await _recommenderService.GetStudentAllTagsMasteryAsync(
            request.StudentId, cancellationToken);
    }
}
