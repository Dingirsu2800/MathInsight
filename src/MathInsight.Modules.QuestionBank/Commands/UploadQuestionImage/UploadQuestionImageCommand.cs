using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;

public sealed record UploadQuestionImageCommand(IFormFile? File)
    : IRequest<Result<QuestionImageUploadResponse>>;
