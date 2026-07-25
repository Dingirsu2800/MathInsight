using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Gamification.Commands.TargetScores;

public sealed class UpdateTargetScoreCommandHandler : IRequestHandler<UpdateTargetScoreCommand>
{
    private readonly GamificationDbContext _dbContext;

    public UpdateTargetScoreCommandHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateTargetScoreCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate TargetPoint (0.00 to 10.00)
        if (request.TargetPoint < 0 || request.TargetPoint > 10)
        {
            throw new ValidationException("TargetScore must be between 0.00 and 10.00.");
        }

        // 2. Load TargetScore
        var targetScore = await _dbContext.Set<TargetScore>()
            .FirstOrDefaultAsync(t => t.TargetId == request.TargetId, cancellationToken);
        
        if (targetScore == null)
        {
            throw new KeyNotFoundException($"TargetScore with ID {request.TargetId} not found.");
        }

        // 3. Verify ownership (BR-31 equivalent, student can only edit their own target)
        if (targetScore.StudentId != request.StudentId)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this TargetScore.");
        }

        // 4. Update
        targetScore.TargetPoint = request.TargetPoint;
        targetScore.UpdatedTime = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
