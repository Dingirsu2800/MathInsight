namespace MathInsight.Modules.Gamification.Enums;

/// <summary>
/// Badge award condition (spec.md:73, BR-43). Persisted and compared as VARCHAR(50) by enum
/// NAME, so member names must match the exact string values in the ConditionType column.
/// </summary>
public enum ConditionType
{
    TOTAL_CORRECT_ANSWERS,
    STREAK_DAYS,
    TESTS_COMPLETED
}
