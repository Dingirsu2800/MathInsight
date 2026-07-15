using System.Text.RegularExpressions;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Shared.Results;

namespace MathInsight.Modules.QuestionBank.Validation;

internal static partial class QuestionRequestValidator
{
    public static Error? Validate(CreateQuestionRequest request, out string? databaseQuestionType)
    {
        databaseQuestionType = MapQuestionType(request.QuestionType);
        if (databaseQuestionType is null)
            return QuestionBankErrors.QuestionInvalidType;

        if (string.IsNullOrWhiteSpace(request.QuestionContent))
            return QuestionBankErrors.QuestionContentRequired;

        if (string.IsNullOrWhiteSpace(request.DifficultyId))
            return QuestionBankErrors.QuestionDifficultyRequired;

        if (request.Grade is not (10 or 11 or 12))
            return QuestionBankErrors.QuestionGradeInvalid;

        if (request.DefaultPoint < 0m || request.DefaultPoint > 10m)
            return QuestionBankErrors.QuestionDefaultPointInvalid;

        if (request.Topics is null || request.Topics.Count == 0 || request.Topics.Any(topic => string.IsNullOrWhiteSpace(topic.TagId)))
            return QuestionBankErrors.QuestionTopicRequired;

        if (request.Topics.Count(topic => topic.IsPrimary) != 1)
            return QuestionBankErrors.QuestionPrimaryTopicInvalid;

        if (request.Topics.GroupBy(topic => topic.TagId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return QuestionBankErrors.QuestionTopicDuplicate;

        if (databaseQuestionType == "Composite")
        {
            if (request.Parts is null || request.Parts.Count == 0)
                return QuestionBankErrors.QuestionPartRequired;

            if (request.Parts.Any(part => string.IsNullOrWhiteSpace(part.PartContent)))
                return QuestionBankErrors.QuestionPartContentRequired;

            if (request.Parts.Any(part => part.PartOrder <= 0))
                return QuestionBankErrors.QuestionPartOrderInvalid;

            if (request.Parts.GroupBy(part => part.PartOrder).Any(group => group.Count() > 1))
                return QuestionBankErrors.QuestionPartOrderDuplicate;

            if (request.Parts.Any(part => part.DefaultPoint < 0m || part.DefaultPoint > 10m))
                return QuestionBankErrors.QuestionPartDefaultPointInvalid;

            if (request.Parts.Any(part => part.NumericTolerance is < 0m))
                return QuestionBankErrors.QuestionPartNumericToleranceInvalid;

            foreach (var part in request.Parts)
            {
                var partType = MapPartType(part.PartType);
                if (partType is null)
                    return QuestionBankErrors.QuestionPartInvalidType;

                if (partType == "TrueFalse" &&
                    (part.CorrectBoolean is null || part.CorrectText is not null || part.CorrectNumeric is not null || part.NumericTolerance is not null))
                {
                    return QuestionBankErrors.QuestionTrueFalsePartAnswerInvalid;
                }

                if (partType == "ShortAnswer" &&
                    (part.CorrectBoolean is not null || !IsPlainShortAnswer(part.CorrectText) || part.CorrectNumeric is not null || part.NumericTolerance is not null))
                {
                    return QuestionBankErrors.QuestionShortAnswerPartAnswerInvalid;
                }

                if (partType == "NumericAnswer" &&
                    (part.CorrectBoolean is not null || part.CorrectText is not null || part.CorrectNumeric is null))
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

        if (databaseQuestionType != "Composite" && request.Parts is { Count: > 0 })
            return QuestionBankErrors.QuestionPartNotAllowed;

        if (databaseQuestionType == "Composite" && request.Answers is { Count: > 0 })
            return QuestionBankErrors.QuestionAnswerNotAllowed;

        if (databaseQuestionType == "SingleChoice" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionSingleChoiceCorrectAnswerRequired;

        if (databaseQuestionType == "MultipleChoice" && !request.Answers.Any(answer => answer.IsCorrect))
            return QuestionBankErrors.QuestionMultipleChoiceCorrectAnswerRequired;

        if (databaseQuestionType == "TrueFalse" && request.Answers.Count != 2)
            return QuestionBankErrors.QuestionTrueFalseAnswerCountInvalid;

        if (databaseQuestionType == "TrueFalse" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionTrueFalseCorrectAnswerRequired;

        if (databaseQuestionType == "ShortAnswer" && request.Answers.Count(answer => answer.IsCorrect) != 1)
            return QuestionBankErrors.QuestionShortAnswerCorrectAnswerRequired;

        if (databaseQuestionType == "ShortAnswer" && !IsPlainShortAnswer(request.Answers.Single(answer => answer.IsCorrect).AnswerContent))
            return QuestionBankErrors.QuestionShortAnswerCorrectAnswerRequired;

        return null;
    }

    public static string? MapQuestionType(string? questionType) =>
        string.IsNullOrWhiteSpace(questionType)
            ? null
            : questionType.Trim().ToUpperInvariant() switch
            {
                "SINGLE_CHOICE" or "SINGLECHOICE" => "SingleChoice",
                "MULTIPLE_CHOICE" or "MULTIPLE_SELECT" or "MULTIPLECHOICE" => "MultipleChoice",
                "TRUE_FALSE" or "TRUEFALSE" => "TrueFalse",
                "SHORT_ANSWER" or "SHORTANSWER" => "ShortAnswer",
                "COMPOSITE" => "Composite",
                _ => null
            };

    public static string? MapPartType(string? partType) =>
        string.IsNullOrWhiteSpace(partType)
            ? null
            : partType.Trim().ToUpperInvariant() switch
            {
                "TRUE_FALSE" or "TRUEFALSE" => "TrueFalse",
                "SHORT_ANSWER" or "SHORTANSWER" => "ShortAnswer",
                "NUMERIC_ANSWER" or "NUMERICANSWER" => "NumericAnswer",
                _ => null
            };

    private static bool IsPlainShortAnswer(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 100 &&
        !MarkupRegex().IsMatch(value) &&
        !value.Contains("![", StringComparison.Ordinal);

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex MarkupRegex();
}
