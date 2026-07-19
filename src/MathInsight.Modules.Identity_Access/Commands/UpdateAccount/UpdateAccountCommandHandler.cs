using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.UpdateAccount;

public class UpdateAccountCommandHandler
    : IRequestHandler<UpdateAccountCommand, Result<AccountListItemResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public UpdateAccountCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AccountListItemResponse>> Handle(
        UpdateAccountCommand request,
        CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .Include(account => account.Student)
            .Include(account => account.Teacher)
            .Include(account => account.Expert)
            .FirstOrDefaultAsync(account => account.AccountId == request.AccountId, cancellationToken);

        if (account is null)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.AccountNotFound);

        if (!string.Equals(account.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailTaken = await _dbContext.Accounts
                .AnyAsync(other => other.AccountId != account.AccountId && other.Email == request.Email, cancellationToken);
            if (emailTaken)
                return Result<AccountListItemResponse>.Failure(IdentityErrors.EmailAlreadyExists);
        }

        var newRole = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.RoleId == request.RoleId, cancellationToken);
        if (newRole is null)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.RoleNotFound);

        if (!string.Equals(account.RoleId, newRole.RoleId, StringComparison.Ordinal))
        {
            if (account.Student is not null) _dbContext.Students.Remove(account.Student);
            if (account.Teacher is not null) _dbContext.Teachers.Remove(account.Teacher);
            if (account.Expert is not null) _dbContext.Experts.Remove(account.Expert);

            switch (newRole.RoleName.ToUpperInvariant())
            {
                case "STUDENT":
                    _dbContext.Students.Add(new Student { StudentId = account.AccountId });
                    break;
                case "TEACHER":
                    _dbContext.Teachers.Add(new Teacher { TeacherId = account.AccountId, IsVerified = true });
                    break;
                case "EXPERT":
                    _dbContext.Experts.Add(new Expert { ExpertId = account.AccountId });
                    break;
            }

            account.RoleId = newRole.RoleId;
        }

        account.FirstName = request.FirstName;
        account.LastName = request.LastName;
        account.Email = request.Email;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountListItemResponse>.Success(new AccountListItemResponse(
            account.AccountId,
            account.Username,
            account.Email,
            account.FirstName,
            account.LastName,
            newRole.RoleId,
            newRole.RoleName,
            account.IsActive,
            account.CreatedTime));
    }
}
