using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Learning_Lecture.Persistence;

namespace MathInsight.Modules.Learning_Lecture.Commands.Discussions;

public class DeleteDiscussionCommentCommandHandler : IRequestHandler<DeleteDiscussionCommentCommand, bool>
{
    private readonly LearningDbContext _dbContext;

    public DeleteDiscussionCommentCommandHandler(LearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteDiscussionCommentCommand request, CancellationToken cancellationToken)
    {
        if (request.IsQuestion)
        {
            var question = await _dbContext.DiscussionQuestions.FirstOrDefaultAsync(x => x.DiscussionQuestionId == request.Id, cancellationToken);
            if (question == null) throw new Exception("Question not found");
            if (!request.IsTeacherOrAdmin && question.StudentId != request.AccountId) throw new Exception("Forbidden");
            
            question.Status = "Deleted";
            question.UpdatedTime = DateTime.UtcNow;
        }
        else
        {
            var answer = await _dbContext.DiscussionAnswers.FirstOrDefaultAsync(x => x.DiscussionAnswerId == request.Id, cancellationToken);
            if (answer == null) throw new Exception("Answer not found");
            if (!request.IsTeacherOrAdmin && answer.AccountId != request.AccountId) throw new Exception("Forbidden");

            answer.Status = "Deleted";
            answer.UpdatedTime = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
