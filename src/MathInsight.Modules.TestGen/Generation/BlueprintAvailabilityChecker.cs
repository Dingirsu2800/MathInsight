using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Generation;

public sealed record BlueprintAvailabilityCheck(BlueprintAvailabilityResponse Response);

public interface IBlueprintAvailabilityChecker
{
    Task<Result<BlueprintAvailabilityResponse>> PreviewAsync(
        BlueprintAvailabilityRequest request,
        CancellationToken cancellationToken);

    Task<BlueprintAvailabilityCheck> CheckAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken);
}

public sealed class BlueprintAvailabilityChecker : IBlueprintAvailabilityChecker
{
    private const int MaxSections = 50;
    private const int MaxRowsPerSection = 100;
    private const int MaxTotalRows = 500;
    private const int MaxQuantityPerRow = 100;
    private const int MaxTotalQuestions = 1000;

    private readonly IBlueprintExamCandidateProvider _candidateProvider;
    private readonly IBlueprintExamQuestionSelector _selector;
    private readonly IBlueprintAggregateValidator _validator;

    public BlueprintAvailabilityChecker(
        IBlueprintExamCandidateProvider candidateProvider,
        IBlueprintExamQuestionSelector selector,
        IBlueprintAggregateValidator validator)
    {
        _candidateProvider = candidateProvider;
        _selector = selector;
        _validator = validator;
    }

    public async Task<Result<BlueprintAvailabilityResponse>> PreviewAsync(
        BlueprintAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateShape(request, out var normalizedSections))
            return Result<BlueprintAvailabilityResponse>.Failure(BlueprintErrors.AvailabilityRequestInvalid);

        var blueprint = ToBlueprint(request, normalizedSections!);
        var taxonomyResult = await _validator.ValidateAsync(ToBlueprintRequest(blueprint), cancellationToken);
        if (taxonomyResult.IsFailure)
        {
            var error = taxonomyResult.Error == BlueprintErrors.TaxonomyInvalid
                ? taxonomyResult.Error
                : BlueprintErrors.AvailabilityRequestInvalid;
            return Result<BlueprintAvailabilityResponse>.Failure(error!);
        }

        var check = await CheckAsync(blueprint, cancellationToken);
        return Result<BlueprintAvailabilityResponse>.Success(check.Response);
    }

    public async Task<BlueprintAvailabilityCheck> CheckAsync(
        Blueprint blueprint,
        CancellationToken cancellationToken)
    {
        var requirements = BlueprintExamGenerationPlanner.BuildRequirements(blueprint);
        var pool = await _candidateProvider.GetCandidatesAsync(blueprint, cancellationToken);
        var candidates = pool.Candidates
            .GroupBy(candidate => candidate.QuestionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var selection = _selector.Select(requirements, candidates, cancellationToken);
        var rows = requirements.ToDictionary(
            requirement => requirement.BlueprintDetailId,
            requirement => new BlueprintAvailabilityRowResponse(
                requirement.BlueprintDetailId,
                candidates.Count(candidate => BlueprintExamCandidateMatcher.Matches(candidate, requirement)),
                requirement.Quantity,
                0),
            StringComparer.OrdinalIgnoreCase);

        var allRowsHaveCapacity = rows.Values.All(row => row.AvailableCount >= row.RequiredCount);
        var availabilityCode = selection.IsComplete
            ? null
            : allRowsHaveCapacity
                ? BlueprintAvailabilityCodes.OverlapConflict
                : BlueprintAvailabilityCodes.InsufficientQuestions;

        var responseRows = rows.ToDictionary(
            item => item.Key,
            item => item.Value with
            {
                Shortage = availabilityCode == BlueprintAvailabilityCodes.OverlapConflict
                    ? 0
                    : Math.Max(item.Value.RequiredCount - item.Value.AvailableCount, 0)
            },
            StringComparer.OrdinalIgnoreCase);
        var response = new BlueprintAvailabilityResponse(
            DateTimeOffset.UtcNow,
            blueprint.Sections
                .OrderBy(section => section.SectionOrder)
                .Select(section => new BlueprintAvailabilitySectionResponse(
                    section.BlueprintSectionId,
                    section.Details
                        .OrderBy(detail => detail.BlueprintDetailId, StringComparer.OrdinalIgnoreCase)
                        .Select(detail => responseRows[detail.BlueprintDetailId])
                        .ToList()))
                .ToList(),
            selection.IsComplete,
            availabilityCode);

        return new BlueprintAvailabilityCheck(response);
    }

    private static bool TryValidateShape(
        BlueprintAvailabilityRequest? request,
        out IReadOnlyList<NormalizedAvailabilitySection>? normalizedSections)
    {
        normalizedSections = null;
        if (request is null || request.Grade is not (10 or 11 or 12) ||
            request.Sections is null || request.Sections.Count is < 1 or > MaxSections)
            return false;

        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NormalizedAvailabilitySection>(request.Sections.Count);
        var totalRows = 0;
        var totalQuestions = 0;

        foreach (var section in request.Sections)
        {
            var sectionId = NormalizeId(section.ClientSectionId);
            var sectionType = BlueprintQuestionTypes.Normalize(section.QuestionType);
            var sectionRule = BlueprintPolicies.NormalizeScoringRule(section.ScoringRule);
            if (sectionId is null || !sectionIds.Add(sectionId) ||
                sectionType is null or "" ||
                (!BlueprintQuestionTypes.IsActualQuestionType(sectionType) &&
                 sectionType != BlueprintQuestionTypes.Mixed) ||
                section.Rows is null || section.Rows.Count is < 1 or > MaxRowsPerSection ||
                (sectionType == BlueprintQuestionTypes.Mixed
                    ? sectionRule is not null
                    : !BlueprintPolicies.IsValidPair(sectionType, sectionRule)))
                return false;

            var rows = new List<NormalizedAvailabilityRow>(section.Rows.Count);
            foreach (var row in section.Rows)
            {
                var rowId = NormalizeId(row.ClientRowId);
                var tagId = NormalizeId(row.TagId);
                var difficultyId = NormalizeId(row.DifficultyId);
                var rowType = BlueprintQuestionTypes.Normalize(row.QuestionType);
                var rowRule = BlueprintPolicies.NormalizeScoringRule(row.ScoringRule);
                if (rowId is null || !rowIds.Add(rowId) || tagId is null || difficultyId is null ||
                    row.Quantity is < 1 or > MaxQuantityPerRow ||
                    !BlueprintPolicies.TryResolveEffectivePolicy(
                        sectionType,
                        sectionRule,
                        rowType,
                        rowRule,
                        out var effectiveType,
                        out var effectiveRule))
                    return false;

                rows.Add(new NormalizedAvailabilityRow(rowId, tagId, difficultyId, row.Quantity, effectiveType, effectiveRule));
                totalQuestions = checked(totalQuestions + row.Quantity);
            }

            totalRows = checked(totalRows + rows.Count);
            result.Add(new NormalizedAvailabilitySection(sectionId, sectionType, sectionRule, rows));
        }

        if (totalRows > MaxTotalRows || totalQuestions > MaxTotalQuestions)
            return false;

        normalizedSections = result;
        return true;
    }

    private static Blueprint ToBlueprint(
        BlueprintAvailabilityRequest request,
        IReadOnlyList<NormalizedAvailabilitySection> sections)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = "availability-preview",
            BlueprintName = "availability-preview",
            Grade = request.Grade,
            TotalQuestions = sections.SelectMany(section => section.Rows).Sum(row => row.Quantity),
            TotalScore = sections.Count,
            DurationMinutes = 1,
            ExpertId = "availability-preview",
            Status = BlueprintStatuses.Draft
        };

        foreach (var (section, index) in sections.Select((value, index) => (value, index)))
        {
            var entitySection = new BlueprintSection
            {
                BlueprintSectionId = section.ClientSectionId,
                BlueprintId = blueprint.BlueprintId,
                SectionOrder = index + 1,
                SectionName = "Availability preview",
                QuestionType = section.QuestionType,
                ScoringRule = section.ScoringRule,
                TotalQuestions = section.Rows.Sum(row => row.Quantity),
                ScoreBudget = 1m
            };
            foreach (var row in section.Rows)
            {
                entitySection.Details.Add(new BlueprintDetail
                {
                    BlueprintDetailId = row.ClientRowId,
                    BlueprintId = blueprint.BlueprintId,
                    BlueprintSectionId = entitySection.BlueprintSectionId,
                    TagId = row.TagId,
                    DifficultyId = row.DifficultyId,
                    Quantity = row.Quantity,
                    QuestionType = section.QuestionType == BlueprintQuestionTypes.Mixed ? row.QuestionType : null,
                    ScoringRule = section.QuestionType == BlueprintQuestionTypes.Mixed ? row.ScoringRule : null
                });
            }
            blueprint.Sections.Add(entitySection);
        }

        return blueprint;
    }

    private static BlueprintRequest ToBlueprintRequest(Blueprint blueprint)
        => new()
        {
            BlueprintName = blueprint.BlueprintName,
            Grade = blueprint.Grade,
            TotalQuestions = blueprint.TotalQuestions,
            TotalScore = blueprint.TotalScore,
            DurationMinutes = blueprint.DurationMinutes,
            Sections = blueprint.Sections.Select(section => new BlueprintSectionRequest
            {
                SectionOrder = section.SectionOrder,
                SectionName = section.SectionName,
                QuestionType = section.QuestionType,
                TotalQuestions = section.TotalQuestions,
                ScoreBudget = section.ScoreBudget,
                ScoringRule = section.ScoringRule,
                Details = section.Details.Select(detail => new BlueprintDetailRequest
                {
                    TagId = detail.TagId,
                    DifficultyId = detail.DifficultyId,
                    Quantity = detail.Quantity,
                    QuestionType = detail.QuestionType,
                    ScoringRule = detail.ScoringRule
                }).ToList()
            }).ToList()
        };

    private static string? NormalizeId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NormalizedAvailabilitySection(
        string ClientSectionId,
        string QuestionType,
        string? ScoringRule,
        IReadOnlyList<NormalizedAvailabilityRow> Rows);

    private sealed record NormalizedAvailabilityRow(
        string ClientRowId,
        string TagId,
        string DifficultyId,
        int Quantity,
        string QuestionType,
        string ScoringRule);
}
