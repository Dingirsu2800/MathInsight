using MathInsight.Modules.Identity_Access.Events;
using MathInsight.Modules.Learning_Lecture.Events;
using MathInsight.Modules.Notification_Report.Handlers;
using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Events;
using Moq;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

/// <summary>
/// Each of the 7 domain-event handlers only has to translate its event into the right
/// INotificationService.SendAsync(accountId, ...) call — verified here with a mocked service so
/// no database is involved.
/// </summary>
public class EventHandlersTests
{
    private static Mock<INotificationService> NewNotificationServiceMock()
    {
        var mock = new Mock<INotificationService>();
        mock.Setup(s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-id");
        return mock;
    }

    [Fact]
    public async Task GradeCalculatedHandler_SendsToStudent()
    {
        var mock = NewNotificationServiceMock();
        var handler = new GradeCalculatedHandler(mock.Object);

        var evt = new GradeCalculatedEvent { SessionId = "session-1", StudentId = "student-1", Score = 8.5m };
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "student-1", It.IsAny<string>(), It.Is<string>(c => c.Contains("8.5")), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BadgeAwardedHandler_SendsToStudent_WithBadgeName()
    {
        var mock = NewNotificationServiceMock();
        var handler = new BadgeAwardedHandler(mock.Object);

        var evt = new BadgeAwardedEvent("student-1", "badge-1", "Speedster", DateTime.UtcNow);
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "student-1", It.IsAny<string>(), It.Is<string>(c => c.Contains("Speedster")), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StreakReminderHandler_SendsToStudent()
    {
        var mock = NewNotificationServiceMock();
        var handler = new StreakReminderHandler(mock.Object);

        var evt = new StreakReminderEvent("student-1", 5, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "student-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscussionQuestionPostedHandler_SendsToTeacher()
    {
        var mock = NewNotificationServiceMock();
        var handler = new DiscussionQuestionPostedHandler(mock.Object);

        var evt = new DiscussionQuestionPostedEvent("q-1", "lecture-1", "student-1", "teacher-1", "Why is x=2?");
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "teacher-1", It.IsAny<string>(), It.Is<string>(c => c.Contains("Why is x=2?")), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscussionAnsweredHandler_SendsToStudent()
    {
        var mock = NewNotificationServiceMock();
        var handler = new DiscussionAnsweredHandler(mock.Object);

        var evt = new DiscussionAnsweredEvent("answer-1", "q-1", "lecture-1", "teacher-1", "student-1");
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "student-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(true, "Application Approved")]
    [InlineData(false, "Application Rejected")]
    public async Task ApplicationResolvedHandler_SendsToTeacher_WithStatusInTitle(bool approved, string expectedTitle)
    {
        var mock = NewNotificationServiceMock();
        var handler = new ApplicationResolvedHandler(mock.Object);

        var evt = new ApplicationResolvedEvent("app-1", "teacher-1", approved, null);
        await handler.Handle(evt, CancellationToken.None);

        mock.Verify(s => s.SendAsync(
            "teacher-1", expectedTitle, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AccountCreatedHandler_SendsInAppNotification_AndWelcomeEmail()
    {
        var notificationMock = NewNotificationServiceMock();
        var emailMock = new Mock<IEmailService>();
        var handler = new AccountCreatedHandler(notificationMock.Object, emailMock.Object);

        var evt = new AccountCreatedEvent
        {
            AccountId = "account-1",
            Email = "student@example.com",
            Username = "student1",
            RoleName = "Student",
            FirstName = "An",
            LastName = "Nguyen"
        };
        await handler.Handle(evt, CancellationToken.None);

        notificationMock.Verify(s => s.SendAsync(
            "account-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        emailMock.Verify(e => e.SendWelcomeEmailAsync("student@example.com", "An", It.IsAny<CancellationToken>()), Times.Once);
    }
}
