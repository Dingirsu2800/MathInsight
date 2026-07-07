using System.Text.Json;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.QuestionBank.Commands.UpdateQuestion;

public sealed class UpdateQuestionCommandHandler
    : IRequestHandler<UpdateQuestionCommand, Result<UpdateQuestionResponse>>
{
    private readonly QuestionBankDbContext _context;

    public UpdateQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<UpdateQuestionResponse>> Handle(
        UpdateQuestionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.QuestionId))
            return Result<UpdateQuestionResponse>.Failure(QuestionBankErrors.QuestionIdRequired);

        var request = command.Request;
        var dbQuestionType = MapQuestionType(request.QuestionType);

        if (dbQuestionType is null)
            return Result<UpdateQuestionResponse>.Failure(QuestionBankErrors.QuestionInvalidType);

        var validationError = ValidateRequest(request, dbQuestionType);
        if (validationError is not null)
            return Result<UpdateQuestionResponse>.Failure(validationError);

        var question = await _context.Questions
            .Include(question => question.Answers)
            .Include(question => question.Parts)
            .Include(question => question.QuestionTopics)
                .ThenInclude(topic => topic.Tag)
            .FirstOrDefaultAsync(
                question => question.QuestionId == command.QuestionId,
                cancellationToken);

        if (question is null)
            return Result<UpdateQuestionResponse>.Failure(QuestionBankErrors.QuestionNotFound);

        if (!string.Equals(question.ExpertId, command.ExpertId, StringComparison.OrdinalIgnoreCase))
            return Result<UpdateQuestionResponse>.Failure(QuestionBankErrors.QuestionUpdateForbidden);

        var referenceValidationError = await ValidateReferencesAsync(request, cancellationToken);
        if (referenceValidationError is not null)
            return Result<UpdateQuestionResponse>.Failure(referenceValidationError);

        var versionCreated = string.Equals(question.Status, "Approved", StringComparison.OrdinalIgnoreCase);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (versionCreated)
            _context.QuestionVersions.Add(CreateSnapshot(question, command.ExpertId));

        var oldTopics = question.QuestionTopics.ToList();
        var oldAnswers = question.Answers.ToList();
        var oldParts = question.Parts.ToList();

        _context.QuestionTopics.RemoveRange(oldTopics);
        _context.Answers.RemoveRange(oldAnswers);
        _context.QuestionParts.RemoveRange(oldParts);

        await _context.SaveChangesAsync(cancellationToken);

        question.QuestionTopics.Clear();
        question.Answers.Clear();
        question.Parts.Clear();

        question.QuestionContent = request.QuestionContent;
        question.SolutionContent = request.SolutionContent;
        question.PictureUrl = request.PictureUrl;
        question.DifficultyId = request.DifficultyId;
        question.Grade = request.Grade;
        question.QuestionType = dbQuestionType;
        question.DefaultPoint = request.DefaultPoint;

        foreach (var topic in request.Topics)
        {
            question.QuestionTopics.Add(new QuestionTopic
            {
                QuestionTopicId = Guid.NewGuid().ToString(),
                QuestionId = question.QuestionId,
                TagId = topic.TagId,
                IsPrimary = topic.IsPrimary
            });
        }

        if (dbQuestionType == "Composite")
        {
            foreach (var part in request.Parts)
            {
                question.Parts.Add(new QuestionPart
                {
                    PartId = Guid.NewGuid().ToString(),
                    QuestionId = question.QuestionId,
                    PartOrder = part.PartOrder,
                    PartLabel = part.PartLabel,
                    PartContent = part.PartContent,
                    PartType = MapPartType(part.PartType)!,
                    CorrectBoolean = part.CorrectBoolean,
                    CorrectText = part.CorrectText,
                    CorrectNumeric = part.CorrectNumeric,
                    NumericTolerance = part.NumericTolerance,
                    Explanation = part.Explanation,
                    DefaultPoint = part.DefaultPoint
                });
            }
        }
        else
        {
            foreach (var answer in request.Answers)
            {
                question.Answers.Add(new Answer
                {
                    AnswerId = Guid.NewGuid().ToString(),
                    QuestionId = question.QuestionId,
                    AnswerContent = answer.AnswerContent,
                    IsCorrect = answer.IsCorrect
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<UpdateQuestionResponse>.Success(
            new UpdateQuestionResponse(question.QuestionId, question.Status, versionCreated));
    }

    private static QuestionVersion CreateSnapshot(Question question, string expertId)
    {
        var answersSnapshot = JsonSerializer.Serialize(new
        {
            question.QuestionType,
            question.DifficultyId,
            question.Grade,
            question.DefaultPoint,
            Topics = question.QuestionTopics
                .OrderByDescending(topic => topic.IsPrimary)
                .ThenBy(topic => topic.TagId)
                .Select(topic => new
                {
                    topic.TagId,
                    TagName = topic.Tag.TagName,
                    topic.IsPrimary
                }),
            Answers = question.Answers
                .OrderBy(answer => answer.AnswerId)
                .Select(answer => new
                {
                    answer.AnswerId,
                    answer.AnswerContent,
                    answer.IsCorrect
                }),
            Parts = question.Parts
                .OrderBy(part => part.PartOrder)
                .Select(part => new
                {
                    part.PartId,
                    part.PartOrder,
                    part.PartLabel,
                    part.PartContent,
                    part.PartType,
                    part.CorrectBoolean,
                    part.CorrectText,
                    part.CorrectNumeric,
                    part.NumericTolerance,
                    part.Explanation,
                    part.DefaultPoint
                })
        });

        return new QuestionVersion
        {
            VersionId = Guid.NewGuid().ToString(),
            QuestionId = question.QuestionId,
            QuestionContent = question.QuestionContent,
            QuestionAnswer = question.SolutionContent,
            AnswersSnapshot = answersSnapshot,
            PictureUrl = question.PictureUrl,
            CreatedTime = DateTime.UtcNow,
            ExpertId = expertId
        };
    }

    private async Task<Error?> ValidateReferencesAsync(
        UpdateQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var difficultyExists = await _context.TagDifficulties
            .AnyAsync(
                difficulty => difficulty.DifficultyId == request.DifficultyId,
                cancellationToken);

        if (!difficultyExists)
            return QuestionBankErrors.QuestionDifficultyNotFound;

        var topicIds = request.Topics
            .Select(topic => topic.TagId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTopicCount = await _context.TagTopics
            .CountAsync(topic => topicIds.Contains(topic.TagId), cancellationToken);

        if (existingTopicCount != topicIds.Count)
            return QuestionBankErrors.QuestionTopicNotFound;

        return null;
    }

    private static Error? ValidateRequest(UpdateQuestionRequest request, string dbQuestionType)
    {
        if (string.IsNullOrWhiteSpace(request.QuestionContent))
            return QuestionBankErrors.QuestionContentRequired;

        if (string.IsNullOrWhiteSpace(request.DifficultyId))
            return QuestionBankErrors.QuestionDifficultyRequired;

        if (request.Grade is not (10 or 11 or 12))
            return QuestionBankErrors.QuestionGradeInvalid;

        if (request.DefaultPoint < 0m || request.DefaultPoint > 10m)
            return QuestionBankErrors.QuestionDefaultPointInvalid;

        if (request.Topics is null || request.Topics.Count == 0)
            return QuestionBankErrors.QuestionTopicRequired;

        if (request.Topics.Any(topic => string.IsNullOrWhiteSpace(topic.TagId)))
            return QuestionBankErrors.QuestionTopicRequired;

        if (request.Topics.Count(topic => topic.IsPrimary) != 1)
            return QuestionBankErrors.QuestionPrimaryTopicInvalid;

        if (request.Topics
            .GroupBy(topic => topic.TagId, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            return QuestionBankErrors.QuestionTopicDuplicate;
        }

        if (dbQuestionType == "Composite")
        {
            if (request.Parts is null || request.Parts.Count == 0)
                return QuestionBankErrors.QuestionPartRequired;

            if (request.Parts.Any(part => string.IsNullOrWhiteSpace(part.PartContent)))
                return QuestionBankErrors.QuestionPartContentRequired;

            if (request.Parts.Any(part => part.PartOrder <= 0))
                return QuestionBankErrors.QuestionPartOrderInvalid;

            if (request.Parts
                .GroupBy(part => part.PartOrder)
                .Any(group => group.Count() > 1))
            {
                return QuestionBankErrors.QuestionPartOrderDuplicate;
            }

            if (request.Parts.Any(part => part.DefaultPoint < 0m || part.DefaultPoint > 10m))
                return QuestionBankErrors.QuestionPartDefaultPointInvalid;

            if (request.Parts.Any(part => part.NumericTolerance is < 0m))
                return QuestionBankErrors.QuestionPartNumericToleranceInvalid;

            foreach (var part in request.Parts)
            {
                var dbPartType = MapPartType(part.PartType);
                if (dbPartType is null)
                    return QuestionBankErrors.QuestionPartInvalidType;

                if (dbPartType == "TrueFalse" &&
                    (part.CorrectBoolean is null ||
                     part.CorrectText is not null ||
                     part.CorrectNumeric is not null ||
                     part.NumericTolerance is not null))
                {
                    return QuestionBankErrors.QuestionTrueFalsePartAnswerInvalid;
                }

                if (dbPartType == "ShortAnswer" &&
                    (part.CorrectBoolean is not null ||
                     string.IsNullOrWhiteSpace(part.CorrectText) ||
                     part.CorrectNumeric is not null ||
                     part.NumericTolerance is not null))
                {
                    return QuestionBankErrors.QuestionShortAnswerPartAnswerInvalid;
                }

                if (dbPartType == "NumericAnswer" &&
                    (part.CorrectBoolean is not null ||
                     part.CorrectText is not null ||
                     part.CorrectNumeric is null))
                {
                    return QuestionBankErrors.QuestionNumericAnswerPartAnswerInvalid;
                }
            }
        }
        else
        {
            if (request.Answers is null || request.Answers.Count == 0)
                return QuestionBankErrors.QuestionAnswerRequired;

            if (request.Answers.Any(answer => string.IsNullOrWhiteSpace(answer.AnswerContent)))
                return QuestionBankErrors.QuestionAnswerContentRequired;
        }

        if (dbQuestionType != "Composite" && request.Parts is { Count: > 0 })
            return QuestionBankErrors.QuestionPartNotAllowed;

        if (dbQuestionType == "Composite" && request.Answers is { Count: > 0 })
            return QuestionBankErrors.QuestionAnswerNotAllowed;

        if (dbQuestionType == "SingleChoice" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionSingleChoiceCorrectAnswerRequired;

        if (dbQuestionType == "MultipleChoice" && !request.Answers.Any(answer => answer.IsCorrect))
            return QuestionBankErrors.QuestionMultipleChoiceCorrectAnswerRequired;

        if (dbQuestionType == "TrueFalse" && request.Answers.Count != 2)
            return QuestionBankErrors.QuestionTrueFalseAnswerCountInvalid;

        if (dbQuestionType == "TrueFalse" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionTrueFalseCorrectAnswerRequired;

        if (dbQuestionType == "ShortAnswer" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionShortAnswerCorrectAnswerRequired;

        return null;
    }

    private static string? MapQuestionType(string? questionType)
    {
        if (string.IsNullOrWhiteSpace(questionType))
            return null;

        return questionType.Trim().ToUpperInvariant() switch
        {
            "SINGLE_CHOICE" => "SingleChoice",
            "SINGLECHOICE" => "SingleChoice",
            "MULTIPLE_CHOICE" => "MultipleChoice",
            "MULTIPLE_SELECT" => "MultipleChoice",
            "MULTIPLECHOICE" => "MultipleChoice",
            "TRUE_FALSE" => "TrueFalse",
            "TRUEFALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
            "SHORTANSWER" => "ShortAnswer",
            "COMPOSITE" => "Composite",
            _ => null
        };
    }

    private static string? MapPartType(string? partType)
    {
        if (string.IsNullOrWhiteSpace(partType))
            return null;

        return partType.Trim().ToUpperInvariant() switch
        {
            "TRUE_FALSE" => "TrueFalse",
            "TRUEFALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
            "SHORTANSWER" => "ShortAnswer",
            "NUMERIC_ANSWER" => "NumericAnswer",
            "NUMERICANSWER" => "NumericAnswer",
            _ => null
        };
    }
}
