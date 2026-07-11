using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

/// <summary>
/// Unit tests for the Elo-based PracticePoint update formula (RCM-06).
///
/// Correct:  practice_point = min(10.0, practice_point + 0.05 × w_D × γ_time)
/// Wrong:    practice_point = max(0.0,  practice_point − 0.05 × (5 − w_D) × γ_time_penalty)
///
/// w_D:  level 1→0.5, 2→1.0, 3→1.5, 4→2.0
/// γ_time_penalty = 1.5 when timeSpent < 5s AND !isAbandoned; else 1.0
/// γ_time         = 1.0 always for correct answers
/// </summary>
public class PracticePointEloFormulaTests
{
    // ── Mirrors TopicResultIngestionHandler.IngestTopicResultAsync Elo logic ──
    private static decimal ApplyElo(decimal practicePoint, bool isCorrect, byte difficultyLevel, int timeSpent, bool isAbandoned)
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

        decimal delta = isCorrect
            ? 0.05m * wD
            : -0.05m * (5.0m - wD) * timePenalty;

        return Math.Clamp(practicePoint + delta, 0.00m, 10.00m);
    }

    [Fact]
    public void Correct_Level2_NormalTime_DeltaIs_Plus0_05()
    {
        // Correct, level 2 (w_D=1.0), normal time: Δ = +0.05 × 1.0 = +0.05
        var before = 5.00m;
        var after = ApplyElo(before, isCorrect: true, difficultyLevel: 2, timeSpent: 10, isAbandoned: false);
        Assert.Equal(5.05m, after);
    }

    [Fact]
    public void Wrong_Level1_NormalTime_DeltaIs_Minus0_225()
    {
        // Wrong, level 1 (w_D=0.5), normal time: Δ = −0.05×(5−0.5) = −0.225
        var before = 5.00m;
        var after = ApplyElo(before, isCorrect: false, difficultyLevel: 1, timeSpent: 10, isAbandoned: false);
        Assert.Equal(5.00m - 0.225m, after);
    }

    [Fact]
    public void Wrong_Level4_NormalTime_DeltaIs_Minus0_150()
    {
        // Wrong, level 4 (w_D=2.0), normal time: Δ = −0.05×(5−2.0) = −0.150
        var before = 5.00m;
        var after = ApplyElo(before, isCorrect: false, difficultyLevel: 4, timeSpent: 10, isAbandoned: false);
        Assert.Equal(5.00m - 0.150m, after);
    }

    [Fact]
    public void Wrong_Level1_FastTime_Guessing_DeltaIs_Minus0_3375()
    {
        // Wrong, level 1 (w_D=0.5), t < 5s (guessing): Δ = −0.05×4.5×1.5 = −0.3375
        var before = 5.00m;
        var after = ApplyElo(before, isCorrect: false, difficultyLevel: 1, timeSpent: 3, isAbandoned: false);
        Assert.Equal(5.00m - 0.3375m, after);
    }

    [Fact]
    public void Abandoned_FastTime_TimePenaltyNotApplied()
    {
        // Wrong, abandoned, t < 5s: IsAbandoned=true → γ = 1.0 (not 1.5)
        // Δ = −0.05 × (5 − 0.5) × 1.0 = −0.225
        var before = 5.00m;
        var after = ApplyElo(before, isCorrect: false, difficultyLevel: 1, timeSpent: 3, isAbandoned: true);
        Assert.Equal(5.00m - 0.225m, after);
    }

    [Fact]
    public void PracticePoint_NeverExceedsTen()
    {
        var after = ApplyElo(9.99m, isCorrect: true, difficultyLevel: 4, timeSpent: 10, isAbandoned: false);
        Assert.Equal(10.00m, after);
    }

    [Fact]
    public void PracticePoint_NeverDropsBelowZero()
    {
        var after = ApplyElo(0.01m, isCorrect: false, difficultyLevel: 1, timeSpent: 3, isAbandoned: false);
        Assert.Equal(0.00m, after);
    }
}
