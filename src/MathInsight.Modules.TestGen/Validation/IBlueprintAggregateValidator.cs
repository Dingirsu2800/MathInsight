using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.TestGen.Validation;

public interface IBlueprintAggregateValidator
{
    Task<Result<ValidatedBlueprintAggregate>> ValidateAsync(
        BlueprintRequest request,
        CancellationToken cancellationToken);
}

public sealed record ValidatedBlueprintAggregate(
    string BlueprintName,
    int Grade,
    int TotalQuestions,
    int DurationMinutes,
    IReadOnlyList<ValidatedBlueprintSection> Sections);

public sealed record ValidatedBlueprintSection(
    int SectionOrder,
    string? SectionCode,
    string SectionName,
    string QuestionType,
    string? InstructionText,
    int TotalQuestions,
    decimal DefaultPointPerQuestion,
    decimal? DefaultPointPerPart,
    int? PartCountPerQuestion,
    IReadOnlyList<ValidatedBlueprintDetail> Details);

public sealed record ValidatedBlueprintDetail(
    string TagId,
    string DifficultyId,
    int Quantity);
