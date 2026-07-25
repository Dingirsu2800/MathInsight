using MathInsight.Modules.Identity_Access.Contracts.Accounts;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Queries.GetProfile;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateProfile;

/// <summary>
/// UC-05. Partially updates the caller's own account row and whichever role-specific row exists
/// for it. Only non-null fields are written (<c>?? existing</c>), so an omitted field keeps its
/// stored value.
///
/// No email uniqueness check lives here any more: email is not updatable through this endpoint,
/// so the Account table's unique index cannot be violated by it.
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileResponse>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMediator _mediator;

    public UpdateProfileCommandHandler(IdentityDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<Result<ProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .Include(account => account.Student)
            .Include(account => account.Teacher)
            .Include(account => account.Expert)
            .FirstOrDefaultAsync(account => account.AccountId == request.AccountId, cancellationToken);

        if (account is null)
        {
            // The token carries an account_id with no row behind it — the account was removed
            // after the token was issued. Treat the token as no longer valid.
            return Result<ProfileResponse>.Failure(AuthErrors.TokenInvalid);
        }

        account.FirstName = request.FirstName ?? account.FirstName;
        account.LastName = request.LastName ?? account.LastName;
        account.PhoneNumber = request.PhoneNumber ?? account.PhoneNumber;
        account.DateOfBirth = request.DateOfBirth ?? account.DateOfBirth;

        // Role-specific fields apply only to the role the account actually holds. Fields sent for
        // a role the caller does not hold are ignored rather than rejected — there is no row to
        // write them to. isVerified (Teacher) and the application status are Admin-controlled
        // (UC-15) and are never touched here.
        if (account.Student is not null)
        {
            account.Student.Gender = request.Gender ?? account.Student.Gender;
            account.Student.School = request.School ?? account.Student.School;
            account.Student.CurrentGrade = request.CurrentGrade ?? account.Student.CurrentGrade;
        }

        if (account.Teacher is not null)
        {
            account.Teacher.Biography = request.Biography ?? account.Teacher.Biography;
        }

        if (account.Expert is not null)
        {
            account.Expert.Specialty = request.Specialty ?? account.Expert.Specialty;
        }

        // EF no-ops the UPDATE entirely when nothing actually changed.
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reuse the UC-04 projection so GET and PUT can never drift apart in shape.
        return await _mediator.Send(new GetProfileQuery(request.AccountId), cancellationToken);
    }
}
