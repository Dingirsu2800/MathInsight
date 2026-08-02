namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed class GenerateTopicPracticeRequest { public string TagId { get; set; } = string.Empty; }

public sealed record GenerateTopicPracticeResponse(
    string TestId,
    string SelectedTagId,
    string SelectedTagName,
    string TestName,
    string TestMode,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    string ScoringPolicy,
    DateTime CreatedTime,
    bool WasAdaptive,
    string? WeakTagId,
    string? WeakTagName,
    byte? RecommendedDifficultyLevel,
    int AdaptiveQuestionCount,
    int FallbackQuestionCount,
    string RuleVersion);
