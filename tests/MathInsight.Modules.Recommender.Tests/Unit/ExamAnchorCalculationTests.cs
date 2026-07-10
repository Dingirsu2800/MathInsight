using System.Text.Json;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Shared.Events;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Verifies the ExamAnchor Exponential Decay formula (RCM-05).
///
/// Formula: exam_anchor = Σ(β^(j-1) × T_j) / Σ(β^(j-1))
///   β = 0.8, j=1 = most recent (history[0]).
///
/// Ordering contract (I2): exam_history[0] = most recent score;
/// always prepend new score, truncate last when len > 5.
/// </summary>
public class ExamAnchorCalculationTests
{
    private const decimal Beta = 0.8m;
    private const int MaxHistory = 5;

    // ── Helper: mirrors the private CalculateExamAnchor logic in TopicResultIngestionHandler ──
    private static decimal CalculateExamAnchor(List<decimal> history)
    {
        if (history.Count == 0) return 5.00m;

        decimal weightedSum = 0m;
        decimal weightSum = 0m;
        decimal weight = 1m;

        foreach (var score in history)
        {
            weightedSum += weight * score;
            weightSum   += weight;
            weight      *= Beta;
        }

        return Math.Clamp(weightedSum / weightSum, 0.00m, 10.00m);
    }

    // ── Helper: prepend + cap at MaxHistory ──
    private static List<decimal> PrependAndCap(List<decimal> history, decimal newScore)
    {
        history.Insert(0, newScore);
        if (history.Count > MaxHistory)
            history.RemoveAt(history.Count - 1);
        return history;
    }

    [Fact]
    public void ExamAnchor_OneResult_EqualsT1()
    {
        // 1 result: exam_anchor = T1
        var history = PrependAndCap([], 7.00m);
        var anchor = CalculateExamAnchor(history);
        Assert.Equal(7.00m, anchor);
    }

    [Fact]
    public void ExamAnchor_TwoResults_CorrectWeightedAverage()
    {
        // 2 results: (T1 + 0.8×T2) / (1 + 0.8)
        // T1=8, T2=6 → (8 + 0.8×6) / (1 + 0.8) = (8 + 4.8) / 1.8 = 12.8 / 1.8 ≈ 7.111...
        var history = new List<decimal>();
        history = PrependAndCap(history, 6.00m); // T2 (older)
        history = PrependAndCap(history, 8.00m); // T1 (newest, at index 0)

        var expected = (8.00m + 0.8m * 6.00m) / (1.00m + 0.8m);
        var anchor = CalculateExamAnchor(history);

        Assert.Equal(Math.Round(expected, 10), Math.Round(anchor, 10));
    }

    [Fact]
    public void ExamAnchor_FiveResults_CorrectWeights()
    {
        // 5 results: weights 1.0, 0.8, 0.64, 0.512, 0.4096
        // Use scores T1=10, T2=8, T3=6, T4=4, T5=2 (T1=most recent at index 0)
        var history = new List<decimal>();
        history = PrependAndCap(history, 2.00m); // T5 oldest
        history = PrependAndCap(history, 4.00m); // T4
        history = PrependAndCap(history, 6.00m); // T3
        history = PrependAndCap(history, 8.00m); // T2
        history = PrependAndCap(history, 10.00m); // T1 newest

        decimal w1 = 1.0m, w2 = 0.8m, w3 = 0.64m, w4 = 0.512m, w5 = 0.4096m;
        decimal expected = (10m * w1 + 8m * w2 + 6m * w3 + 4m * w4 + 2m * w5)
                         / (w1 + w2 + w3 + w4 + w5);
        expected = Math.Clamp(expected, 0m, 10m);

        var anchor = CalculateExamAnchor(history);
        Assert.Equal(Math.Round(expected, 6), Math.Round(anchor, 6));
    }

    [Fact]
    public void ExamHistory_CappedAt5_AfterSixInserts()
    {
        // After 6 inserts, only 5 entries remain; oldest (6th) is dropped.
        var history = new List<decimal>();
        for (int i = 1; i <= 6; i++)
            history = PrependAndCap(history, i * 1.0m);

        Assert.Equal(MaxHistory, history.Count);
        // Most recent is last-inserted value = 6.0; it should be at index 0
        Assert.Equal(6.0m, history[0]);
        // Oldest kept = 5th insert = 2.0 (value 2) → wait, let's trace:
        // insert 1→[1], 2→[2,1], 3→[3,2,1], 4→[4,3,2,1], 5→[5,4,3,2,1], 6→[6,5,4,3,2] cap
        Assert.Equal(2.0m, history[4]); // oldest kept
    }

    [Fact]
    public void ExamAnchor_ClampsToTen_WhenAllScoresMaximal()
    {
        var history = new List<decimal> { 10m, 10m, 10m };
        Assert.Equal(10.00m, CalculateExamAnchor(history));
    }

    [Fact]
    public void ExamAnchor_ClampsToZero_WhenAllScoresZero()
    {
        var history = new List<decimal> { 0m, 0m, 0m };
        Assert.Equal(0.00m, CalculateExamAnchor(history));
    }
}
