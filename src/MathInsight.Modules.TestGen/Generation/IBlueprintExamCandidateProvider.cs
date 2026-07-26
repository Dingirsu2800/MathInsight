using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Generation;

public interface IBlueprintExamCandidateProvider
{
    Task<BlueprintExamCandidatePool> GetCandidatesAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken);
}
