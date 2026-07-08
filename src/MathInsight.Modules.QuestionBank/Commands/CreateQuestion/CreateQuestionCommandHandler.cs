using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Modules.QuestionBank.Persistence;
using MathInsight.Shared.Results;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateQuestion;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, Result<CreateQuestionResponse>>
{
    private readonly QuestionBankDbContext _context;

    public CreateQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CreateQuestionResponse>> Handle(CreateQuestionCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var questionId = Guid.NewGuid().ToString();

        var dbQuestionType = MapQuestionType(request.QuestionType);

        if (dbQuestionType is null)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionInvalidType);

        if (string.IsNullOrWhiteSpace(request.QuestionContent))
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionContentRequired);

        if (string.IsNullOrWhiteSpace(request.DifficultyId))
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionDifficultyRequired);

        if (request.Grade is not (10 or 11 or 12))
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionGradeInvalid);

        if (request.DefaultPoint < 0m || request.DefaultPoint > 10m)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionDefaultPointInvalid);

        if (request.Topics is null || request.Topics.Count == 0)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTopicRequired);

        if (request.Topics.Any(topic => string.IsNullOrWhiteSpace(topic.TagId)))
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTopicRequired);

        if (request.Topics.Count(topic => topic.IsPrimary) != 1)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPrimaryTopicInvalid);

        if (request.Topics
            .GroupBy(topic => topic.TagId, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTopicDuplicate);
        }

        if (dbQuestionType == "Composite")
        {
            if (request.Parts is null || request.Parts.Count == 0)
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartRequired);

            if (request.Parts.Any(part => string.IsNullOrWhiteSpace(part.PartContent)))
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartContentRequired);

            if (request.Parts.Any(part => part.PartOrder <= 0))
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartOrderInvalid);

            if (request.Parts
                .GroupBy(part => part.PartOrder)
                .Any(group => group.Count() > 1))
            {
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartOrderDuplicate);
            }

            if (request.Parts.Any(part => part.DefaultPoint < 0m || part.DefaultPoint > 10m))
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartDefaultPointInvalid);

            if (request.Parts.Any(part => part.NumericTolerance is < 0m))
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartNumericToleranceInvalid);
        }
        else
        {
            if (request.Answers is null || request.Answers.Count == 0)
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionAnswerRequired);

            if (request.Answers.Any(answer => string.IsNullOrWhiteSpace(answer.AnswerContent)))
                return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionAnswerContentRequired);
        }

        if (dbQuestionType != "Composite" && request.Parts is { Count: > 0 })
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartNotAllowed);

        if (dbQuestionType == "Composite" && request.Answers is { Count: > 0 })
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionAnswerNotAllowed);

        if (dbQuestionType == "SingleChoice" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionSingleChoiceCorrectAnswerRequired);

        if (dbQuestionType == "MultipleChoice" && !request.Answers.Any(answer => answer.IsCorrect))
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionMultipleChoiceCorrectAnswerRequired);

        if (dbQuestionType == "TrueFalse" && request.Answers.Count != 2)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTrueFalseAnswerCountInvalid);

        if (dbQuestionType == "TrueFalse" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTrueFalseCorrectAnswerRequired);

        if (dbQuestionType == "ShortAnswer" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionShortAnswerCorrectAnswerRequired);

        var question = new Question
        {
            QuestionId = questionId,
            QuestionContent = request.QuestionContent,
            SolutionContent = request.SolutionContent,
            PictureUrl = request.PictureUrl,
            DifficultyId = request.DifficultyId,
            DefaultPoint = request.DefaultPoint,
            Grade = request.Grade,
            QuestionType = dbQuestionType,
            ExpertId = command.ExpertId,
            Status = "Approved",
            IsActive = true
        };

        _context.Questions.Add(question);

        foreach (var topic in request.Topics)
        {
            question.QuestionTopics.Add(new QuestionTopic
            {
                QuestionTopicId = Guid.NewGuid().ToString(),
                TagId = topic.TagId,
                QuestionId = question.QuestionId,
                IsPrimary = topic.IsPrimary,
            });
        }

        if (dbQuestionType == "Composite")
        {
            foreach (var part in request.Parts)
            {
                var dbPartType = MapPartType(part.PartType);
                if (dbPartType is null)
                    return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionPartInvalidType);

                if (dbPartType == "TrueFalse" &&
                    (part.CorrectBoolean is null ||
                     part.CorrectText is not null ||
                     part.CorrectNumeric is not null ||
                     part.NumericTolerance is not null))
                {
                    return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionTrueFalsePartAnswerInvalid);
                }

                if (dbPartType == "ShortAnswer" &&
                    (part.CorrectBoolean is not null ||
                     string.IsNullOrWhiteSpace(part.CorrectText) ||
                     part.CorrectNumeric is not null ||
                     part.NumericTolerance is not null))
                {
                    return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionShortAnswerPartAnswerInvalid);
                }

                if (dbPartType == "NumericAnswer" &&
                    (part.CorrectBoolean is not null ||
                     part.CorrectText is not null ||
                     part.CorrectNumeric is null))
                {
                    return Result<CreateQuestionResponse>.Failure(QuestionBankErrors.QuestionNumericAnswerPartAnswerInvalid);
                }

                question.Parts.Add(new QuestionPart
                {
                    PartId = Guid.NewGuid().ToString(),
                    QuestionId = question.QuestionId,
                    PartOrder = part.PartOrder,
                    PartLabel = part.PartLabel,
                    PartContent = part.PartContent,
                    PartType = dbPartType,
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
                    IsCorrect = answer.IsCorrect,
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreateQuestionResponse>.Success(new CreateQuestionResponse(question.QuestionId, question.Status));
    }

    private static string? MapQuestionType(string? questionType)
    {
        if (string.IsNullOrWhiteSpace(questionType))
            return null;

        return questionType.Trim().ToUpperInvariant() switch
        {
            "SINGLE_CHOICE" => "SingleChoice",
            "MULTIPLE_CHOICE" => "MultipleChoice",
            "MULTIPLE_SELECT" => "MultipleChoice",
            "TRUE_FALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
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
            "SHORT_ANSWER" => "ShortAnswer",
            "NUMERIC_ANSWER" => "NumericAnswer",
            _ => null
        };
    }
}

