using MathInsight.Modules.Identity_Access.Commands.ResolveApplication;
using MathInsight.Modules.Identity_Access.Entities;
using MathInsight.Modules.Identity_Access.Errors;
using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Identity_Access.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Identity_Access.Tests;

public class ResolveApplicationCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IMediator> _mediator = new();
    private readonly ResolveApplicationCommandHandler _handler;

    public ResolveApplicationCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new IdentityDbContext(options);
        _mediator.Setup(m => m.Publish(It.IsAny<ApplicationResolvedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ResolveApplicationCommandHandler(_db, _mediator.Object);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TeacherApplication> SeedPendingApplicationAsync(bool teacherVerified = false, bool accountActive = false)
    {
        var role = new Role { RoleId = Guid.NewGuid().ToString(), RoleName = "Teacher" };
        var account = new Account
        {
            AccountId = Guid.NewGuid().ToString(), Username = "teacher1", Email = "teacher1@x.com",
            PasswordHash = "hash", FirstName = "A", LastName = "B", RoleId = role.RoleId,
            IsActive = accountActive, CreatedTime = DateTime.UtcNow
        };
        var teacher = new Teacher { TeacherId = account.AccountId, IsVerified = teacherVerified };
        var application = new TeacherApplication
        {
            ApplicationId = Guid.NewGuid().ToString(),
            TeacherId = account.AccountId,
            DocumentsUrl = "https://example.com/cert.png",
            Status = "Pending",
            AppliedTime = DateTime.UtcNow
        };
        _db.Roles.Add(role);
        _db.Accounts.Add(account);
        _db.Teachers.Add(teacher);
        _db.TeacherApplications.Add(application);
        await _db.SaveChangesAsync();
        return application;
    }

    [Fact]
    public async Task Handle_RejectWithoutReviewComments_ReturnsFailure()
    {
        var command = new ResolveApplicationCommand("any-id", false, null, "reviewer-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RejectReasonRequired, result.Error);
    }

    [Fact]
    public async Task Handle_RejectWithWhitespaceOnlyComments_ReturnsFailure()
    {
        var command = new ResolveApplicationCommand("any-id", false, "   ", "reviewer-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.RejectReasonRequired, result.Error);
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ReturnsFailure()
    {
        var command = new ResolveApplicationCommand("missing-id", true, null, "reviewer-id");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.ApplicationNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_ApplicationAlreadyResolved_ReturnsFailure()
    {
        var application = await SeedPendingApplicationAsync();
        application.Status = "Approved";
        await _db.SaveChangesAsync();

        var command = new ResolveApplicationCommand(application.ApplicationId, false, "reason", "reviewer-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.ApplicationAlreadyResolved, result.Error);
    }

    [Fact]
    public async Task Handle_Approve_ActivatesTeacherAndAccount_PublishesEvent()
    {
        var application = await SeedPendingApplicationAsync(teacherVerified: false, accountActive: false);

        var command = new ResolveApplicationCommand(application.ApplicationId, true, null, "reviewer-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updatedApp = await _db.TeacherApplications.AsNoTracking().FirstAsync(a => a.ApplicationId == application.ApplicationId);
        Assert.Equal("Approved", updatedApp.Status);
        var teacher = await _db.Teachers.FindAsync(application.TeacherId);
        Assert.True(teacher!.IsVerified);
        var account = await _db.Accounts.AsNoTracking().FirstAsync(a => a.AccountId == application.TeacherId);
        Assert.True(account.IsActive);
        _mediator.Verify(m => m.Publish(
            It.Is<ApplicationResolvedEvent>(e => e.ApplicationId == application.ApplicationId && e.Approved),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Reject_SavesReviewCommentsAndPublishesEvent()
    {
        var application = await SeedPendingApplicationAsync();

        var command = new ResolveApplicationCommand(application.ApplicationId, false, "Certificate unreadable", "reviewer-id");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updatedApp = await _db.TeacherApplications.AsNoTracking().FirstAsync(a => a.ApplicationId == application.ApplicationId);
        Assert.Equal("Rejected", updatedApp.Status);
        Assert.Equal("Certificate unreadable", updatedApp.ReviewComments);
        var teacher = await _db.Teachers.FindAsync(application.TeacherId);
        Assert.False(teacher!.IsVerified);
        _mediator.Verify(m => m.Publish(
            It.Is<ApplicationResolvedEvent>(e => e.ApplicationId == application.ApplicationId && !e.Approved),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
