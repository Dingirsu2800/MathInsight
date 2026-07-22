using Dapper;
using MathInsight.Modules.Gamification.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Gamification.Queries.TargetScores;

public sealed class GetTargetProgressQueryHandler : IRequestHandler<GetTargetProgressQuery, List<TargetProgressDto>>
{
    private readonly GamificationDbContext _dbContext;

    public GetTargetProgressQueryHandler(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TargetProgressDto>> Handle(GetTargetProgressQuery request, CancellationToken cancellationToken)
    {
        // Cross-read across Gamification (TargetScore), QuestionBank (TagTopic), and Recommender (TagsMastery)
        // This relies on the unified database architecture of the modular monolith.
        const string sql = """
            SELECT 
                ts.TargetID as TargetId,
                ts.TagID as TagId,
                tt.TagName as TagName,
                ts.TargetPoint as TargetPoint,
                ISNULL(tm.OfficialPoint, 0) as CurrentPoint,
                CASE WHEN ISNULL(tm.OfficialPoint, 0) >= ts.TargetPoint THEN 1 ELSE 0 END as IsAchieved,
                ts.CreatedTime as CreatedTime
            FROM [TargetScore] ts
            LEFT JOIN [TagTopic] tt ON ts.TagID = tt.TagID
            LEFT JOIN [TagsMastery] tm ON ts.StudentID = tm.StudentID AND ts.TagID = tm.TagId
            WHERE ts.StudentID = @StudentId
            ORDER BY ts.CreatedTime DESC
        """;

        var connection = _dbContext.Database.GetDbConnection();
        var results = await connection.QueryAsync<TargetProgressDto>(sql, new { StudentId = request.StudentId });

        return results.AsList();
    }
}
