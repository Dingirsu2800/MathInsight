using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;

public sealed class UpdateTagTopicCommandHandler
    : IRequestHandler<UpdateTagTopicCommand, Result<TagTopicTreeResponse>>
{
    private readonly QuestionBankDbContext _context;

    public UpdateTagTopicCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TagTopicTreeResponse>> Handle(
        UpdateTagTopicCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.TagId))
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagIdRequired);

        var request = command.Request;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return Result<TagTopicTreeResponse>.Failure(validationError);

        var topic = await _context.TagTopics
            .FirstOrDefaultAsync(existing => existing.TagId == command.TagId, cancellationToken);

        if (topic is null)
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagTopicNotFound);

        var normalizedName = request.TagName.Trim();
        var normalizedParentId = string.IsNullOrWhiteSpace(request.ParentTagId)
            ? null
            : request.ParentTagId.Trim();

        var nameExists = await _context.TagTopics
            .AnyAsync(
                existing => existing.TagId != topic.TagId && existing.TagName == normalizedName,
                cancellationToken);

        if (nameExists)
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagNameDuplicate);

        if (!string.Equals(topic.ParentTagId, normalizedParentId, StringComparison.OrdinalIgnoreCase) ||
            topic.Grade != request.Grade)
        {
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagStructureImmutable);
        }

        if (normalizedParentId == topic.TagId)
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentInvalid);

        if (normalizedParentId is not null)
        {
            var parent = await _context.TagTopics
                .AsNoTracking()
                .FirstOrDefaultAsync(existing => existing.TagId == normalizedParentId, cancellationToken);

            if (parent is null)
                return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentNotFound);

            if (parent.Grade != request.Grade)
                return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentInvalid);

            var topicLinks = await _context.TagTopics
                .AsNoTracking()
                .Select(existing => new { existing.TagId, existing.ParentTagId })
                .ToListAsync(cancellationToken);

            var ancestorId = normalizedParentId;
            while (ancestorId is not null)
            {
                if (ancestorId == topic.TagId)
                    return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentInvalid);

                ancestorId = topicLinks
                    .FirstOrDefault(existing => existing.TagId == ancestorId)
                    ?.ParentTagId;
            }
        }

        topic.ParentTagId = normalizedParentId;
        topic.TagName = normalizedName;
        topic.Description = NormalizeOptionalText(request.Description);
        topic.Grade = request.Grade;
        topic.DisplayOrder = request.DisplayOrder;
        topic.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<TagTopicTreeResponse>.Success(ToResponse(topic));
    }

    private static Error? ValidateRequest(UpdateTagTopicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TagName))
            return QuestionBankErrors.TagNameRequired;

        if (request.TagName.Trim().Length > 50)
            return QuestionBankErrors.TagNameTooLong;

        if (request.Description?.Length > 255)
            return QuestionBankErrors.TagDescriptionTooLong;

        if (request.Grade is not (10 or 11 or 12))
            return QuestionBankErrors.TagGradeInvalid;

        if (request.DisplayOrder <= 0)
            return QuestionBankErrors.TagDisplayOrderInvalid;

        return null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TagTopicTreeResponse ToResponse(TagTopic topic)
    {
        return new TagTopicTreeResponse(
            topic.TagId,
            topic.ParentTagId,
            topic.TagName,
            topic.Description,
            topic.Grade,
            topic.DisplayOrder,
            topic.IsActive,
            Array.Empty<TagTopicTreeResponse>());
    }
}
