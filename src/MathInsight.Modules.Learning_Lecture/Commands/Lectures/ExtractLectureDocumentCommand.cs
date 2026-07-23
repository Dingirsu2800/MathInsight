using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public sealed record ExtractLectureDocumentCommand(IFormFile? File) : IRequest<Result<string>>;
