using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ResolveApplication;

public sealed record ResolveApplicationCommand(
    string ApplicationId,
    bool Approve,
    string? ReviewComments,
    string ReviewerAccountId) : IRequest<Result<bool>>;
