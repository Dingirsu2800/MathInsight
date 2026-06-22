namespace MathInsight.Shared.Events;

public record TestSubmittedEvent : MediatR.INotification
{
    public Guid SessionId { get; init; }
    public Guid StudentId { get; init; }
    public Guid TestId { get; init; }
    public string TestFormat { get; init; } = "Practice"; // "Practice" or "Exam"
    public Dictionary<Guid, string> Answers { get; init; } = new(); // QuestionId -> StudentAnswer
    public DateTime SubmittedTime { get; init; }
}