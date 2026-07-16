using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public class UpdateMaterialCommandHandler : IRequestHandler<UpdateMaterialCommand, MaterialDto>
{
    private readonly LearningDbContext _dbContext;

    public UpdateMaterialCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MaterialDto> Handle(UpdateMaterialCommand request, CancellationToken cancellationToken)
    {
        var material = await _dbContext.Materials.FirstOrDefaultAsync(x => x.MaterialId == request.MaterialId, cancellationToken);
        if (material == null) throw new Exception("Material not found");
        if (material.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");

        material.MaterialName = request.MaterialName;
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MaterialDto
        {
            MaterialId = material.MaterialId,
            MaterialName = material.MaterialName,
            FileUrl = material.FileUrl,
            FileType = material.FileType,
            TeacherId = material.TeacherId,
            Status = material.Status,
            UploadedTime = material.UploadedTime
        };
    }
}
