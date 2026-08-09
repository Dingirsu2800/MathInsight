using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;
using MathInsight.Modules.Learning_Lecture.Events;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class HideDiscussionCommentCommandHandler : IRequestHandler<HideDiscussionCommentCommand, bool>
{
    private readonly LearningDbContext _dbContext;
    private readonly IMediator _mediator;

    public HideDiscussionCommentCommandHandler(LearningDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<bool> Handle(HideDiscussionCommentCommand request, CancellationToken cancellationToken)
    {
        string targetAccountId;
        string lectureId;

        // Teacher/Admin authorization check happens at API layer / via role claim
        if (request.IsQuestion)
        {
            var question = await _dbContext.DiscussionQuestions.FirstOrDefaultAsync(x => x.DiscussionQuestionId == request.Id, cancellationToken);
            if (question == null) throw new Exception("Question not found");
            question.Status = "Hidden";
            question.UpdatedTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Reason))
                question.ModerationReason = request.Reason;
                
            targetAccountId = question.StudentId;
            lectureId = question.LectureId;
        }
        else
        {
            var answer = await _dbContext.DiscussionAnswers
                .Include(x => x.Question)
                .FirstOrDefaultAsync(x => x.DiscussionAnswerId == request.Id, cancellationToken);
            if (answer == null) throw new Exception("Answer not found");
            answer.Status = "Hidden";
            answer.UpdatedTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Reason))
                answer.ModerationReason = request.Reason;
                
            targetAccountId = answer.AccountId;
            lectureId = answer.Question.LectureId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        await _mediator.Publish(new DiscussionCommentHiddenEvent(
            targetAccountId,
            lectureId,
            request.IsQuestion,
            request.Reason
        ), cancellationToken);

        return true;
    }
}
