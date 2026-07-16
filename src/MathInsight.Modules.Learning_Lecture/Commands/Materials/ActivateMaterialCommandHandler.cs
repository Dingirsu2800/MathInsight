using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public class ActivateMaterialCommandHandler : IRequestHandler<ActivateMaterialCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public ActivateMaterialCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(ActivateMaterialCommand request, CancellationToken cancellationToken)
    {
        var material = await _dbContext.Materials.FirstOrDefaultAsync(x => x.MaterialId == request.MaterialId, cancellationToken);
        if (material == null) throw new Exception("Material not found");
        if (material.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");

        if (material.Status == "Active") return true;

        material.Status = "Active";
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
