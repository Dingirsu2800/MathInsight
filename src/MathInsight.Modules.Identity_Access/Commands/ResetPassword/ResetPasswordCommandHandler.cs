using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ResetPassword;

/// <summary>
/// UC-06 part 1. If a confirmed account owns the email, issues a reset token
/// (<c>password:reset:{token}</c>, 15-minute TTL) and emails a reset link. Otherwise it does
/// nothing. Either way the result is <see cref="Result{T}.Success"/>, so the controller returns
/// the same generic 200 and never reveals whether the email is registered.
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<Unit>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordResetTokenStore _resetTokens;
    private readonly IEmailService _emailService;

    public ResetPasswordCommandHandler(
        IdentityDbContext dbContext,
        IPasswordResetTokenStore resetTokens,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _resetTokens = resetTokens;
        _emailService = emailService;
    }

    public async Task<Result<Unit>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        // DD-01: every Account row is an email-confirmed account, so this is the
        // "confirmed account with that email" check.
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(account => account.Email == email, cancellationToken);

        if (account is not null)
        {
            var token = await _resetTokens.CreateAsync(account.AccountId, cancellationToken);
            await _emailService.SendPasswordResetAsync(account.Email, token, cancellationToken);
        }

        // Enumeration protection (UC-06): identical outcome whether or not the email exists.
        return Result<Unit>.Success(Unit.Value);
    }
}
