namespace MathInsight.Shared.Events;

public record GradeCalculatedEvent
{
    public Guid SessionId { get; init; }
    public Guid StudentId { get; init; }
    public Guid TestId { get; init; }
    public double Score { get; init; }
    public int NumCorrect { get; init; }
    public int NumIncorrect { get; init; }
    public List<string> WeakTags { get; init; } = new();
    public DateTime GradedTime { get; init; }
}