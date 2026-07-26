using MathInsight.Modules.Notification_Report.Contracts;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.Notification_Report.Queries.GetLeaderboard;

public sealed record GetLeaderboardQuery(int Grade, int Top = 50) : IRequest<Result<IReadOnlyList<LeaderboardEntryDto>>>;
