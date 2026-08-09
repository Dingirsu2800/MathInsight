using MathInsight.Modules.Gamification.Contracts;
using MathInsight.Modules.Gamification.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Queries.GetHeatmap;

public class GetHeatmapQueryHandler : IRequestHandler<GetHeatmapQuery, Result<StudyHeatmapDto>>
{
    private readonly GamificationDbContext _dbContext;

    public GetHeatmapQueryHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<StudyHeatmapDto>> Handle(GetHeatmapQuery request, CancellationToken cancellationToken)
    {
        // Calculate 84 days ago (12 weeks)
        var startDate = DateTime.UtcNow.Date.AddDays(-84);

        var activities = await _dbContext.ActivityLogs
            .Where(a => a.StudentId == request.StudentId && a.ActivityDate >= startDate)
            .GroupBy(a => a.ActivityDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var dto = new StudyHeatmapDto();
        
        foreach (var act in activities)
        {
            dto.Days.Add(new HeatmapDayDto
            {
                Date = act.Date.ToString("yyyy-MM-dd"),
                ActivityCount = act.Count
            });
        }

        return Result<StudyHeatmapDto>.Success(dto);
    }
}
