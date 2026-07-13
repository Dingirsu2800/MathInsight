using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public class DeactivateMaterialCommandHandler : IRequestHandler<DeactivateMaterialCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public DeactivateMaterialCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeactivateMaterialCommand request, CancellationToken cancellationToken)
    {
        var material = await _dbContext.Materials.FirstOrDefaultAsync(x => x.MaterialId == request.MaterialId, cancellationToken);
        if (material == null) throw new Exception("Material not found");
        if (material.TeacherId != request.TeacherId) throw new Exception("Forbidden: Not the owner");

        material.Status = "Deactivated";
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
