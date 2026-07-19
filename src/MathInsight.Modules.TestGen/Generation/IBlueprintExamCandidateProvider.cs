using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Generation;

public interface IBlueprintExamCandidateProvider
{
    Task<IReadOnlyList<BlueprintExamCandidate>> GetCandidatesAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken);
}
