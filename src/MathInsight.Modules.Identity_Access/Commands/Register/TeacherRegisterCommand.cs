using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.Register;

// UC-08. DD-01: writes to Redis ONLY (plus the certificate upload) — performs zero SQL inserts.
// The certificate fields are populated by the controller from the uploaded IFormFile;
// CertificateSizeInBytes MUST be IFormFile.Length so the 10MB gate is enforced (BR-05).
public record TeacherRegisterCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Biography,
    Stream CertificateContent,
    string CertificateFileName,
    string CertificateContentType,
    long CertificateSizeInBytes) : IRequest<Result<Unit>>;
