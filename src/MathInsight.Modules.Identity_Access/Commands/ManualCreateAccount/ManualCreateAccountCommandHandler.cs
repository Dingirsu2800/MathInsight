using MathInsight.Modules.Identity_Access.Contracts.Admin;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ManualCreateAccount;

public class ManualCreateAccountCommandHandler
    : IRequestHandler<ManualCreateAccountCommand, Result<AccountListItemResponse>>
{
    private const int BCryptWorkFactor = 12;

    private readonly IdentityDbContext _dbContext;
    private readonly IMediator _mediator;

    public ManualCreateAccountCommandHandler(IdentityDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<Result<AccountListItemResponse>> Handle(
        ManualCreateAccountCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Password.Length < 8)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.PasswordTooShort);

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.RoleName == request.RoleName, cancellationToken);

        if (role is null ||
            !string.Equals(role.RoleName, "Student", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role.RoleName, "Teacher", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role.RoleName, "Expert", StringComparison.OrdinalIgnoreCase))
        {
            return Result<AccountListItemResponse>.Failure(IdentityErrors.InvalidRole);
        }

        var usernameTaken = await _dbContext.Accounts
            .AnyAsync(account => account.Username == request.Username, cancellationToken);
        if (usernameTaken)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.UsernameAlreadyExists);

        var emailTaken = await _dbContext.Accounts
            .AnyAsync(account => account.Email == request.Email, cancellationToken);
        if (emailTaken)
            return Result<AccountListItemResponse>.Failure(IdentityErrors.EmailAlreadyExists);

        var accountId = Guid.NewGuid().ToString();

        var account = new Account
        {
            AccountId = accountId,
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCryptWorkFactor),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            RoleId = role.RoleId,
            IsActive = true,
            CreatedTime = DateTime.UtcNow
        };

        _dbContext.Accounts.Add(account);

        switch (role.RoleName.ToUpperInvariant())
        {
            case "STUDENT":
                _dbContext.Students.Add(new Student { StudentId = accountId });
                break;
            case "TEACHER":
                _dbContext.Teachers.Add(new Teacher { TeacherId = accountId, IsVerified = true });
                break;
            case "EXPERT":
                _dbContext.Experts.Add(new Expert { ExpertId = accountId });
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(
            new AccountCreatedEvent(accountId, account.Email, $"{account.FirstName} {account.LastName}", role.RoleName),
            cancellationToken);

        return Result<AccountListItemResponse>.Success(new AccountListItemResponse(
            account.AccountId,
            account.Username,
            account.Email,
            account.FirstName,
            account.LastName,
            role.RoleId,
            role.RoleName,
            account.IsActive,
            account.CreatedTime));
    }
}
