using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Services;

namespace MathInsight.Modules.Learning_Lecture.Commands.Materials;

public class UploadMaterialCommandHandler : IRequestHandler<UploadMaterialCommand, MaterialDto>
{
    private readonly LearningDbContext _dbContext;
    private readonly ICloudinaryService _cloudinaryService;

    public UploadMaterialCommandHandler(LearningDbContext dbContext, ICloudinaryService cloudinaryService)
    {
        _dbContext = dbContext;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<MaterialDto> Handle(UploadMaterialCommand request, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext != ".pdf" && ext != ".mp4" && ext != ".docx")
            throw new Exception("Invalid file format. Only PDF, MP4, and DOCX are allowed.");
        
        if (request.FileStream.Length > 500 * 1024 * 1024)
            throw new Exception("File size exceeds 500 MB limit.");

        var fileUrl = await _cloudinaryService.UploadAsync(request.FileStream, request.FileName);

        var material = new Material
        {
            MaterialId = Guid.NewGuid().ToString(),
            MaterialName = request.MaterialName,
            FileUrl = fileUrl,
            FileType = ext.TrimStart('.'),
            TeacherId = request.TeacherId,
            Status = "Active",
            UploadedTime = DateTime.UtcNow
        };

        _dbContext.Materials.Add(material);
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
