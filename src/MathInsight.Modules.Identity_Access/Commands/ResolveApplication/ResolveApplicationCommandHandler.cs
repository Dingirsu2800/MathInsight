using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ResolveApplication;

public class ResolveApplicationCommandHandler : IRequestHandler<ResolveApplicationCommand, Result<bool>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMediator _mediator;

    public ResolveApplicationCommandHandler(IdentityDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<Result<bool>> Handle(ResolveApplicationCommand request, CancellationToken cancellationToken)
    {
        if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewComments))
            return Result<bool>.Failure(IdentityErrors.RejectReasonRequired);

        var application = await _dbContext.TeacherApplications
            .Include(application => application.Teacher)
            .ThenInclude(teacher => teacher.Account)
            .FirstOrDefaultAsync(application => application.ApplicationId == request.ApplicationId, cancellationToken);

        if (application is null)
            return Result<bool>.Failure(IdentityErrors.ApplicationNotFound);

        if (!string.Equals(application.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return Result<bool>.Failure(IdentityErrors.ApplicationAlreadyResolved);

        application.Status = request.Approve ? "Approved" : "Rejected";
        application.ReviewComments = request.ReviewComments;
        application.ReviewedTime = DateTime.UtcNow;
        application.ReviewedBy = request.ReviewerAccountId;

        if (request.Approve)
        {
            application.Teacher.IsVerified = true;
            application.Teacher.Account.IsActive = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(
            new ApplicationResolvedEvent(application.ApplicationId, application.TeacherId, request.Approve, request.ReviewComments),
            cancellationToken);

        return Result<bool>.Success(true);
    }
}
