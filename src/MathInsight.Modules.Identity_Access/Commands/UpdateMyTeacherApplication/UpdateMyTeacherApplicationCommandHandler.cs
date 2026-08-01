using MathInsight.Modules.Identity_Access.Contracts.Auth;
using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Queries.GetMyTeacherApplication;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateMyTeacherApplication;

/// <summary>
/// UC-08. Applies the teacher's own edits to a rejected application. Status is deliberately NOT
/// changed here — resubmission is the separate ResubmitTeacherApplicationCommand, so a teacher can
/// save partial work without putting an unfinished application in front of an Admin.
/// </summary>
public class UpdateMyTeacherApplicationCommandHandler
    : IRequestHandler<UpdateMyTeacherApplicationCommand, Result<MyTeacherApplicationResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly ICertificateStorage _certificateStorage;

    public UpdateMyTeacherApplicationCommandHandler(
        IdentityDbContext dbContext,
        ICertificateStorage certificateStorage)
    {
        _dbContext = dbContext;
        _certificateStorage = certificateStorage;
    }

    public async Task<Result<MyTeacherApplicationResponse>> Handle(
        UpdateMyTeacherApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var application = await _dbContext.TeacherApplications
            .Include(application => application.Teacher)
            .ThenInclude(teacher => teacher.Account)
            .FirstOrDefaultAsync(
                application => application.ApplicationId == request.ApplicationId,
                cancellationToken);

        if (application is null)
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationNotFound);
        }

        // Ownership: the route id is untrusted, the account id comes from the token.
        if (!string.Equals(application.TeacherId, request.AccountId, StringComparison.Ordinal))
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationForbidden);
        }

        if (!string.Equals(application.Status, TeacherApplication.StatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationNotEditable);
        }

        var phoneNumber = request.PhoneNumber.Trim();

        // Account.PhoneNumber is uniquely indexed where not null (UX_Account_PhoneNumber_NotNull);
        // exclude the caller's own row so re-saving an unchanged number is not a conflict.
        var phoneTaken = await _dbContext.Accounts.AnyAsync(
            account => account.PhoneNumber == phoneNumber && account.AccountId != request.AccountId,
            cancellationToken);

        if (phoneTaken)
        {
            return Result<MyTeacherApplicationResponse>.Failure(AuthErrors.PhoneNumberAlreadyUsed);
        }

        var keptResult = ResolveKeptUrls(application.DocumentsUrl, request.KeptDocumentsUrls);
        if (keptResult.IsFailure)
        {
            return Result<MyTeacherApplicationResponse>.Failure(keptResult.Error!);
        }

        var keptUrls = keptResult.Value!;
        var totalCertificates = keptUrls.Count + request.Certificates.Count;

        if (totalCertificates == 0)
        {
            return Result<MyTeacherApplicationResponse>.Failure(
                AuthErrors.CertificateInvalid("At least one certificate image is required."));
        }

        if (totalCertificates > TeacherRegisterRequest.MaxCertificates)
        {
            return Result<MyTeacherApplicationResponse>.Failure(AuthErrors.CertificateInvalid(
                $"At most {TeacherRegisterRequest.MaxCertificates} certificate images can be uploaded."));
        }

        // Kept URLs first so the teacher's existing order is preserved, new uploads appended.
        // Uploads run sequentially so the first rejected file short-circuits the rest (BR-05).
        var documentsUrls = new List<string>(keptUrls);
        try
        {
            foreach (var certificate in request.Certificates)
            {
                documentsUrls.Add(await _certificateStorage.UploadAsync(certificate, cancellationToken));
            }
        }
        catch (InvalidCertificateException exception)
        {
            return Result<MyTeacherApplicationResponse>.Failure(
                AuthErrors.CertificateInvalid(exception.Message));
        }

        var documentsUrl = string.Join(TeacherApplication.DocumentsUrlSeparator, documentsUrls);

        // The column is VARCHAR(2000); silently truncating would lose a certificate.
        if (documentsUrl.Length > TeacherApplication.DocumentsUrlMaxLength)
        {
            return Result<MyTeacherApplicationResponse>.Failure(AuthErrors.CertificateInvalid(
                "The certificate URLs exceed the storage limit. Please upload fewer images."));
        }

        var account = application.Teacher.Account;
        account.FirstName = request.FirstName.Trim();
        account.LastName = request.LastName.Trim();
        account.PhoneNumber = phoneNumber;
        application.Teacher.Biography = request.Biography;
        application.DocumentsUrl = documentsUrl;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<MyTeacherApplicationResponse>.Success(new MyTeacherApplicationResponse(
            application.ApplicationId,
            application.TeacherId,
            account.Email,
            account.FirstName,
            account.LastName,
            account.PhoneNumber,
            application.Teacher.Biography,
            documentsUrls,
            application.Status,
            application.ReviewComments,
            application.AppliedTime,
            application.ReviewedTime,
            CanEdit: true));
    }

    /// <summary>
    /// Keeps only URLs that are ALREADY on this application. Without this check the field would be
    /// an open write into DocumentsUrl, letting a caller point their application at any URL they
    /// like — including one an Admin would later open from the review screen.
    /// </summary>
    private static Result<List<string>> ResolveKeptUrls(
        string storedDocumentsUrl,
        IReadOnlyList<string> requestedUrls)
    {
        var currentUrls = GetMyTeacherApplicationQueryHandler.SplitDocumentsUrl(storedDocumentsUrl);
        var currentSet = new HashSet<string>(currentUrls, StringComparer.Ordinal);
        var keptUrls = new List<string>();

        foreach (var requestedUrl in requestedUrls)
        {
            var url = requestedUrl?.Trim();

            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (!currentSet.Contains(url))
            {
                return Result<List<string>>.Failure(AuthErrors.CertificateInvalid(
                    "A kept certificate URL does not belong to this application."));
            }

            // Tolerate a duplicated entry from the client rather than storing the URL twice.
            if (!keptUrls.Contains(url, StringComparer.Ordinal))
            {
                keptUrls.Add(url);
            }
        }

        return Result<List<string>>.Success(keptUrls);
    }
}
