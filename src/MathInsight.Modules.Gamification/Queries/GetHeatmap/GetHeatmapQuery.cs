using MathInsight.Modules.Gamification.Contracts;
using MediatR;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.Gamification.Queries.GetHeatmap;

public record GetHeatmapQuery(string StudentId) : IRequest<Result<StudyHeatmapDto>>;
