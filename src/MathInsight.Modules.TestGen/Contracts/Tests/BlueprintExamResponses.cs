namespace MathInsight.Modules.TestGen.Contracts.Tests;

public sealed record BlueprintExamOptionResponse(
    string BlueprintId,
    string BlueprintName,
    int Grade,
    int TotalQuestions,
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
    DateTime CreatedTime);
