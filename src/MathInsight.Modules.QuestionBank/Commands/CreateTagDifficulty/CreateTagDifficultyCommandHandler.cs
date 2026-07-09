using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.CreateTagDifficulty;

public sealed class CreateTagDifficultyCommandHandler
    : IRequestHandler<CreateTagDifficultyCommand, Result<TagDifficultyResponse>>
{
    private readonly QuestionBankDbContext _context;

    public CreateTagDifficultyCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TagDifficultyResponse>> Handle(
        CreateTagDifficultyCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return Result<TagDifficultyResponse>.Failure(validationError);

        var normalizedName = request.DifficultyName.Trim();

        var nameExists = await _context.TagDifficulties
            .AnyAsync(difficulty => difficulty.DifficultyName == normalizedName, cancellationToken);

        if (nameExists)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagNameDuplicate);

        var levelExists = await _context.TagDifficulties
            .AnyAsync(difficulty => difficulty.LevelValue == request.LevelValue, cancellationToken);

        if (levelExists)
            return Result<TagDifficultyResponse>.Failure(QuestionBankErrors.TagLevelValueDuplicate);

        var difficulty = new TagDifficulty
        {
            DifficultyId = Guid.NewGuid().ToString(),
            DifficultyName = normalizedName,
            Description = NormalizeOptionalText(request.Description),
            LevelValue = request.LevelValue,
            DisplayOrder = request.DisplayOrder,
            IsActive = true
        };

        _context.TagDifficulties.Add(difficulty);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TagDifficultyResponse>.Success(ToResponse(difficulty));
    }

    private static Error? ValidateRequest(CreateTagDifficultyRequest request)
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
