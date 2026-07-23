using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Ocr;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

internal sealed class ExtractLectureOcrCommandHandler : IRequestHandler<ExtractLectureOcrCommand, Result<ExtractLectureOcrResult>>
{
    private static readonly HashSet<string> AllowedFileTypes = new()
    {
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    private readonly ILectureOcrService _ocrService;

    public ExtractLectureOcrCommandHandler(ILectureOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<Result<ExtractLectureOcrResult>> Handle(ExtractLectureOcrCommand request, CancellationToken cancellationToken)
    {
        if (request.ImageFile == null || request.ImageFile.Length == 0)
        {
            return Result<ExtractLectureOcrResult>.Failure(new Error("LectureOcr.NoFile", "No file provided for OCR."));
        }

        if (!AllowedFileTypes.Contains(request.ImageFile.ContentType.ToLowerInvariant()))
        {
            return Result<ExtractLectureOcrResult>.Failure(new Error("LectureOcr.InvalidType", "Only PDF and JPEG/PNG images are supported for OCR."));
        }

        // Increase size limit to 20MB for PDF files
        if (request.ImageFile.Length > 20 * 1024 * 1024)
        {
            return Result<ExtractLectureOcrResult>.Failure(new Error("LectureOcr.TooLarge", "File size cannot exceed 20MB."));
        }

        await using var stream = new MemoryStream();
        await request.ImageFile.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        var ocrResult = await _ocrService.ExtractMarkdownAsync(stream, request.ImageFile.ContentType, cancellationToken);

        if (ocrResult.IsFailure)
        {
            return Result<ExtractLectureOcrResult>.Failure(ocrResult.Error!);
        }

        return Result<ExtractLectureOcrResult>.Success(new ExtractLectureOcrResult(ocrResult.Value!));
    }
}
