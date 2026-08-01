using MathInsight.Modules.Identity_Access.Contracts.Teacher;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Queries.GetMyTeacherApplication;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ResubmitTeacherApplication;

public class ResubmitTeacherApplicationCommandHandler
    : IRequestHandler<ResubmitTeacherApplicationCommand, Result<MyTeacherApplicationResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPublisher _publisher;

    public ResubmitTeacherApplicationCommandHandler(IdentityDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }

    public async Task<Result<MyTeacherApplicationResponse>> Handle(
        ResubmitTeacherApplicationCommand request,
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

        if (!string.Equals(application.TeacherId, request.AccountId, StringComparison.Ordinal))
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationForbidden);
        }

        // Also blocks a double resubmit: the second call sees Pending and stops here.
        if (!string.Equals(application.Status, TeacherApplication.StatusRejected, StringComparison.OrdinalIgnoreCase))
        {
            return Result<MyTeacherApplicationResponse>.Failure(IdentityErrors.ApplicationNotEditable);
        }

        // CK_TeacherApplication_Review requires ReviewedTime and ReviewedBy to be NULL whenever
        // Status is 'Pending' — clearing them is mandatory, not cosmetic. ReviewComments goes too,
        // so the next reviewer is not reading the previous round's rejection reason.
        application.Status = TeacherApplication.StatusPending;
        application.ReviewedTime = null;
        application.ReviewedBy = null;
        application.ReviewComments = null;

        // Re-stamped so the application resurfaces at the top of the Admin queue, which orders by
        // AppliedTime descending; keeping the original date would bury it at its old position.
        application.AppliedTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var account = application.Teacher.Account;

        // Same event the first submission raises, so any Admin notification treats a resubmission
        // exactly like a new application to review.
        await _publisher.Publish(new TeacherApplicationSubmittedEvent
        {
            ApplicationId = application.ApplicationId,
            TeacherId = application.TeacherId,
            Email = account.Email,
            DocumentsUrl = application.DocumentsUrl,
        }, cancellationToken);

        return Result<MyTeacherApplicationResponse>.Success(new MyTeacherApplicationResponse(
            application.ApplicationId,
            application.TeacherId,
            account.Email,
            account.FirstName,
            account.LastName,
            account.PhoneNumber,
            application.Teacher.Biography,
            GetMyTeacherApplicationQueryHandler.SplitDocumentsUrl(application.DocumentsUrl),
            application.Status,
            application.ReviewComments,
            application.AppliedTime,
            application.ReviewedTime,
            CanEdit: false));
    }
}
