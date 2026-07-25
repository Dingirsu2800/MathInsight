namespace MathInsight.Modules.TestGen.Generation;

public interface IBlueprintExamQuestionSelector
{
    BlueprintExamSelection Select(
        IReadOnlyList<BlueprintExamRequirement> requirements,
        IReadOnlyList<BlueprintExamCandidate> candidates,
        CancellationToken cancellationToken);
}
