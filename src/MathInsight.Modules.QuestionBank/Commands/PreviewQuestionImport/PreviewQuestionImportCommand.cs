using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.QuestionBank.Commands.PreviewQuestionImport;

public sealed record PreviewQuestionImportCommand(IFormFile? File)
    : IRequest<Result<QuestionImportPreviewResponse>>;
