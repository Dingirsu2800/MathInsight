using MediatR;

namespace MathInsight.Modules.Gamification.Commands.TargetScores;

public sealed record UpdateTargetScoreCommand(
    string TargetId,
    string StudentId,
    decimal TargetPoint) : IRequest;
