using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Queries.GetTagDifficulties;

public class GetTagDifficultiesQueryHandler : IRequestHandler<GetTagDifficultiesQuery, IReadOnlyList<TagDifficultyResponse>>
{
    private readonly QuestionBankDbContext _context;

    public GetTagDifficultiesQueryHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TagDifficultyResponse>> Handle(GetTagDifficultiesQuery request, CancellationToken cancellationToken)
    {
        return await _context.TagDifficulties
            .AsNoTracking()
            .Where(tag => tag.IsActive)
            .OrderBy(tag => tag.DisplayOrder)
            .Select(tag => new TagDifficultyResponse
            (
                tag.DifficultyId,
                tag.DifficultyName,
                tag.Description,
                tag.LevelValue,
                tag.DisplayOrder,
                tag.IsActive
            ))
            .ToListAsync(cancellationToken);
    }
}
