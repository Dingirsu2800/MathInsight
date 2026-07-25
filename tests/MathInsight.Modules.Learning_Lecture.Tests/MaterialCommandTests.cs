using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Commands.Materials;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class MaterialCommandTests
{
    [Fact]
    public async Task UploadMaterial_ValidPdf_ReturnsUploadedMaterial()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        
        var mockStorage = new Mock<IImageStorage>();
        mockStorage.Setup(s => s.UploadAsync(It.IsAny<ImageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cloudinary.com/test-url.pdf");

        var handler = new UploadMaterialCommandHandler(database.Context, mockStorage.Object);
        var fileStream = new MemoryStream();
        var command = new UploadMaterialCommand("Test Material", fileStream, "test.pdf", "teacher-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://cloudinary.com/test-url.pdf", result.FileUrl);
        Assert.Equal("pdf", result.FileType);
        
        var materialInDb = await database.Context.Materials.FirstOrDefaultAsync(m => m.MaterialId == result.MaterialId);
        Assert.NotNull(materialInDb);
        Assert.Equal("Active", materialInDb.Status);
    }

    [Fact]
    public async Task UploadMaterial_InvalidFormat_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var mockStorage = new Mock<IImageStorage>();
        var handler = new UploadMaterialCommandHandler(database.Context, mockStorage.Object);
        var fileStream = new MemoryStream();
        
        // Use an invalid format like .exe
        var command = new UploadMaterialCommand("Malicious file", fileStream, "malicious.exe", "teacher-1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid file format", ex.Message);
    }

    [Fact]
    public async Task AttachMaterialToLecture_ValidOwnership_Succeeds()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        var materialId = Guid.NewGuid().ToString();
        
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test Lecture",
            TagId = "tag-1",
            TeacherId = "teacher-1", // Owner is teacher-1
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });

        database.Context.Materials.Add(new Material
        {
            MaterialId = materialId,
            MaterialName = "Test Material",
            TeacherId = "teacher-1", // Owner is teacher-1
            FileUrl = "http://test",
            FileType = "PDF",
            Status = "Active",
            UploadedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new AttachMaterialToLectureCommandHandler(database.Context);
        var command = new AttachMaterialToLectureCommand(materialId, lectureId, "teacher-1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var lectureMaterial = await database.Context.LectureMaterials
            .FirstOrDefaultAsync(lm => lm.LectureId == lectureId && lm.MaterialId == materialId);
        Assert.NotNull(lectureMaterial);
    }

    [Fact]
    public async Task AttachMaterialToLecture_NotOwnerOfLecture_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        var materialId = Guid.NewGuid().ToString();
        
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test",
            TagId = "tag-1",
            TeacherId = "teacher-1", // Owner is teacher-1
            Status = "Draft",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });

        database.Context.Materials.Add(new Material
        {
            MaterialId = materialId,
            MaterialName = "Material",
            TeacherId = "teacher-2", // Owner is teacher-2, but this won't even be reached because lecture fails first
            FileUrl = "http://test",
            FileType = "PDF",
            Status = "Active",
            UploadedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new AttachMaterialToLectureCommandHandler(database.Context);
        // Caller is teacher-2, but lecture is owned by teacher-1
        var command = new AttachMaterialToLectureCommand(materialId, lectureId, "teacher-2"); 

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Forbidden: Not the owner of lecture or material", ex.Message);
    }
}
