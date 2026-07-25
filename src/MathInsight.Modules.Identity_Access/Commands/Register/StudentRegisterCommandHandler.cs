using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.Register;

/// <summary>
/// UC-39, BR-04. Validates, checks uniqueness against confirmed accounts, hashes the password,
/// and stores the payload in Redis. DD-01: this handler performs ZERO SQL inserts — the Account
/// row is created only at email confirmation.
/// </summary>
public class StudentRegisterCommandHandler : IRequestHandler<StudentRegisterCommand, Result<Unit>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPendingRegistrationStore _pendingRegistrations;
    private readonly IEmailService _emailService;

    public StudentRegisterCommandHandler(
        IdentityDbContext dbContext,
        IPendingRegistrationStore pendingRegistrations,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _pendingRegistrations = pendingRegistrations;
        _emailService = emailService;
    }

    public async Task<Result<Unit>> Handle(StudentRegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var username = request.Username.Trim();

        // Every persisted account is confirmed (DD-01), so this checks confirmed accounts only.
        var exists = await _dbContext.Accounts
            .AnyAsync(account => account.Email == email || account.Username == username, cancellationToken);

        if (exists)
        {
            return Result<Unit>.Failure(AuthErrors.EmailAlreadyConfirmed);
        }

        var payload = new PendingRegistration
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = "Student",
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Gender = request.Gender,
            School = request.School,
            CurrentGrade = request.CurrentGrade,
        };

        var token = await _pendingRegistrations.SaveAsync(payload, cancellationToken);
        await _emailService.SendRegistrationConfirmationAsync(email, token, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
