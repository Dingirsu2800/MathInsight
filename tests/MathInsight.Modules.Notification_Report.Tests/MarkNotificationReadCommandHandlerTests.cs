using MathInsight.Modules.Notification_Report.Commands.MarkNotificationRead;
using MathInsight.Modules.Notification_Report.Errors;
using MathInsight.Modules.Notification_Report.Services;
using MathInsight.Shared.Results;
using Moq;
using Xunit;

namespace MathInsight.Modules.Notification_Report.Tests;

public class MarkNotificationReadCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToNotificationService_WithSameParameters_OnSuccess()
    {
        var mock = new Mock<INotificationService>();
        mock.Setup(s => s.MarkReadAsync("n1", "acc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        var handler = new MarkNotificationReadCommandHandler(mock.Object);

        var result = await handler.Handle(new MarkNotificationReadCommand("n1", "acc-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mock.Verify(s => s.MarkReadAsync("n1", "acc-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsServiceFailureUnchanged()
    {
        var mock = new Mock<INotificationService>();
        mock.Setup(s => s.MarkReadAsync("n1", "acc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure(NotificationErrors.NotificationAccessForbidden));
        var handler = new MarkNotificationReadCommandHandler(mock.Object);

        var result = await handler.Handle(new MarkNotificationReadCommand("n1", "acc-1"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NotificationAccessForbidden, result.Error);
    }
}
