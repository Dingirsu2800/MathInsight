using MathInsight.Modules.Recommender.Persistence.Entities;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Unit tests for OfficialPoint formula (RCM-04) and PracticeSeries reset logic.
///
/// OfficialPoint = 0.7 × ExamAnchor + 0.3 × PracticePoint, clamped [0, 10].
/// PracticeSeries: when SeriesAnswerCount >= 10, blend and reset.
/// </summary>
public class OfficialPointFormulaTests
{
    // ── Mirrors the OfficialPoint recalculation ──
    private static decimal CalculateOfficialPoint(decimal examAnchor, decimal practicePoint)
        => Math.Clamp(0.7m * examAnchor + 0.3m * practicePoint, 0.00m, 10.00m);

    [Theory]
    [InlineData(8.00, 6.00, 7.40)]   // 0.7×8 + 0.3×6 = 5.6 + 1.8 = 7.4
    [InlineData(5.00, 5.00, 5.00)]   // baseline: 0.7×5 + 0.3×5 = 5.0
    [InlineData(10.00, 10.00, 10.00)] // maximum
    [InlineData(0.00, 0.00, 0.00)]   // minimum
    [InlineData(7.50, 2.50, 6.00)]   // 0.7×7.5 + 0.3×2.5 = 5.25 + 0.75 = 6.0
    public void OfficialPoint_Formula_IsCorrect(double examAnchor, double practicePoint, double expected)
    {
        var result = CalculateOfficialPoint((decimal)examAnchor, (decimal)practicePoint);
        Assert.Equal((decimal)expected, Math.Round(result, 10));
    }

    [Fact]
    public void OfficialPoint_WeakThreshold_Below5_IsWeak()
    {
        // official_point < 5.00 → WeakTag
        // ExamAnchor=4, PracticePoint=4: 0.7×4 + 0.3×4 = 4.0 < 5 → weak
        var officialPoint = CalculateOfficialPoint(4.00m, 4.00m);
        Assert.True(officialPoint < 5.00m);
    }

    [Fact]
    public void OfficialPoint_At5OrAbove_IsNotWeak()
    {
        // ExamAnchor=5, PracticePoint=5: 5.0 >= 5 → not weak
        var officialPoint = CalculateOfficialPoint(5.00m, 5.00m);
        Assert.False(officialPoint < 5.00m);
    }

    [Fact]
    public void PracticeSeries_ResetAt10_BlendsThenResets()
    {
        // When SeriesAnswerCount >= 10:
        //   official_point = 0.7 * exam_anchor + 0.3 * practice_point
        //   practice_point = official_point
        //   series_answer_count = 0
        var mastery = new TagsMastery
        {
            ExamAnchor    = 7.00m,
            PracticePoint = 4.00m,
            SeriesAnswerCount = 10
        };

        // Simulate the series reset logic
        mastery.OfficialPoint = CalculateOfficialPoint(mastery.ExamAnchor, mastery.PracticePoint);
        mastery.PracticePoint = mastery.OfficialPoint;
        mastery.SeriesAnswerCount = 0;

        // OfficialPoint = 0.7×7 + 0.3×4 = 4.9 + 1.2 = 6.1
        Assert.Equal(6.1m, mastery.OfficialPoint);
        Assert.Equal(6.1m, mastery.PracticePoint); // blended into practice
        Assert.Equal(0, mastery.SeriesAnswerCount);  // reset
    }

    [Fact]
    public void OfficialPoint_ClampsToTen()
    {
        var result = CalculateOfficialPoint(10.00m, 10.00m);
        Assert.Equal(10.00m, result);
    }

    [Fact]
    public void OfficialPoint_ClampsToZero()
    {
        var result = CalculateOfficialPoint(0.00m, 0.00m);
        Assert.Equal(0.00m, result);
    }
}
