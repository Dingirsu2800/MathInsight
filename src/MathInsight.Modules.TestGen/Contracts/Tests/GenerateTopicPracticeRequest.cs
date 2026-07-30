namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed class GenerateTopicPracticeRequest { public string TagId { get; set; } = string.Empty; }

public sealed record GenerateTopicPracticeResponse(string TestId, string TestMode, int DurationMinutes, int TotalQuestions, decimal MaxScore, string ScoringPolicy, DateTime CreatedTime);
