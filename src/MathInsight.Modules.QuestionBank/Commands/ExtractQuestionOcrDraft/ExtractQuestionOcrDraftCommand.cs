using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MathInsight.Modules.QuestionBank.Commands.ExtractQuestionOcrDraft;

public sealed record ExtractQuestionOcrDraftCommand(IFormFile? File)
    : IRequest<Result<QuestionOcrDraftResponse>>;
