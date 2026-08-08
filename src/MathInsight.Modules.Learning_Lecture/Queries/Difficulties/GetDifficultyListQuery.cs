using MathInsight.Modules.Learning_Lecture.Contracts;
using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Queries.Difficulties;

public sealed record GetDifficultyListQuery : IRequest<IReadOnlyList<DifficultyDto>>;
