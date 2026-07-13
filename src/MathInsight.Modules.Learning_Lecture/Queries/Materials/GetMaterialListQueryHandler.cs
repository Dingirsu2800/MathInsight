using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Queries.Materials;

public class GetMaterialListQueryHandler : IRequestHandler<GetMaterialListQuery, List<MaterialDto>>
{
    private readonly LearningDbContext _dbContext;

    public GetMaterialListQueryHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MaterialDto>> Handle(GetMaterialListQuery request, CancellationToken cancellationToken)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Where(x => x.TeacherId == request.TeacherId)
            .OrderByDescending(x => x.UploadedTime)
            .Skip(skip)
            .Take(request.PageSize)
            .Select(x => new MaterialDto
            {
                MaterialId = x.MaterialId,
                MaterialName = x.MaterialName,
                FileUrl = x.FileUrl,
                FileType = x.FileType,
                TeacherId = x.TeacherId,
                Status = x.Status,
                UploadedTime = x.UploadedTime
            }).ToListAsync(cancellationToken);

        return materials;
    }
}
