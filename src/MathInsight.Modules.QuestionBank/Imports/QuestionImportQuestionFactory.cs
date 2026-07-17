using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Entities;
using MathInsight.Modules.QuestionBank.Validation;

namespace MathInsight.Modules.QuestionBank.Imports;

internal static class QuestionImportQuestionFactory
{
    public static Question Create(CreateQuestionRequest request, string expertId, string databaseQuestionType)
    {
        var question = new Question
        {
            QuestionId = Guid.NewGuid().ToString(),
            QuestionContent = request.QuestionContent,
            SolutionContent = request.SolutionContent,
            PictureUrl = string.IsNullOrWhiteSpace(request.PictureUrl) ? null : request.PictureUrl,
            DifficultyId = request.DifficultyId,
            Grade = request.Grade,
            Status = "Approved",
            QuestionType = databaseQuestionType,
            ExpertId = expertId,
            DefaultPoint = request.DefaultPoint,
            IsActive = true
        };

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

        if (databaseQuestionType == "Composite")
        {
            foreach (var part in request.Parts)
            {
                question.Parts.Add(new QuestionPart
                {
                    PartId = Guid.NewGuid().ToString(),
                    QuestionId = question.QuestionId,
                    PartOrder = part.PartOrder,
                    PartLabel = string.IsNullOrWhiteSpace(part.PartLabel) ? null : part.PartLabel.Trim(),
                    PartContent = part.PartContent,
                    PartType = QuestionRequestValidator.MapPartType(part.PartType)!,
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

        return question;
    }
}
