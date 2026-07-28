using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public sealed record ExtractLectureOcrCommand(IFormFile? ImageFile) : IRequest<Result<ExtractLectureOcrResult>>;

public sealed record ExtractLectureOcrResult(string Markdown);
