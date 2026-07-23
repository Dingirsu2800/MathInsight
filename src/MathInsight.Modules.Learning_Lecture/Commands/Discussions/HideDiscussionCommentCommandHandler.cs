using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class HideDiscussionCommentCommandHandler : IRequestHandler<HideDiscussionCommentCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public HideDiscussionCommentCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(HideDiscussionCommentCommand request, CancellationToken cancellationToken)
    {
        // Teacher/Admin authorization check happens at API layer / via role claim
        if (request.IsQuestion)
        {
            var question = await _dbContext.DiscussionQuestions.FirstOrDefaultAsync(x => x.DiscussionQuestionId == request.Id, cancellationToken);
            if (question == null) throw new Exception("Question not found");
            question.Status = "Hidden";
            question.UpdatedTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Reason))
                question.ModerationReason = request.Reason;
        }
        else
        {
            var answer = await _dbContext.DiscussionAnswers.FirstOrDefaultAsync(x => x.DiscussionAnswerId == request.Id, cancellationToken);
            if (answer == null) throw new Exception("Answer not found");
            answer.Status = "Hidden";
            answer.UpdatedTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.Reason))
                answer.ModerationReason = request.Reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
