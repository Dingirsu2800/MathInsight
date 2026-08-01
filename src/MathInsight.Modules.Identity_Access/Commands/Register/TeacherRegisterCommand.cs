using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Register;

// UC-08. DD-01: writes to Redis ONLY (plus the certificate uploads) — performs zero SQL inserts.
// Certificates are built by the controller from the uploaded IFormFile list; each entry's
// SizeInBytes MUST be IFormFile.Length so the 10MB gate is enforced per file (BR-05).
public record TeacherRegisterCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Biography,
    IReadOnlyList<CertificateUploadRequest> Certificates) : IRequest<Result<Unit>>;
