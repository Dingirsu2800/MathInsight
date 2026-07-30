namespace MathInsight.Modules.TestGen.Generation;

public sealed record QuestionCandidateCatalogFilter(
    int Grade,
    IReadOnlyCollection<string> TagIds,
    IReadOnlyCollection<string>? DifficultyIds = null,
    IReadOnlyCollection<string>? QuestionTypes = null);

public interface IQuestionCandidateCatalog
{
    Task<BlueprintExamCandidatePool> GetCandidatesAsync(
        QuestionCandidateCatalogFilter filter,
        CancellationToken cancellationToken);
}
