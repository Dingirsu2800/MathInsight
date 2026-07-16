using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Entities;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Events;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class AskDiscussionQuestionCommandHandler : IRequestHandler<AskDiscussionQuestionCommand, DiscussionQuestionDto>
{
    private readonly LearningDbContext _dbContext;
    private readonly IMediator _mediator;

    public AskDiscussionQuestionCommandHandler(LearningDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<DiscussionQuestionDto> Handle(AskDiscussionQuestionCommand request, CancellationToken cancellationToken)
    {
        var lecture = await _dbContext.Lectures.FirstOrDefaultAsync(x => x.LectureId == request.LectureId, cancellationToken);
        if (lecture == null) throw new Exception("Lecture not found");
        if (lecture.Status != "Published") throw new Exception("Cannot ask questions on non-published lectures");

        var question = new DiscussionQuestion
        {
            DiscussionQuestionId = Guid.NewGuid().ToString(),
            LectureId = request.LectureId,
            StudentId = request.StudentId,
            Title = request.Title,
            Content = request.Content,
            Status = "Active",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };

        _dbContext.DiscussionQuestions.Add(question);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new DiscussionQuestionPostedEvent(
            question.DiscussionQuestionId,
            lecture.LectureId,
            question.StudentId,
            lecture.TeacherId,
            question.Title
        ), cancellationToken);

        return new DiscussionQuestionDto
        {
            DiscussionQuestionId = question.DiscussionQuestionId,
            LectureId = question.LectureId,
            StudentId = question.StudentId,
            Title = question.Title,
            Content = question.Content,
            Status = question.Status,
            CreatedTime = question.CreatedTime,
            UpdatedTime = question.UpdatedTime
        };
    }
}
