namespace MathInsight.Modules.Testing.Entities;

public class TestAnswerOption
{
    public string TestAnswerId { get; set; } = string.Empty;
    public string AnswerId { get; set; } = string.Empty;

    // Navigation
    public TestAnswer? TestAnswer { get; set; }
}
