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

public class AnswerDiscussionQuestionCommandHandler : IRequestHandler<AnswerDiscussionQuestionCommand, DiscussionAnswerDto>
{
    private readonly LearningDbContext _dbContext;
    private readonly IMediator _mediator;

    public AnswerDiscussionQuestionCommandHandler(LearningDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<DiscussionAnswerDto> Handle(AnswerDiscussionQuestionCommand request, CancellationToken cancellationToken)
    {
        var question = await _dbContext.DiscussionQuestions.FirstOrDefaultAsync(x => x.DiscussionQuestionId == request.DiscussionQuestionId, cancellationToken);
        if (question == null) throw new Exception("Question not found");
        if (question.Status != "Active") throw new Exception("Cannot answer inactive questions");

        var answer = new DiscussionAnswer
        {
            DiscussionAnswerId = Guid.NewGuid().ToString(),
            DiscussionQuestionId = request.DiscussionQuestionId,
            AccountId = request.AccountId,
            Content = request.Content,
            Status = "Active",
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };

        _dbContext.DiscussionAnswers.Add(answer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _mediator.Publish(new DiscussionAnsweredEvent(
            answer.DiscussionAnswerId,
            question.DiscussionQuestionId,
            answer.AccountId,
            question.StudentId
        ), cancellationToken);

        return new DiscussionAnswerDto
        {
            DiscussionAnswerId = answer.DiscussionAnswerId,
            AccountId = answer.AccountId,
            Content = answer.Content,
            Status = answer.Status,
            CreatedTime = answer.CreatedTime,
            UpdatedTime = answer.UpdatedTime
        };
    }
}
