using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ToggleAccountStatus;

public class ToggleAccountStatusCommandHandler
    : IRequestHandler<ToggleAccountStatusCommand, Result<AccountListItemResponse>>
{
    private readonly IdentityDbContext _dbContext;

    public ToggleAccountStatusCommandHandler(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AccountListItemResponse>> Handle(
        ToggleAccountStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.IsActive &&
            string.Equals(request.AccountId, request.RequestingAccountId, StringComparison.Ordinal))
        {
            return Result<AccountListItemResponse>.Failure(IdentityErrors.CannotDeactivateSelf);
        }

        var account = await _dbContext.Accounts
            .Include(account => account.Role)
            .FirstOrDefaultAsync(account => account.AccountId == request.AccountId, cancellationToken);

        if (account is null)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.AccountNotFound);

        account.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountListItemResponse>.Success(new AccountListItemResponse(
            account.AccountId,
            account.Username,
            account.Email,
            account.FirstName,
            account.LastName,
            account.RoleId,
            account.Role.RoleName,
            account.IsActive,
            account.CreatedTime));
    }
}
