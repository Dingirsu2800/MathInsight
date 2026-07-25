using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MathInsight.Modules.Identity_Access.Services;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Identity_Access.Commands.ConfirmEmail;

/// <summary>
/// UC-93, BR-04 step 5. The single point where a self-registered Account row is created, with
/// <c>is_active = true</c>. Reads the pending registration from Redis, re-checks uniqueness,
/// inserts the account and its role-specific row(s) in one transaction, publishes events, and
/// deletes the Redis key.
/// </summary>
public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result<Unit>>
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPendingRegistrationStore _pendingRegistrations;
    private readonly IPublisher _publisher;

    public ConfirmEmailCommandHandler(
        IdentityDbContext dbContext,
        IPendingRegistrationStore pendingRegistrations,
        IPublisher publisher)
    {
        _dbContext = dbContext;
        _pendingRegistrations = pendingRegistrations;
        _publisher = publisher;
    }

    public async Task<Result<Unit>> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        // Missing key ⇒ token expired (24h) or already used.
        var payload = await _pendingRegistrations.GetAsync(request.Token, cancellationToken);

        if (payload is null)
        {
            return Result<Unit>.Failure(AuthErrors.TokenExpired);
        }

        // Re-check uniqueness: another pending registration for the same identity may have
        // confirmed first (the pending-registration race).
        var conflict = await _dbContext.Accounts
            .AnyAsync(account => account.Email == payload.Email || account.Username == payload.Username, cancellationToken);

        if (conflict)
        {
            return Result<Unit>.Failure(AuthErrors.EmailAlreadyConfirmed);
        }

        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(role => role.RoleName == payload.Role, cancellationToken)
            ?? throw new InvalidOperationException($"Seeded role '{payload.Role}' was not found.");

        var accountId = Guid.NewGuid().ToString();

        var account = new Account
        {
            AccountId = accountId,
            Username = payload.Username,
            PasswordHash = payload.PasswordHash,
            Email = payload.Email,
            FirstName = payload.FirstName,
            LastName = payload.LastName,
            RoleId = role.RoleId,
            IsActive = true, // every persisted account is email-verified by construction (DD-01)
            CreatedTime = DateTime.UtcNow,
        };

        _dbContext.Accounts.Add(account);

        var isTeacher = string.Equals(payload.Role, "Teacher", StringComparison.OrdinalIgnoreCase);
        string? applicationId = null;

        if (isTeacher)
        {
            _dbContext.Teachers.Add(new Teacher
            {
                TeacherId = accountId,
                Biography = payload.Biography,
                IsVerified = false,
            });

            applicationId = Guid.NewGuid().ToString();
            _dbContext.TeacherApplications.Add(new TeacherApplication
            {
                ApplicationId = applicationId,
                TeacherId = accountId,
                DocumentsUrl = payload.DocumentsUrl!,
                Status = "Pending", // title-case, per CK_TeacherApplication_Status
                AppliedTime = DateTime.UtcNow,
            });
        }
        else
        {
            _dbContext.Students.Add(new Student
            {
                StudentId = accountId,
                Gender = payload.Gender,
                School = payload.School,
                CurrentGrade = payload.CurrentGrade,
            });
        }

        // One SaveChanges = one implicit transaction: all inserts commit together or not at all.
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish only after the write is durable.
        await _publisher.Publish(new AccountCreatedEvent
        {
            AccountId = accountId,
            Email = payload.Email,
            Username = payload.Username,
            RoleName = role.RoleName,
            FirstName = payload.FirstName,
            LastName = payload.LastName,
        }, cancellationToken);

        if (isTeacher)
        {
            await _publisher.Publish(new TeacherApplicationSubmittedEvent
            {
                ApplicationId = applicationId!,
                TeacherId = accountId,
                Email = payload.Email,
                DocumentsUrl = payload.DocumentsUrl!,
            }, cancellationToken);
        }

        await _pendingRegistrations.DeleteAsync(request.Token, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
