using MathInsight.Modules.Testing.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Testing.Commands.RecordIncident;

public sealed record RecordIncidentCommand(
    string SessionId,
    string StudentId,
    string Type) : IRequest<Result<RecordIncidentResponse>>;
