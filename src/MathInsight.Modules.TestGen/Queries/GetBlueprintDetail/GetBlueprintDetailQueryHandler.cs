using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Queries.GetBlueprintDetail;

public sealed class GetBlueprintDetailQueryHandler
    : IRequestHandler<GetBlueprintDetailQuery, Result<BlueprintDetailResponse>>
{
    private readonly TestGenDbContext _context;

    public GetBlueprintDetailQueryHandler(TestGenDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BlueprintDetailResponse>> Handle(
        GetBlueprintDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BlueprintId))
            return Result<BlueprintDetailResponse>.Failure(BlueprintErrors.RequestInvalid);

        var blueprint = await _context.Blueprints
            .AsNoTracking()
            .Where(item =>
                item.BlueprintId == request.BlueprintId &&
                item.Status != BlueprintStatuses.Deactivated)
            .Select(item => new BlueprintDetailResponse(
                item.BlueprintId,
                item.BlueprintName,
                item.Grade,
                item.TotalQuestions,
                item.DurationMinutes,
                item.ExpertId,
                item.Status,
                item.ApprovedBy,
                item.ReviewNote,
                item.ReviewTime,
                item.Sections
                    .OrderBy(section => section.SectionOrder)
                    .ThenBy(section => section.BlueprintSectionId)
                    .Select(section => new BlueprintSectionResponse(
                        section.BlueprintSectionId,
                        section.SectionOrder,
                        section.SectionCode,
                        section.SectionName,
                        section.QuestionType,
                        section.InstructionText,
                        section.TotalQuestions,
                        section.DefaultPointPerQuestion,
                        section.DefaultPointPerPart,
                        section.PartCountPerQuestion,
                        section.Details
                            .OrderBy(detail => detail.TagId)
                            .ThenBy(detail => detail.DifficultyId)
                            .Select(detail => new BlueprintDetailSlotResponse(
                                detail.BlueprintDetailId,
                                detail.TagId,
                                _context.TagTopics
                                    .Where(topic => topic.TagId == detail.TagId)
                                    .Select(topic => topic.TagName)
                                    .FirstOrDefault(),
                                detail.DifficultyId,
                                _context.TagDifficulties
                                    .Where(difficulty => difficulty.DifficultyId == detail.DifficultyId)
                                    .Select(difficulty => difficulty.DifficultyName)
                                    .FirstOrDefault(),
                                _context.TagDifficulties
                                    .Where(difficulty => difficulty.DifficultyId == detail.DifficultyId)
                                    .Select(difficulty => (int?)difficulty.LevelValue)
                                    .FirstOrDefault(),
                                detail.Quantity))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return blueprint is null
            ? Result<BlueprintDetailResponse>.Failure(BlueprintErrors.NotFound)
            : Result<BlueprintDetailResponse>.Success(blueprint);
    }
}
