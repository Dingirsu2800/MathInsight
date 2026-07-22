using MediatR;

namespace MathInsight.Modules.Gamification.Queries.TargetScores;

public sealed record GetTargetProgressQuery(string StudentId) : IRequest<List<TargetProgressDto>>;

public sealed record TargetProgressDto
{
    public string TargetId { get; init; } = default!;
    public string TagId { get; init; } = default!;
    public string TagName { get; init; } = default!;
    public decimal TargetPoint { get; init; }
    public decimal CurrentPoint { get; init; }
    public bool IsAchieved { get; init; }
    public DateTime CreatedTime { get; init; }
}
