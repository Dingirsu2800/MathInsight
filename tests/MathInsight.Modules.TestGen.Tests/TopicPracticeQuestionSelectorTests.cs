using MathInsight.Modules.TestGen.Generation;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class TopicPracticeQuestionSelectorTests
{
    [Fact]
    public void Select_BaselineUsesExactThreeFourTwoOneQuota_WhenAvailable()
    {
        var selection = CreateSelector().Select(CandidatesByLevel(3, 4, 2, 1), TopicPracticeSelectionPlanFactory.CreateBaseline(), CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.Equal([3, 4, 2, 1], selection.Selected.GroupBy(item => item.Candidate.DifficultyLevel).OrderBy(item => item.Key).Select(item => item.Count()));
    }

    [Theory]
    [InlineData(1, 8, 2, 0, 0)]
    [InlineData(2, 2, 7, 1, 0)]
    public void CreateAdaptive_BuildsApprovedDifficultyProfile(byte recommendedLevel, int level1, int level2, int level3, int level4)
    {
        var plan = TopicPracticeSelectionPlanFactory.CreateAdaptive(recommendedLevel, FocusTags(), isDirectFocusSelection: false);

        Assert.Equal(10, plan.Slots.Count);
        Assert.Equal(6, plan.Slots.Count(slot => slot.Scope == TopicPracticeSlotScope.FocusPreferred));
        var countsByLevel = plan.Slots
            .GroupBy(slot => slot.TargetDifficultyLevel)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal([level1, level2, level3, level4], Enumerable.Range(1, 4).Select(level => countsByLevel.GetValueOrDefault(level)));
    }

    [Theory]
    [InlineData(0, 9, 1, 0, 0)]
    [InlineData(1.99, 9, 1, 0, 0)]
    [InlineData(2, 8, 2, 0, 0)]
    [InlineData(2.99, 8, 2, 0, 0)]
    [InlineData(3, 3, 6, 1, 0)]
    [InlineData(3.99, 3, 6, 1, 0)]
    [InlineData(4, 2, 6, 2, 0)]
    [InlineData(4.99, 2, 6, 2, 0)]
    [InlineData(5, 0, 3, 6, 1)]
    [InlineData(5.99, 0, 3, 6, 1)]
    [InlineData(6, 0, 2, 6, 2)]
    [InlineData(7.49, 0, 2, 6, 2)]
    [InlineData(7.5, 0, 0, 2, 8)]
    [InlineData(8.99, 0, 0, 2, 8)]
    [InlineData(9, 0, 0, 1, 9)]
    [InlineData(10, 0, 0, 1, 9)]
    public void CreateMastery_BuildsProfileForEveryOfficialPointBoundary(
        decimal officialPoint,
        int level1,
        int level2,
        int level3,
        int level4)
    {
        var plan = TopicPracticeSelectionPlanFactory.CreateMastery(officialPoint, FocusTags());

        Assert.Equal(10, plan.Slots.Count);
        var countsByLevel = plan.Slots
            .GroupBy(slot => slot.TargetDifficultyLevel)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal([level1, level2, level3, level4], Enumerable.Range(1, 4).Select(level => countsByLevel.GetValueOrDefault(level)));
        Assert.Equal(TopicPracticePolicy.MasteryRuleVersion, plan.RuleVersion);
    }

    [Fact]
    public void CreateMastery_UsesColdStartWithoutNormalEvidence()
    {
        var plan = TopicPracticeSelectionPlanFactory.CreateMastery(
            1.00m,
            FocusTags(),
            hasNormalEvidence: false,
            hasStrongEvidence: false);

        var counts = plan.Slots
            .GroupBy(slot => slot.TargetDifficultyLevel)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal([3, 4, 2, 1], Enumerable.Range(1, 4).Select(level => counts.GetValueOrDefault(level)));
    }

    [Theory]
    [InlineData(1.00, false, 8, 2, 0, 0)]
    [InlineData(1.00, true, 9, 1, 0, 0)]
    [InlineData(9.00, false, 0, 0, 2, 8)]
    [InlineData(9.00, true, 0, 0, 1, 9)]
    public void CreateMastery_ReservesExtremeProfilesForStrongEvidence(
        decimal officialPoint,
        bool hasStrongEvidence,
        int level1,
        int level2,
        int level3,
        int level4)
    {
        var plan = TopicPracticeSelectionPlanFactory.CreateMastery(
            officialPoint,
            FocusTags(),
            hasNormalEvidence: true,
            hasStrongEvidence);
        var counts = plan.Slots
            .GroupBy(slot => slot.TargetDifficultyLevel)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal([level1, level2, level3, level4], Enumerable.Range(1, 4).Select(level => counts.GetValueOrDefault(level)));
    }

    [Fact]
    public void Select_AdaptiveParent_UsesSixFocusAndAtLeastTwoOutside_WhenPoolPermits()
    {
        var candidates = CandidatesByLevel(8, 2, 0, 0, "focus");
        candidates.AddRange(CandidatesByLevel(3, 4, 2, 1, "outside", offset: candidates.Count));
        var plan = TopicPracticeSelectionPlanFactory.CreateAdaptive(1, FocusTags(), isDirectFocusSelection: false);

        var selection = CreateSelector().Select(candidates, plan, CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.InRange(selection.Selected.Count(item => item.IsWeakTagFocus), 6, 8);
        Assert.True(selection.Selected.Count(item => !item.IsWeakTagFocus) >= 2);
    }

    [Fact]
    public void Select_AdaptiveParent_UsesAtMostEightFocus_WhenOnlyTwoOutsideCandidatesExist()
    {
        var candidates = CandidatesByLevel(8, 2, 0, 0, "focus");
        candidates.AddRange(CandidatesByLevel(2, 0, 0, 0, "outside", offset: candidates.Count));
        var plan = TopicPracticeSelectionPlanFactory.CreateAdaptive(1, FocusTags(), isDirectFocusSelection: false);

        var selection = CreateSelector().Select(candidates, plan, CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.Equal(8, selection.Selected.Count(item => item.IsWeakTagFocus));
    }

    [Fact]
    public void Select_DirectWeakTagSelection_AllowsAllTenFocusQuestions()
    {
        var plan = TopicPracticeSelectionPlanFactory.CreateAdaptive(1, FocusTags(), isDirectFocusSelection: true);

        var selection = CreateSelector().Select(CandidatesByLevel(8, 2, 0, 0, "focus"), plan, CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.Equal(10, selection.Selected.Count(item => item.IsWeakTagFocus));
    }

    [Fact]
    public void Select_FallsBackToNearestLevel_AndPrefersLowerOnTie()
    {
        var candidates = CandidatesByLevel(4, 0, 6, 0);
        var selection = CreateSelector().Select(candidates, TopicPracticeSelectionPlanFactory.CreateBaseline(), CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.Contains(selection.Selected, item => item.Candidate.Question.QuestionId == "q-1-3-3");
    }

    [Fact]
    public void Select_FallbackPrefersEveryLowerLevelBeforeAnyHigherLevel()
    {
        var plan = new TopicPracticeSelectionPlan(
            Enumerable.Repeat(new TopicPracticeSlot(3, TopicPracticeSlotScope.BreadthPreferred), 10).ToList(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsDirectFocusSelection: false,
            TopicPracticePolicy.RuleVersion);
        var candidates = CandidatesByLevel(10, 0, 0, 10);

        var selection = CreateSelector().Select(candidates, plan, CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.All(selection.Selected, item => Assert.Equal(1, item.Candidate.DifficultyLevel));
    }

    [Fact]
    public void Select_NeverReturnsMoreThanTwoCompositeQuestions()
    {
        var candidates = CandidatesByLevel(3, 4, 1, 0);
        candidates.Add(Candidate("composite-1", 3, "topic", composite: true));
        candidates.Add(Candidate("composite-2", 4, "topic", composite: true));

        var selection = CreateSelector().Select(candidates, TopicPracticeSelectionPlanFactory.CreateBaseline(), CancellationToken.None);

        Assert.True(selection.IsComplete);
        Assert.True(selection.Selected.Count(item => item.Candidate.Question.QuestionType == "Composite") <= 2);
    }

    [Fact]
    public void Select_PrefersUnseenThenOldestSeen()
    {
        var candidates = CandidatesByLevel(3, 4, 2, 1);
        candidates[0] = Candidate("seen-new", 1, "topic", DateTime.UtcNow);
        candidates.Add(Candidate("unseen", 1, "topic"));

        var selection = CreateSelector().Select(candidates, TopicPracticeSelectionPlanFactory.CreateBaseline(), CancellationToken.None);

        Assert.Contains(selection.Selected, item => item.Candidate.Question.QuestionId == "unseen");
        Assert.DoesNotContain(selection.Selected, item => item.Candidate.Question.QuestionId == "seen-new");
    }

    [Fact]
    public void Select_ReturnsIncomplete_WhenSelectableCapacityBelowTen()
    {
        var selection = CreateSelector().Select(CandidatesByLevel(3, 4, 2, 0), TopicPracticeSelectionPlanFactory.CreateBaseline(), CancellationToken.None);

        Assert.False(selection.IsComplete);
        Assert.Empty(selection.Selected);
    }

    private static TopicPracticeQuestionSelector CreateSelector() => new(new NoOpRandomizer());

    private static IReadOnlySet<string> FocusTags() => new HashSet<string>(["focus"], StringComparer.OrdinalIgnoreCase);

    private static List<TopicPracticeCandidate> CandidatesByLevel(int l1, int l2, int l3, int l4, string tagId = "topic", int offset = 0)
    {
        var result = new List<TopicPracticeCandidate>();
        foreach (var (level, count) in new[] { (1, l1), (2, l2), (3, l3), (4, l4) })
        {
            for (var index = 0; index < count; index++)
            {
                var ordinal = offset + result.Count;
                result.Add(Candidate($"q-{level}-{index}-{ordinal}", level, tagId));
            }
        }

        return result;
    }

    private static TopicPracticeCandidate Candidate(string id, int level, string tagId, DateTime? lastSeenAt = null, bool composite = false) => new(
        new BlueprintExamCandidate(
            id,
            $"{id}-v",
            1m,
            $"d-{level}",
            composite ? "Composite" : "SingleChoice",
            new HashSet<string>([tagId], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["AllOrNothing"], StringComparer.OrdinalIgnoreCase)),
        level,
        lastSeenAt);

    private sealed class NoOpRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }
}
