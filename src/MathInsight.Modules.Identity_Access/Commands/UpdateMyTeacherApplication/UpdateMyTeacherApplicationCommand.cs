using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateMyTeacherApplication;

/// <summary>
/// UC-08. Edits a REJECTED application in place. <paramref name="AccountId"/> comes from the access
/// token and is checked against the application's owner, so the route id alone grants nothing.
///
/// Certificates are built by the controller from the uploaded IFormFile list; as at registration,
/// each entry's SizeInBytes MUST be IFormFile.Length so the 10 MB gate is enforced (BR-05).
/// </summary>
public sealed record UpdateMyTeacherApplicationCommand(
    string ApplicationId,
    string AccountId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Biography,
    IReadOnlyList<string> KeptDocumentsUrls,
    IReadOnlyList<CertificateUploadRequest> Certificates)
    : IRequest<Result<MyTeacherApplicationResponse>>;
