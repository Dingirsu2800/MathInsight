using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.AutoSave;

public sealed record AutoSaveCommand(
    string SessionId,
    string StudentId,
    IReadOnlyList<AutoSaveAnswerDto> Answers) : IRequest<Result<AutoSaveResponse>>;
