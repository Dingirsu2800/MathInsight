using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.ForceSubmitSession;

/// <summary>
/// Internal command invoked by RecordIncident (5+ incidents) or timer expiry.
/// SubmissionType: TimeoutSubmit (timer) or SystemSubmit (system/proctor/incidents).
/// </summary>
public sealed record ForceSubmitSessionCommand(
    string SessionId,
    string SubmissionType = "SystemSubmit") : IRequest<Result<SubmitSessionResponse>>;
