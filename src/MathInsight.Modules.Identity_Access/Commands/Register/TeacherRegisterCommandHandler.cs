using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.Register;

/// <summary>
/// UC-08, BR-04/BR-05. As Student registration, plus: the certificate is uploaded to blob storage
/// first and its URL is carried in the Redis payload. DD-01: ZERO SQL inserts — no Account and no
/// TeacherApplication row is created here (both are created at confirmation).
/// </summary>
public class TeacherRegisterCommandHandler : IRequestHandler<TeacherRegisterCommand, Result<Unit>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPendingRegistrationStore _pendingRegistrations;
    private readonly ICertificateStorage _certificateStorage;
    private readonly IEmailService _emailService;

    public TeacherRegisterCommandHandler(
        IdentityDbContext dbContext,
        IPendingRegistrationStore pendingRegistrations,
        ICertificateStorage certificateStorage,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _pendingRegistrations = pendingRegistrations;
        _certificateStorage = certificateStorage;
        _emailService = emailService;
    }

    public async Task<Result<Unit>> Handle(TeacherRegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var username = request.Username.Trim();
        var phoneNumber = request.PhoneNumber.Trim();

        var exists = await _dbContext.Accounts
            .AnyAsync(account => account.Email == email || account.Username == username, cancellationToken);

        if (exists)
        {
            return Result<Unit>.Failure(AuthErrors.EmailAlreadyConfirmed);
        }

        // Separate check, and before the uploads: the phone number has its own unique index, so a
        // duplicate would otherwise only fail at confirmation — after the certificates were stored.
        var phoneTaken = await _dbContext.Accounts
            .AnyAsync(account => account.PhoneNumber == phoneNumber, cancellationToken);

        if (phoneTaken)
        {
            return Result<Unit>.Failure(AuthErrors.PhoneNumberAlreadyUsed);
        }

        if (request.Certificates.Count == 0)
        {
            return Result<Unit>.Failure(AuthErrors.CertificateInvalid("At least one certificate image is required."));
        }

        // Each request's SizeInBytes comes from the controller's IFormFile.Length; if it were 0 the
        // 10MB gate in BlobCertificateStorage would be silently skipped (BR-05). Uploads run
        // sequentially so the first rejected file short-circuits the rest.
        var documentsUrls = new List<string>(request.Certificates.Count);
        try
        {
            foreach (var certificate in request.Certificates)
            {
                documentsUrls.Add(await _certificateStorage.UploadAsync(certificate, cancellationToken));
            }
        }
        catch (InvalidCertificateException exception)
        {
            // Wrong type or too large (BR-05) → 400, distinct from a storage failure.
            return Result<Unit>.Failure(AuthErrors.CertificateInvalid(exception.Message));
        }

        var payload = new PendingRegistration
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = "Teacher",
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = phoneNumber,
            Biography = request.Biography,
            // DocumentsUrl stays populated with the first URL for the existing single-URL
            // TeacherApplication column; DocumentsUrls carries the full set.
            DocumentsUrl = documentsUrls[0],
            DocumentsUrls = documentsUrls,
        };

        var token = await _pendingRegistrations.SaveAsync(payload, cancellationToken);
        await _emailService.SendRegistrationConfirmationAsync(email, token, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
