using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.ResubmitTeacherApplication;

/// <summary>
/// UC-08. Puts a REJECTED application back into review. No email re-confirmation: the address is
/// unchanged and was already confirmed (DD-01).
/// </summary>
public sealed record ResubmitTeacherApplicationCommand(string ApplicationId, string AccountId)
    : IRequest<Result<MyTeacherApplicationResponse>>;
