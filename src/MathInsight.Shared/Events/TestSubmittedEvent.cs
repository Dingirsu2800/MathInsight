namespace MathInsight.Shared.Events;

public record TestSubmittedEvent : MediatR.INotification
{
    public string SessionId { get; init; } = string.Empty;
    public string StudentId { get; init; } = string.Empty;
    public string TestId { get; init; } = string.Empty;
    public string TestFormat { get; init; } = "Practice"; // "Practice" or "Exam"
    public string SubmissionType { get; init; } = "StudentSubmit";
    public Dictionary<string, string> Answers { get; init; } = new(); // QuestionId -> StudentAnswer
    public DateTime SubmittedTime { get; init; }
}
