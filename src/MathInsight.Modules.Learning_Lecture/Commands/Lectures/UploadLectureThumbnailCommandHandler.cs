using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MathInsight.Shared.Storage;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public class UploadLectureThumbnailCommandHandler : IRequestHandler<UploadLectureThumbnailCommand, string>
{
    private readonly IImageStorage _imageStorage;

    public UploadLectureThumbnailCommandHandler(IImageStorage imageStorage)
    {
        _imageStorage = imageStorage;
    }

    public async Task<string> Handle(UploadLectureThumbnailCommand request, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
            throw new Exception("Invalid image format. Only JPG, PNG, and WebP are allowed.");
        
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var uploadRequest = new ImageUploadRequest(request.FileStream, request.FileName, contentType, "thumbnails");
        var fileUrl = await _imageStorage.UploadAsync(uploadRequest, cancellationToken);

        return fileUrl;
    }
}
