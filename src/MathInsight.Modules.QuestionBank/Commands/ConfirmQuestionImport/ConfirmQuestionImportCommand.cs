using MathInsight.Modules.QuestionBank.Contracts.Imports;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.ConfirmQuestionImport;

public sealed record ConfirmQuestionImportCommand(
    ConfirmQuestionImportRequest Request,
    string ExpertId) : IRequest<Result<QuestionImportConfirmResponse>>;
