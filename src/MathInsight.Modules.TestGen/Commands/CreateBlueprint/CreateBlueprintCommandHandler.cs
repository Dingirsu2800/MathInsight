using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Persistence;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Commands.CreateBlueprint;

public sealed class CreateBlueprintCommandHandler
    : IRequestHandler<CreateBlueprintCommand, Result<CreateBlueprintResponse>>
{
    private readonly TestGenDbContext _context;
    private readonly IBlueprintAggregateValidator _validator;

    public CreateBlueprintCommandHandler(
        TestGenDbContext context,
        IBlueprintAggregateValidator validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<CreateBlueprintResponse>> Handle(
        CreateBlueprintCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpertId) ||
            !await _context.Experts.AsNoTracking().AnyAsync(
                expert => expert.ExpertId == command.ExpertId,
                cancellationToken))
        {
            return Result<CreateBlueprintResponse>.Failure(ApplicationErrors.AuthInvalidToken);
        }

        var validationResult = await _validator.ValidateAsync(command.Request, cancellationToken);
        if (validationResult.IsFailure)
            return Result<CreateBlueprintResponse>.Failure(validationResult.Error!);

        var validated = validationResult.Value!;
        var blueprintId = Guid.NewGuid().ToString();
        var blueprint = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = validated.BlueprintName,
            Grade = validated.Grade,
            TotalQuestions = validated.TotalQuestions,
            DurationMinutes = validated.DurationMinutes,
            ExpertId = command.ExpertId,
            Status = BlueprintStatuses.Draft
        };

        foreach (var sectionRequest in validated.Sections)
        {
            var sectionId = Guid.NewGuid().ToString();
            var section = new BlueprintSection
            {
                BlueprintSectionId = sectionId,
                BlueprintId = blueprintId,
                SectionOrder = sectionRequest.SectionOrder,
                SectionCode = sectionRequest.SectionCode,
                SectionName = sectionRequest.SectionName,
                QuestionType = sectionRequest.QuestionType,
                InstructionText = sectionRequest.InstructionText,
                TotalQuestions = sectionRequest.TotalQuestions,
                DefaultPointPerQuestion = sectionRequest.DefaultPointPerQuestion,
                DefaultPointPerPart = sectionRequest.DefaultPointPerPart,
                PartCountPerQuestion = sectionRequest.PartCountPerQuestion
            };

            foreach (var detailRequest in sectionRequest.Details)
            {
                section.Details.Add(new BlueprintDetail
                {
                    BlueprintDetailId = Guid.NewGuid().ToString(),
                    BlueprintId = blueprintId,
                    BlueprintSectionId = sectionId,
                    TagId = detailRequest.TagId,
                    DifficultyId = detailRequest.DifficultyId,
                    Quantity = detailRequest.Quantity
                });
            }

            blueprint.Sections.Add(section);
        }

        _context.Blueprints.Add(blueprint);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateBlueprintResponse>.Success(
            new CreateBlueprintResponse(blueprint.BlueprintId, blueprint.Status));
    }
}
