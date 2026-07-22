namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// Pure-logic implementation of difficulty mapping (RCM-07).
/// No dependencies — all methods are deterministic calculations.
/// </summary>
public sealed class DifficultyMappingService : IDifficultyMappingService
{
    private const decimal WeakThreshold = 5.00m;
    private const decimal BottleneckThreshold = 4.00m; // BR-19, RCM-14

    /// <inheritdoc />
    public byte MapFromOfficialPoint(decimal officialPoint) => officialPoint switch
    {
        < 3.00m => 1,
        < 5.00m => 2,
        < 7.50m => 3,
        _       => 4
    };

    /// <inheritdoc />
    public bool IsWeak(decimal officialPoint) => officialPoint < WeakThreshold;

    /// <inheritdoc />
    public bool IsRemedial(byte recommendedDifficultyLevel, decimal officialPoint)
        => recommendedDifficultyLevel == 1 && IsWeak(officialPoint);

    /// <inheritdoc />
    public bool IsBottleneckWeak(decimal officialPoint) => officialPoint < BottleneckThreshold;
}
