using MathInsight.Modules.Grading_Analytics.Persistence.Entities;

namespace MathInsight.Modules.Grading_Analytics.Services;

/// <summary>
/// Grading algorithm interface.
/// Grades all answers in a session synchronously, determines per-answer correctness
/// and points, and produces session-level aggregate results.
/// </summary>
public interface IGradingEngine
{
    /// <summary>
    /// Grades all answers for a given session. Mutates TestAnswer and TestAnswerPart entities
    /// in-place (IsCorrect, PointsEarned). Returns aggregate session results.
    /// </summary>
    /// <param name="session">The session to grade (must include TestAnswers with related entities loaded).</param>
    /// <returns>Aggregate grading result for the session.</returns>
    GradingResult Grade(TestSession session);
}

/// <summary>
/// Aggregate result produced by the grading engine for a single session.
/// </summary>
public sealed record GradingResult
{
    /// <summary>Normalized score 0.00–10.00: SUM(points_earned) / SUM(max_points) × 10.0 (BR-20).</summary>
    public decimal Score { get; init; }

    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }

    /// <summary>Count of abandoned/unanswered questions per BR-16b.</summary>
    public int NumAbandoned { get; init; }
}
