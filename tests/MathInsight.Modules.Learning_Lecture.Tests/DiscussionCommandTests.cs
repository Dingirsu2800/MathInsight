using System;
using System.Threading;
using System.Threading.Tasks;
using MathInsight.Modules.Learning_Lecture.Commands.Discussions;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class DiscussionCommandTests
{
    [Fact]
    public async Task AskDiscussionQuestion_LectureNotPublished_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var lectureId = Guid.NewGuid().ToString();
        database.Context.Lectures.Add(new Lecture
        {
            LectureId = lectureId,
            Title = "Test",
            TagId = "tag-1",
            TeacherId = "teacher-1",
            Status = "Draft", // Not published
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var mockMediator = new Mock<IMediator>();
        var handler = new AskDiscussionQuestionCommandHandler(database.Context, mockMediator.Object);
        var command = new AskDiscussionQuestionCommand(lectureId, "student-1", "Title", "Content");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Cannot ask questions on non-published lectures", ex.Message);
    }

    [Fact]
    public async Task ReportDiscussion_TargetingQuestion_CreatesPendingReport()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var questionId = Guid.NewGuid().ToString();
        
        database.Context.DiscussionQuestions.Add(new DiscussionQuestion
        {
            DiscussionQuestionId = questionId,
            LectureId = Guid.NewGuid().ToString(),
            StudentId = "student-1",
            Title = "Test",
            Content = "Content",
            Status = "Active",
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new ReportDiscussionCommandHandler(database.Context);
        var command = new ReportDiscussionCommand(questionId, null, "student-2", "Spam");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var report = await database.Context.DiscussionReports.FirstOrDefaultAsync(r => r.ReportId == result.ReportId);
        Assert.NotNull(report);
        Assert.Equal("Pending", report.Status);
        
        // Ensure original question status is unchanged
        var question = await database.Context.DiscussionQuestions.FirstOrDefaultAsync(q => q.DiscussionQuestionId == questionId);
        Assert.Equal("Active", question!.Status);
    }

    [Fact]
    public async Task ReportDiscussion_TargetingBoth_ThrowsException()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var handler = new ReportDiscussionCommandHandler(database.Context);
        
        // DC-06 Validation
        var command = new ReportDiscussionCommand(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "student-2", "Spam");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Exactly one", ex.Message);
    }

    [Fact]
    public async Task ResolveModeration_PendingReport_StatusIsUpdated()
    {
        // Arrange
        await using var database = await LearningInMemoryContext.CreateAsync();
        var reportId = Guid.NewGuid().ToString();
        
        database.Context.DiscussionReports.Add(new DiscussionReport
        {
            ReportId = reportId,
            DiscussionQuestionId = Guid.NewGuid().ToString(),
            ReporterAccountId = "student-1",
            ReportReason = "Spam",
            Status = "Pending",
            CreatedTime = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();

        var handler = new ResolveModerationCommandHandler(database.Context);
        // IsDismissed = true => Status = Dismissed
        var command = new ResolveModerationCommand(reportId, "admin-1", true); 

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        var report = await database.Context.DiscussionReports.FirstOrDefaultAsync(r => r.ReportId == reportId);
        Assert.Equal("Dismissed", report!.Status);
        Assert.Equal("admin-1", report.ResolverAccountId);
    }
}
