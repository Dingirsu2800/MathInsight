using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateTagDifficulty;

public sealed class UpdateTagDifficultyCommandHandler
    : IRequestHandler<UpdateTagDifficultyCommand, Result<TagDifficultyResponse>>
{
    private readonly QuestionBankDbContext _context;

    public UpdateTagDifficultyCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TagDifficultyResponse>> Handle(
        UpdateTagDifficultyCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DifficultyId))
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagIdRequired);

        var request = command.Request;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return Result<TagDifficultyResponse>.Failure(validationError);

        var difficulty = await _context.TagDifficulties
            .FirstOrDefaultAsync(
                existing => existing.DifficultyId == command.DifficultyId,
                cancellationToken);

        if (difficulty is null)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagDifficultyNotFound);

        var normalizedName = request.DifficultyName.Trim();

        var nameExists = await _context.TagDifficulties
            .AnyAsync(
                existing => existing.DifficultyId != difficulty.DifficultyId &&
                            existing.DifficultyName == normalizedName,
                cancellationToken);

        if (nameExists)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagNameDuplicate);

        if (difficulty.LevelValue != request.LevelValue)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagLevelValueImmutable);

        var levelExists = await _context.TagDifficulties
            .AnyAsync(
                existing => existing.DifficultyId != difficulty.DifficultyId &&
                            existing.LevelValue == request.LevelValue,
                cancellationToken);

        if (levelExists)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagLevelValueDuplicate);

        difficulty.DifficultyName = normalizedName;
        difficulty.Description = NormalizeOptionalText(request.Description);
        difficulty.LevelValue = request.LevelValue;
        difficulty.DisplayOrder = request.DisplayOrder;
        difficulty.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<TagDifficultyResponse>.Success(ToResponse(difficulty));
    }

    private static Error? ValidateRequest(UpdateTagDifficultyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DifficultyName))
            return QuestionBankErrors.TagNameRequired;

        if (request.DifficultyName.Trim().Length > 50)
            return QuestionBankErrors.TagNameTooLong;

        if (request.Description?.Length > 255)
            return QuestionBankErrors.TagDescriptionTooLong;

        if (request.LevelValue <= 0)
            return QuestionBankErrors.TagLevelValueInvalid;

        if (request.DisplayOrder <= 0)
            return QuestionBankErrors.TagDisplayOrderInvalid;

        return null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TagDifficultyResponse ToResponse(TagDifficulty difficulty)
    {
        return new TagDifficultyResponse(
            difficulty.DifficultyId,
            difficulty.DifficultyName,
            difficulty.Description,
            difficulty.LevelValue,
            difficulty.DisplayOrder,
            difficulty.IsActive);
    }
}
