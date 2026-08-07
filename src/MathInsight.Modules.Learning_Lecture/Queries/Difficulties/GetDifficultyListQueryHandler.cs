using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Learning_Lecture.Queries.Difficulties;

public sealed class GetDifficultyListQueryHandler : IRequestHandler<GetDifficultyListQuery, IReadOnlyList<DifficultyDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetDifficultyListQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DifficultyDto>> Handle(
        GetDifficultyListQuery request,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TagDifficulties
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.DifficultyId)
            .Select(x => new DifficultyDto(
                x.DifficultyId,
                x.DifficultyName,
                x.LevelValue,
                x.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
