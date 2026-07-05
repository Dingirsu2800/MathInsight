using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Persistence;
using MediatR;

namespace MathInsight.Modules.QuestionBank.Commands.CreateQuestion;

public class CreateQuestionCommandHandler : IRequestHandler<CreateQuestionCommand, CreateQuestionResponse>
{
    private readonly QuestionBankDbContext _context;

    public CreateQuestionCommandHandler(QuestionBankDbContext context)
    {
        _context = context;
    }

    public async Task<CreateQuestionResponse> Handle(CreateQuestionCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var questionId = Guid.NewGuid().ToString();

        var dbQuestionType = MapQuestionType(request.QuestionType);

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



        foreach (var part in request.Parts)
        {
            var dbPartType = MapPartType(part.PartType);
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

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateQuestionResponse(question.QuestionId, question.Status);
    }

    private static string MapQuestionType(string questionType)
    {
        return questionType.Trim().ToUpperInvariant() switch
        {
            "SINGLE_CHOICE" => "SingleChoice",
            "MULTIPLE_CHOICE" => "MultipleChoice",
            "MULTIPLE_SELECT" => "MultipleChoice",
            "TRUE_FALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
            "COMPOSITE" => "Composite",
            _ => throw new InvalidOperationException("Invalid question type.")
        };
    }

    private static string MapPartType(string partType)
    {
        return partType.Trim().ToUpperInvariant() switch
        {
            "TRUE_FALSE" => "TrueFalse",
            "SHORT_ANSWER" => "ShortAnswer",
            "NUMERIC_ANSWER" => "NumericAnswer",
            _ => partType
        };
    }
}

