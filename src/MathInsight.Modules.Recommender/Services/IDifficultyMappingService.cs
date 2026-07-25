namespace MathInsight.Modules.Recommender.Services;

/// <summary>
/// Maps <c>OfficialPoint</c> to recommended difficulty level (RCM-07)
/// and provides weak/remedial classification helpers.
/// </summary>
public interface IDifficultyMappingService
{
    /// <summary>
    /// Returns the recommended difficulty level (1–4) based on official point (RCM-07).
    /// <list type="table">
    ///   <item><term>0.00 ≤ p &lt; 3.00</term><description>Level 1</description></item>
    ///   <item><term>3.00 ≤ p &lt; 5.00</term><description>Level 2</description></item>
    ///   <item><term>5.00 ≤ p &lt; 7.50</term><description>Level 3</description></item>
    ///   <item><term>7.50 ≤ p ≤ 10.00</term><description>Level 4</description></item>
    /// </list>
    /// </summary>
    byte MapFromOfficialPoint(decimal officialPoint);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="officialPoint"/> &lt; 5.00 (RCM-03).
    /// </summary>
    bool IsWeak(decimal officialPoint);

    /// <summary>
    /// Returns <c>true</c> when the student is at the easiest difficulty (level 1) and is weak.
    /// </summary>
    bool IsRemedial(byte recommendedDifficultyLevel, decimal officialPoint);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="officialPoint"/> &lt; 4.00 (BR-19, RCM-14).
    /// A secondary (sub) tag below this threshold creates a bottleneck risk for completing
    /// questions that require it, even if the primary tag is above the standard weak threshold.
    /// </summary>
    bool IsBottleneckWeak(decimal officialPoint);
}
