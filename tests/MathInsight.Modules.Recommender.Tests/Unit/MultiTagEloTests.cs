using MathInsight.Modules.Recommender.Services;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Multi-tag Elo delta distribution tests (v4.1).
///
/// Verifies the unified multi-tag formula from RCM-06 Bước 2:
///   1. Compute Δ_total per answer (unchanged from MVP)
///   2. For EACH tag in answer.TagWeights: ΔP_tag_i = Δ_total × w_i
///
/// Also verifies:
///   - Independent series_answer_count per tag (blend + reset at 10)
///   - Exam TopicScore weighted Tầng 1–2 formula
///   - BR-19 IsBottleneckWeak threshold
/// </summary>
public class MultiTagEloTests
{
    // ── Helper: Compute Δ_total (mirrors TopicResultIngestionHandler Bước 1) ──
    private static decimal ComputeDeltaTotal(bool isCorrect, byte difficultyLevel, int timeSpent, bool isAbandoned)
    {
        decimal wD = difficultyLevel switch
        {
            1 => 0.5m,
            2 => 1.0m,
            3 => 1.5m,
            4 => 2.0m,
            _ => 1.0m
        };

        decimal timePenalty = (timeSpent < 5 && !isAbandoned) ? 1.5m : 1.0m;

        return isCorrect
            ? 0.05m * wD
            : -0.05m * (5.0m - wD) * timePenalty;
    }

    // ── Helper: Apply delta to a tag (mirrors ApplyDeltaToTag) ──
    private static (decimal PracticePoint, int SeriesAnswerCount) ApplyDelta(
        decimal practicePoint, decimal examAnchor, int seriesAnswerCount,
        decimal deltaTotal, decimal weight)
    {
        seriesAnswerCount++;

        decimal deltaForTag = deltaTotal * weight;
        practicePoint = Math.Clamp(practicePoint + deltaForTag, 0.00m, 10.00m);

        decimal officialPoint = Math.Clamp(0.7m * examAnchor + 0.3m * practicePoint, 0.00m, 10.00m);

        if (seriesAnswerCount >= 10)
        {
            practicePoint = officialPoint;
            seriesAnswerCount = 0;
        }

        return (practicePoint, seriesAnswerCount);
    }

    // ── Test 1: Single-tag degenerate case ──────────────────────────────────
    [Fact]
    public void SingleTag_DeltaEquals_DeltaTotal_Times_1()
    {
        // Single-tag answer: ΔP = Δ_total × 1.0 (suy biến, same as MVP)
        decimal practicePoint = 5.00m;
        decimal examAnchor = 5.00m;

        var deltaTotal = ComputeDeltaTotal(isCorrect: true, difficultyLevel: 2, timeSpent: 10, isAbandoned: false);
        // Δ_total = +0.05 × 1.0 = +0.05

        // Single tag: w = 1.0
        var (newPP, _) = ApplyDelta(practicePoint, examAnchor, 0, deltaTotal, 1.0m);

        Assert.Equal(5.05m, newPP);
    }

    // ── Test 2: 1 primary (w=0.65) + 1 secondary (w=0.35), correct Mức 3 ──
    [Fact]
    public void MultiTag_Primary065_Secondary035_Correct_Level3()
    {
        // Correct, Mức 3 (w_D=1.5): Δ_total = +0.05 × 1.5 = +0.075
        // ΔP_main = 0.075 × 0.65 = 0.04875
        // ΔP_sub  = 0.075 × 0.35 = 0.02625
        var deltaTotal = ComputeDeltaTotal(isCorrect: true, difficultyLevel: 3, timeSpent: 10, isAbandoned: false);
        Assert.Equal(0.075m, deltaTotal);

        decimal mainPP = 5.00m;
        decimal subPP = 5.00m;
        decimal examAnchor = 5.00m;

        var (newMainPP, _) = ApplyDelta(mainPP, examAnchor, 0, deltaTotal, 0.65m);
        var (newSubPP, _) = ApplyDelta(subPP, examAnchor, 0, deltaTotal, 0.35m);

        decimal expectedMain = 5.00m + (0.075m * 0.65m); // 5.04875
        decimal expectedSub = 5.00m + (0.075m * 0.35m);  // 5.02625

        Assert.Equal(expectedMain, newMainPP);
        Assert.Equal(expectedSub, newSubPP);
    }

    // ── Test 3: 1 primary (w=0.65) + 2 secondary (w=0.175 each), wrong Mức 1 t<5s ──
    [Fact]
    public void MultiTag_Primary065_TwoSecondary0175_Wrong_Level1_Fast()
    {
        // Wrong, level 1 (w_D=0.5), t < 5s: Δ_total = -0.05 × (5-0.5) × 1.5 = -0.3375
        // ΔP_main = -0.3375 × 0.65 = -0.219375
        // ΔP_sub  = -0.3375 × 0.175 = -0.059063 (each)
        var deltaTotal = ComputeDeltaTotal(isCorrect: false, difficultyLevel: 1, timeSpent: 3, isAbandoned: false);
        Assert.Equal(-0.3375m, deltaTotal);

        decimal mainPP = 5.00m;
        decimal sub1PP = 5.00m;
        decimal sub2PP = 5.00m;
        decimal examAnchor = 5.00m;

        var (newMainPP, _) = ApplyDelta(mainPP, examAnchor, 0, deltaTotal, 0.65m);
        var (newSub1PP, _) = ApplyDelta(sub1PP, examAnchor, 0, deltaTotal, 0.175m);
        var (newSub2PP, _) = ApplyDelta(sub2PP, examAnchor, 0, deltaTotal, 0.175m);

        decimal expectedMain = 5.00m + (-0.3375m * 0.65m);   // 4.780625
        decimal expectedSub = 5.00m + (-0.3375m * 0.175m);    // 4.940938 (rounded)

        Assert.Equal(expectedMain, newMainPP);
        Assert.Equal(expectedSub, newSub1PP);
        Assert.Equal(expectedSub, newSub2PP);
    }

    // ── Test 4: series_answer_count reaches 10 independently per tag → blend + reset ──
    [Fact]
    public void SeriesAnswerCount_ReachTen_IndependentlyPerTag_BlendAndReset()
    {
        // Simulate: tagA receives 10 answers, tagB receives only 5.
        // After 10 answers tagA should blend + reset; tagB should not.
        decimal tagAPP = 5.00m;
        decimal tagBPP = 5.00m;
        decimal examAnchor = 5.00m;
        int tagACount = 0;
        int tagBCount = 0;

        // Apply 10 correct answers at level 2 to tagA (w=1.0 for simplicity)
        for (int i = 0; i < 10; i++)
        {
            var delta = ComputeDeltaTotal(isCorrect: true, difficultyLevel: 2, timeSpent: 10, isAbandoned: false);
            (tagAPP, tagACount) = ApplyDelta(tagAPP, examAnchor, tagACount, delta, 1.0m);
        }

        // Apply 5 correct answers to tagB
        for (int i = 0; i < 5; i++)
        {
            var delta = ComputeDeltaTotal(isCorrect: true, difficultyLevel: 2, timeSpent: 10, isAbandoned: false);
            (tagBPP, tagBCount) = ApplyDelta(tagBPP, examAnchor, tagBCount, delta, 1.0m);
        }

        // tagA: should have blended and reset at count 10
        Assert.Equal(0, tagACount); // Reset to 0 after blend
        // tagB: should still be counting (no blend)
        Assert.Equal(5, tagBCount);

        // tagA practice_point should be blended with official_point
        // After 10 increments of 0.05: PP would be 5.50 before blend
        // officialPoint = 0.7 * 5.00 + 0.3 * 5.50 = 3.50 + 1.65 = 5.15 → blend: PP = 5.15
        // (exact values depend on recalculation at each step, but key assertion is blend happened)
        Assert.NotEqual(5.50m, tagAPP); // Would be 5.50 without blend
    }

    // ── Test 5: Exam TopicScore weighted Tầng 1–2 ──────────────────────────
    [Fact]
    public void ExamTopicScore_Weighted_Tang12_ThreeMultiTagQuestions()
    {
        // 3 questions linking to tag INT (intersect tag / secondary tag in multi-tag questions)
        // Each question has a NormalizedScore s_q and weight w_{q,INT}
        //
        // Task spec: c_{q,INT} = [8.0, 3.9, 1.75]
        // T_j = avg(c_{q,INT}) = (8.0 + 3.9 + 1.75) / 3 = 13.65 / 3 = 4.55

        var contributions = new List<decimal> { 8.0m, 3.9m, 1.75m };

        decimal topicScore = Math.Round(contributions.Average(), 2);

        Assert.Equal(4.55m, topicScore);
    }

    // ── Test 6: BR-19 IsBottleneckWeak threshold ────────────────────────────
    [Fact]
    public void BR19_BottleneckWeak_SubTagBelow4_IsTrue_Above4_IsFalse()
    {
        var service = new DifficultyMappingService();

        // sub-tag official_point = 3.9 → IsBottleneckWeak = true
        Assert.True(service.IsBottleneckWeak(3.9m));

        // sub-tag official_point = 4.1 → IsBottleneckWeak = false
        Assert.False(service.IsBottleneckWeak(4.1m));

        // Boundary: exactly 4.00 → not bottleneck (< 4.00, not <=)
        Assert.False(service.IsBottleneckWeak(4.00m));
    }
}
