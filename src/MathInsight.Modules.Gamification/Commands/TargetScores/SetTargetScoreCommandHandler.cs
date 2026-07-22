using MathInsight.Modules.Gamification.Entities;
using MathInsight.Modules.Gamification.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Gamification.Commands.TargetScores;

public sealed class SetTargetScoreCommandHandler : IRequestHandler<SetTargetScoreCommand, string>
{
    private readonly GamificationDbContext _dbContext;

    public SetTargetScoreCommandHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> Handle(SetTargetScoreCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate TargetPoint (0.00 to 10.00)
        if (request.TargetPoint < 0 || request.TargetPoint > 10)
        {
            throw new ValidationException("TargetScore must be between 0.00 and 10.00.");
        }

        // 2. Check duplicate (BR-44: only one target per TagID for a Student)
        var exists = await _dbContext.Set<TargetScore>()
            .AnyAsync(t => t.StudentId == request.StudentId && t.TagId == request.TagId, cancellationToken);
        
        if (exists)
        {
            // Throw exception to return 409 Conflict in API
            throw new InvalidOperationException($"TargetScore for TagID {request.TagId} already exists for this student.");
        }

        // 3. Create
        var newTargetId = Guid.NewGuid().ToString("N");
        var targetScore = new TargetScore
        {
            TargetId = newTargetId,
            StudentId = request.StudentId,
            TagId = request.TagId,
            TargetPoint = request.TargetPoint,
            CreatedTime = DateTime.UtcNow,
            UpdatedTime = DateTime.UtcNow
        };

        _dbContext.Set<TargetScore>().Add(targetScore);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return newTargetId;
    }
}
