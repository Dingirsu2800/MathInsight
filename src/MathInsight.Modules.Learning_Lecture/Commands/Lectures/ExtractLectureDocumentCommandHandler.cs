using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public sealed class ExtractLectureDocumentCommandHandler : IRequestHandler<ExtractLectureDocumentCommand, Result<string>>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB limit for .docx
    
    private readonly ILectureDocumentParserService _parserService;

    public ExtractLectureDocumentCommandHandler(ILectureDocumentParserService parserService)
    {
        _parserService = parserService;
    }

    public async Task<Result<string>> Handle(ExtractLectureDocumentCommand command, CancellationToken cancellationToken)
    {
        var file = command.File;
        if (file is null || file.Length <= 0)
            return Result<string>.Failure(new Error("DocxParser.FileRequired", "A .docx file is required."));

        if (file.Length > MaxFileSizeBytes)
            return Result<string>.Failure(new Error("DocxParser.FileTooLarge", "File exceeds the 10MB size limit."));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".docx")
            return Result<string>.Failure(new Error("DocxParser.FormatNotSupported", "Only .docx files are supported."));

        await using var docxStream = file.OpenReadStream();
        
        var result = await _parserService.ParseDocxAsync(docxStream, cancellationToken);
        
        return result;
    }
}
