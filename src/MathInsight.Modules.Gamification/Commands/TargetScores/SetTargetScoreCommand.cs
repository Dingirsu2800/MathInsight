using MediatR;

namespace MathInsight.Modules.Gamification.Commands.TargetScores;

public sealed record SetTargetScoreCommand(
    string StudentId,
    string TagId,
    decimal TargetPoint) : IRequest<string>;
