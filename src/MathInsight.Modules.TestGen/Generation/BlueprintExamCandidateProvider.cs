using MathInsight.Modules.TestGen.Persistence;

namespace MathInsight.Modules.TestGen.Generation;

public sealed class BlueprintExamCandidateProvider : IBlueprintExamCandidateProvider
{
    private readonly IQuestionCandidateCatalog _catalog;

    public BlueprintExamCandidateProvider(TestGenDbContext context)
        : this(new QuestionCandidateCatalog(context))
    {
    }

    public BlueprintExamCandidateProvider(IQuestionCandidateCatalog catalog)
    {
        _catalog = catalog;
    }

    public Task<BlueprintExamCandidatePool> GetCandidatesAsync(
        MathInsight.Modules.TestGen.Persistence.Entities.Blueprint blueprint,
        CancellationToken cancellationToken)
    {
        var details = blueprint.Sections
            .SelectMany(section => section.Details)
            .ToList();
        return GetCandidatesAsync(
            blueprint,
            details.Select(detail => detail.DifficultyId).ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    public Task<BlueprintExamCandidatePool> GetCandidatesAsync(
        MathInsight.Modules.TestGen.Persistence.Entities.Blueprint blueprint,
        IReadOnlyCollection<string> difficultyIds,
        CancellationToken cancellationToken)
    {
        var sections = blueprint.Sections.ToList();
        var details = sections.SelectMany(section => section.Details).ToList();
        var filter = new QuestionCandidateCatalogFilter(
            blueprint.Grade,
            details.Select(detail => detail.TagId).Distinct().ToList(),
            difficultyIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            sections.Select(section => section.QuestionType).Distinct().ToList());

        return _catalog.GetCandidatesAsync(filter, cancellationToken);
    }
}
