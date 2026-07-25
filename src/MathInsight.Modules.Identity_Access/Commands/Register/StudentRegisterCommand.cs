using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Register;

// UC-39. DD-01: writes to Redis ONLY — performs zero SQL inserts.
public record StudentRegisterCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Gender,
    string? School,
    int? CurrentGrade) : IRequest<Result<Unit>>;
