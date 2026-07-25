using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.CreateTagTopic;

public sealed class CreateTagTopicCommandHandler
    : IRequestHandler<CreateTagTopicCommand, Result<TagTopicTreeResponse>>
{
    private readonly QuestionBankDbContext _context;

    public CreateTagTopicCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TagTopicTreeResponse>> Handle(
        CreateTagTopicCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var validationError = ValidateRequest(request);
        if (validationError is not null)
            return Result<TagTopicTreeResponse>.Failure(validationError);

        var normalizedName = request.TagName.Trim();
        var normalizedParentId = string.IsNullOrWhiteSpace(request.ParentTagId)
            ? null
            : request.ParentTagId.Trim();

        var nameExists = await _context.TagTopics
            .AnyAsync(topic => topic.TagName == normalizedName, cancellationToken);

        if (nameExists)
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagNameDuplicate);

        if (normalizedParentId is not null)
        {
            var parent = await _context.TagTopics
                .AsNoTracking()
                .FirstOrDefaultAsync(topic => topic.TagId == normalizedParentId, cancellationToken);

            if (parent is null)
                return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentNotFound);

            if (parent.Grade != request.Grade)
                return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagParentInvalid);
        }

        var topic = new TagTopic
        {
            TagId = Guid.NewGuid().ToString(),
            ParentTagId = normalizedParentId,
            TagName = normalizedName,
            Description = NormalizeOptionalText(request.Description),
            Grade = request.Grade,
            DisplayOrder = request.DisplayOrder,
            IsActive = true
        };

        _context.TagTopics.Add(topic);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TagTopicTreeResponse>.Success(ToResponse(topic));
    }

    private static Error? ValidateRequest(CreateTagTopicRequest request)
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
