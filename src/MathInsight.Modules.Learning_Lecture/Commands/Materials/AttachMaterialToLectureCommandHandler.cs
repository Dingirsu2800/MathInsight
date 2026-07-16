using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Entities;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public class AttachMaterialToLectureCommandHandler : IRequestHandler<AttachMaterialToLectureCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public AttachMaterialToLectureCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(AttachMaterialToLectureCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        var material = await _dbContext.Materials.FirstOrDefaultAsync(x => x.MaterialId == request.MaterialId, cancellationToken);

        if (lecture == null) throw new Exception("Lecture not found");
        if (material == null) throw new Exception("Material not found");
        
        if (lecture.TeacherId != request.TeacherId || material.TeacherId != request.TeacherId) 
            throw new Exception("Forbidden: Not the owner of lecture or material");

        var existingAssoc = await _dbContext.LectureMaterials
            .FirstOrDefaultAsync(x => x.LectureId == request.LectureId && x.MaterialId == request.MaterialId, cancellationToken);
            
        if (existingAssoc != null) return true; // Idempotent

        _dbContext.LectureMaterials.Add(new LectureMaterial
        {
            LectureId = request.LectureId,
            MaterialId = request.MaterialId
        });
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
