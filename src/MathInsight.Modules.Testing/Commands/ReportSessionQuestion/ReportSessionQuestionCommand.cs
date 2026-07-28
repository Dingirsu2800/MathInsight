using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.ReportSessionQuestion;

public sealed record ReportSessionQuestionCommand(
    string SessionId,
    string QuestionId,
    string StudentId,
    string Reason) : IRequest<Result<bool>>;
