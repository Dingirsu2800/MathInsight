using MathInsight.Modules.TestGen.Generation;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeQuestionSelectorTests
{
    [Fact]
    public void Select_UsesExactThreeFourTwoOneQuota_WhenAvailable()
    {
        var selection = CreateSelector().Select(CandidatesByLevel(3, 4, 2, 1), CancellationToken.None);
        Assert.True(selection.IsComplete);
        Assert.Equal([3, 4, 2, 1], selection.Selected.GroupBy(item => item.DifficultyLevel).OrderBy(item => item.Key).Select(item => item.Count()));
    }

    [Fact]
    public void Select_FallsBackToNearestLevel_AndPrefersLowerOnTie()
    {
        var candidates = CandidatesByLevel(4, 0, 6, 0);
        var selection = CreateSelector().Select(candidates, CancellationToken.None);
        Assert.True(selection.IsComplete);
        Assert.Contains(selection.Selected, item => item.Question.QuestionId == "q-1-3-3");
    }

    [Fact]
    public void Select_PrefersExactDifficultyComposite_BeforeFartherNonComposite()
    {
        var candidates = CandidatesByLevel(10, 0, 0, 0);
        candidates.Add(Candidate("exact-composite", 4, composite: true));

        var selection = CreateSelector().Select(candidates, CancellationToken.None);

        Assert.Contains(selection.Selected, item => item.Question.QuestionId == "exact-composite");
    }

    [Fact]
    public void Select_NeverReturnsMoreThanTwoCompositeQuestions()
    {
        var candidates = CandidatesByLevel(3, 4, 1, 0);
        candidates.Add(Candidate("composite-1", 3, composite: true));
        candidates.Add(Candidate("composite-2", 4, composite: true));
        var selection = CreateSelector().Select(candidates, CancellationToken.None);
        Assert.True(selection.IsComplete);
        Assert.True(selection.Selected.Count(item => item.Question.QuestionType == "Composite") <= 2);
    }

    [Fact]
    public void Select_PrefersUnseenThenOldestSeen()
    {
        var candidates = CandidatesByLevel(3, 4, 2, 1);
        candidates[0] = Candidate("seen-new", 1, DateTime.UtcNow);
        candidates.Add(Candidate("unseen", 1));
        var selection = CreateSelector().Select(candidates, CancellationToken.None);
        Assert.Contains(selection.Selected, item => item.Question.QuestionId == "unseen");
        Assert.DoesNotContain(selection.Selected, item => item.Question.QuestionId == "seen-new");
    }

    [Fact]
    public void Select_ReturnsTenUniqueQuestions()
    {
        var selection = CreateSelector().Select(CandidatesByLevel(3, 4, 2, 1), CancellationToken.None);
        Assert.Equal(10, selection.Selected.Select(item => item.Question.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Select_ReturnsIncomplete_WhenSelectableCapacityBelowTen()
    {
        var selection = CreateSelector().Select(CandidatesByLevel(3, 4, 2, 0), CancellationToken.None);
        Assert.False(selection.IsComplete);
        Assert.Empty(selection.Selected);
    }

    private static TopicPracticeQuestionSelector CreateSelector() => new(new NoOpRandomizer());
    private static List<TopicPracticeCandidate> CandidatesByLevel(int l1, int l2, int l3, int l4, bool composite = false)
    {
        var result = new List<TopicPracticeCandidate>();
        foreach (var (level, count) in new[] { (1, l1), (2, l2), (3, l3), (4, l4) })
            for (var index = 0; index < count; index++) result.Add(Candidate($"q-{level}-{index}-{result.Count}", level, null, composite));
        return result;
    }
    private static TopicPracticeCandidate Candidate(string id, int level, DateTime? lastSeenAt = null, bool composite = false) => new(new BlueprintExamCandidate(id, $"{id}-v", 1m, $"d-{level}", composite ? "Composite" : "SingleChoice", new HashSet<string>(["topic"], StringComparer.OrdinalIgnoreCase), new HashSet<string>(["AllOrNothing"], StringComparer.OrdinalIgnoreCase)), level, lastSeenAt);
    private sealed class NoOpRandomizer : IGenerationRandomizer { public void Shuffle<T>(IList<T> values) { } }
}
