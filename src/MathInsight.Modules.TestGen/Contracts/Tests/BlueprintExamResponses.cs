namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed record BlueprintExamOptionResponse(
    string BlueprintId,
    string BlueprintName,
    int Grade,
    int TotalQuestions,
    decimal TotalScore,
    int DurationMinutes,
    string Status,
    int SectionCount);

public sealed record GenerateBlueprintExamResponse(
    string TestId,
    string BlueprintId,
    string TestMode,
    string TestName,
    int DurationMinutes,
    int TotalQuestions,
    decimal MaxScore,
    string ScoringPolicy,
    DateTime CreatedTime);
