namespace MathInsight.Modules.TestGen.Generation;

public interface IAdaptiveBlueprintExamQuestionSelector
{
    BlueprintExamSelection Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyDictionary<string, AdaptiveBlueprintDetailPlan> plansByDetailId,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        CancellationToken cancellationToken);
}
